using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

namespace BookmarkStudio
{
    internal sealed class BookmarkGlyphTagger : ITagger<BookmarkGlyphTag>, IDisposable
    {
        private readonly ITextBuffer _buffer;
        private readonly ITextDocument? _textDocument;
        private readonly object _trackingPointsLock = new();
        private string _documentPath;
        private string _normalizedDocumentPath;
        private int _attachedViewCount;
        private int _isDisposed;
        private volatile IReadOnlyList<ManagedBookmark> _cachedDocumentBookmarks = [];
        private Dictionary<string, ITrackingPoint> _trackingPoints = new(StringComparer.Ordinal);

        public BookmarkGlyphTagger(ITextBuffer buffer, string documentPath)
            : this(buffer, documentPath, null)
        {
        }

        public BookmarkGlyphTagger(ITextBuffer buffer, string documentPath, ITextDocument? textDocument)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _documentPath = documentPath ?? string.Empty;
            _normalizedDocumentPath = BookmarkIdentity.NormalizeDocumentPath(_documentPath);
            _textDocument = textDocument;

            RefreshCachedBookmarks();
            BookmarkStudioSession.Current.BookmarksChanged += OnBookmarksChanged;
            _buffer.Changed += OnBufferChanged;

            if (_textDocument is not null)
            {
                _textDocument.FileActionOccurred += OnFileActionOccurred;
            }
        }

        public event EventHandler<SnapshotSpanEventArgs>? TagsChanged;

        public void AttachToView(ITextView textView)
        {
            if (textView is null)
            {
                return;
            }

            Interlocked.Increment(ref _attachedViewCount);
            EventHandler? closedHandler = null;
            closedHandler = (sender, args) =>
            {
                textView.Closed -= closedHandler;
                if (Interlocked.Decrement(ref _attachedViewCount) == 0)
                {
                    Dispose();
                }
            };

            textView.Closed += closedHandler;
        }

        public IEnumerable<ITagSpan<BookmarkGlyphTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans is null || spans.Count == 0 || string.IsNullOrWhiteSpace(_normalizedDocumentPath))
            {
                yield break;
            }

            ITextSnapshot snapshot = spans[0].Snapshot;
            var startLine = spans[0].Start.GetContainingLine().LineNumber;
            var endLine = spans[spans.Count - 1].End.GetContainingLine().LineNumber;

            // Use cached bookmarks for this document instead of filtering all bookmarks
            IReadOnlyList<ManagedBookmark> documentBookmarks = _cachedDocumentBookmarks;
            Dictionary<string, ITrackingPoint> trackingPoints;

            lock (_trackingPointsLock)
            {
                trackingPoints = _trackingPoints;
            }

            foreach (ManagedBookmark bookmark in documentBookmarks)
            {
                int lineIndex;

                // Use tracking point if available for real-time position updates
                if (trackingPoints.TryGetValue(bookmark.BookmarkId, out ITrackingPoint trackingPoint))
                {
                    SnapshotPoint currentPoint = trackingPoint.GetPoint(snapshot);
                    lineIndex = currentPoint.GetContainingLine().LineNumber;
                }
                else
                {
                    // Fall back to cached line number (1-based to 0-based)
                    lineIndex = bookmark.LineNumber - 1;
                }

                if (lineIndex < startLine || lineIndex > endLine || lineIndex < 0 || lineIndex >= snapshot.LineCount)
                {
                    continue;
                }

                ITextSnapshotLine line = snapshot.GetLineFromLineNumber(lineIndex);
                var spanLength = line.Length > 0 ? 1 : 0;
                var span = new SnapshotSpan(line.Start, spanLength);
                var glyphTag = new BookmarkGlyphTag
                {
                    BookmarkId = bookmark.BookmarkId,
                    Label = bookmark.Label,
                    ShortcutNumber = bookmark.ShortcutNumber,
                    Color = bookmark.Color,
                };
                yield return new TagSpan<BookmarkGlyphTag>(span, glyphTag);
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) == 1)
            {
                return;
            }

            BookmarkStudioSession.Current.BookmarksChanged -= OnBookmarksChanged;
            _buffer.Changed -= OnBufferChanged;

            if (_textDocument is not null)
            {
                _textDocument.FileActionOccurred -= OnFileActionOccurred;
            }
        }

        private bool MatchesDocumentPath(ManagedBookmark bookmark)
            => string.Equals(bookmark.NormalizedDocumentPath, _normalizedDocumentPath, StringComparison.Ordinal);

        private void RefreshCachedBookmarks()
        {
            IReadOnlyList<ManagedBookmark> oldBookmarks = _cachedDocumentBookmarks;
            IReadOnlyList<ManagedBookmark> newBookmarks = BookmarkStudioSession.Current.CachedBookmarks
                .Where(MatchesDocumentPath)
                .ToArray();

            _cachedDocumentBookmarks = newBookmarks;

            // Create or update tracking points for each bookmark
            RebuildTrackingPoints(oldBookmarks, newBookmarks);
        }

        private void RebuildTrackingPoints(IReadOnlyList<ManagedBookmark> oldBookmarks, IReadOnlyList<ManagedBookmark> newBookmarks)
        {
            ITextSnapshot snapshot = _buffer.CurrentSnapshot;
            var newTrackingPoints = new Dictionary<string, ITrackingPoint>(StringComparer.Ordinal);

            Dictionary<string, ITrackingPoint> existingTrackingPoints;
            lock (_trackingPointsLock)
            {
                existingTrackingPoints = _trackingPoints;
            }

            // Build a lookup of old bookmark line numbers to detect explicit moves
            var oldLineNumbers = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (ManagedBookmark oldBookmark in oldBookmarks)
            {
                oldLineNumbers[oldBookmark.BookmarkId] = oldBookmark.LineNumber;
            }

            foreach (ManagedBookmark bookmark in newBookmarks)
            {
                var targetLineIndex = bookmark.LineNumber - 1;

                // Check if we have an existing tracking point
                if (existingTrackingPoints.TryGetValue(bookmark.BookmarkId, out ITrackingPoint existingPoint))
                {
                    SnapshotPoint currentPoint = existingPoint.GetPoint(snapshot);
                    var trackedLineIndex = currentPoint.GetContainingLine().LineNumber;

                    // Check if the tracking point is still valid (within bounds)
                    if (trackedLineIndex >= 0 && trackedLineIndex < snapshot.LineCount)
                    {
                        // Determine if this is an explicit move vs tracked position:
                        // - If the bookmark's stored LineNumber changed AND doesn't match where the
                        //   tracking point tracked to, it's an explicit move (e.g., drag-drop)
                        // - If the bookmark's stored LineNumber matches the tracked position, the
                        //   debounced update caught up - we can use either (they're the same)
                        // - If the bookmark's stored LineNumber is stale (doesn't match tracked and
                        //   wasn't explicitly changed), preserve the tracking point

                        var wasExplicitlyMoved = false;
                        if (oldLineNumbers.TryGetValue(bookmark.BookmarkId, out var oldLineNumber))
                        {
                            // Bookmark existed before - check if its line number was explicitly changed
                            // An explicit move: old != new AND new != tracked
                            // A tracked update: old != new AND new == tracked (debounce caught up)
                            // Stale state: old == new AND new != tracked (tracking is ahead)
                            var storedLineChanged = oldLineNumber != bookmark.LineNumber;
                            var storedMatchesTracked = (bookmark.LineNumber - 1) == trackedLineIndex;

                            wasExplicitlyMoved = storedLineChanged && !storedMatchesTracked;
                        }

                        if (!wasExplicitlyMoved)
                        {
                            // Preserve the tracking point - it has the accurate position
                            newTrackingPoints[bookmark.BookmarkId] = existingPoint;
                            continue;
                        }

                        // Explicit move detected - fall through to create new tracking point
                    }
                }

                // No existing tracking point, invalid tracking point, or explicit move
                // Create a new tracking point at the target line
                if (targetLineIndex < 0 || targetLineIndex >= snapshot.LineCount)
                {
                    continue;
                }

                ITextSnapshotLine line = snapshot.GetLineFromLineNumber(targetLineIndex);

                // Create tracking point at the start of the line
                // Use Positive tracking so insertions before this point push it forward
                ITrackingPoint trackingPoint = snapshot.CreateTrackingPoint(line.Start.Position, PointTrackingMode.Positive);
                newTrackingPoints[bookmark.BookmarkId] = trackingPoint;
            }

            lock (_trackingPointsLock)
            {
                _trackingPoints = newTrackingPoints;
            }
        }

        private void OnFileActionOccurred(object sender, TextDocumentFileActionEventArgs e)
        {
            if (Volatile.Read(ref _isDisposed) == 1)
            {
                return;
            }

            // Handle file rename - update our cached document path
            if (e.FileActionType == FileActionTypes.DocumentRenamed && !string.IsNullOrEmpty(e.FilePath))
            {
                _documentPath = e.FilePath;
                _normalizedDocumentPath = BookmarkIdentity.NormalizeDocumentPath(_documentPath);

                // Refresh bookmarks with the new path and notify of tag changes
                RefreshCachedBookmarks();

                ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
                {
                    if (Volatile.Read(ref _isDisposed) == 1)
                    {
                        return;
                    }

                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    if (Volatile.Read(ref _isDisposed) == 1)
                    {
                        return;
                    }

                    ITextSnapshot snapshot = _buffer.CurrentSnapshot;
                    var span = new SnapshotSpan(snapshot, 0, snapshot.Length);
                    TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));
                }).FireAndForget();
            }
        }

        private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            if (Volatile.Read(ref _isDisposed) == 1)
            {
                return;
            }

            // When buffer changes, the tracking points automatically track to new positions.
            // We just need to notify that tags may have changed so GetTags is called again.
            ITextSnapshot snapshot = e.After;
            var fullSpan = new SnapshotSpan(snapshot, 0, snapshot.Length);
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(fullSpan));
        }

        private void OnBookmarksChanged(object sender, EventArgs e)
        {
            if (Volatile.Read(ref _isDisposed) == 1)
            {
                return;
            }

            RefreshCachedBookmarks();

            // Capture disposal state before scheduling async work to avoid queuing
            // unnecessary UI thread operations during solution close
            if (Volatile.Read(ref _isDisposed) == 1)
            {
                return;
            }

            ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                if (Volatile.Read(ref _isDisposed) == 1)
                {
                    return;
                }

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                if (Volatile.Read(ref _isDisposed) == 1)
                {
                    return;
                }

                ITextSnapshot snapshot = _buffer.CurrentSnapshot;
                var span = new SnapshotSpan(snapshot, 0, snapshot.Length);
                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));
            }).FireAndForget();
        }
    }
}
