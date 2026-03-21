using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;

namespace BookmarkStudio
{
    internal sealed class BookmarkGlyphTagger : ITagger<BookmarkGlyphTag>, IDisposable
    {
        private readonly ITextBuffer _buffer;
        private readonly string _documentPath;
        private readonly string _normalizedDocumentPath;
        private readonly object _trackingPointsLock = new object();
        private int _attachedViewCount;
        private int _isDisposed;
        private volatile IReadOnlyList<ManagedBookmark> _cachedDocumentBookmarks = Array.Empty<ManagedBookmark>();
        private Dictionary<string, ITrackingPoint> _trackingPoints = new Dictionary<string, ITrackingPoint>(StringComparer.Ordinal);

        public BookmarkGlyphTagger(ITextBuffer buffer, string documentPath)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _documentPath = documentPath ?? string.Empty;
            _normalizedDocumentPath = BookmarkIdentity.NormalizeDocumentPath(_documentPath);
            RefreshCachedBookmarks();
            BookmarkStudioSession.Current.BookmarksChanged += OnBookmarksChanged;
            _buffer.Changed += OnBufferChanged;
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
            int startLine = spans[0].Start.GetContainingLine().LineNumber;
            int endLine = spans[spans.Count - 1].End.GetContainingLine().LineNumber;

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
                int spanLength = line.Length > 0 ? 1 : 0;
                SnapshotSpan span = new SnapshotSpan(line.Start, spanLength);
                BookmarkGlyphTag glyphTag = new BookmarkGlyphTag
                {
                    BookmarkId = bookmark.BookmarkId,
                    Label = bookmark.Label,
                    SlotNumber = bookmark.SlotNumber,
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
        }

        private bool MatchesDocumentPath(ManagedBookmark bookmark)
            => string.Equals(BookmarkIdentity.NormalizeDocumentPath(bookmark.DocumentPath), _normalizedDocumentPath, StringComparison.Ordinal);

        private void RefreshCachedBookmarks()
        {
            IReadOnlyList<ManagedBookmark> bookmarks = BookmarkStudioSession.Current.CachedBookmarks
                .Where(MatchesDocumentPath)
                .ToArray();

            _cachedDocumentBookmarks = bookmarks;

            // Create or update tracking points for each bookmark
            RebuildTrackingPoints(bookmarks);
        }

        private void RebuildTrackingPoints(IReadOnlyList<ManagedBookmark> bookmarks)
        {
            ITextSnapshot snapshot = _buffer.CurrentSnapshot;
            var newTrackingPoints = new Dictionary<string, ITrackingPoint>(StringComparer.Ordinal);

            Dictionary<string, ITrackingPoint> existingTrackingPoints;
            lock (_trackingPointsLock)
            {
                existingTrackingPoints = _trackingPoints;
            }

            foreach (ManagedBookmark bookmark in bookmarks)
            {
                int targetLineIndex = bookmark.LineNumber - 1;
                if (targetLineIndex < 0 || targetLineIndex >= snapshot.LineCount)
                {
                    continue;
                }

                // Check if we have an existing tracking point and if it's still on the same line
                // as the bookmark's persisted line number. If the line number changed (e.g., via
                // drag-and-drop), we need to create a new tracking point at the new location.
                if (existingTrackingPoints.TryGetValue(bookmark.BookmarkId, out ITrackingPoint existingPoint))
                {
                    SnapshotPoint currentPoint = existingPoint.GetPoint(snapshot);
                    int currentLineIndex = currentPoint.GetContainingLine().LineNumber;

                    // If the tracking point is still on the target line, preserve it
                    if (currentLineIndex == targetLineIndex)
                    {
                        newTrackingPoints[bookmark.BookmarkId] = existingPoint;
                        continue;
                    }
                }

                // Create new tracking point at the bookmark's line number
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

        private void OnBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            if (Volatile.Read(ref _isDisposed) == 1)
            {
                return;
            }

            // When buffer changes, the tracking points automatically track to new positions.
            // We just need to notify that tags may have changed so GetTags is called again.
            ITextSnapshot snapshot = e.After;
            SnapshotSpan fullSpan = new SnapshotSpan(snapshot, 0, snapshot.Length);
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
                SnapshotSpan span = new SnapshotSpan(snapshot, 0, snapshot.Length);
                TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));
            }).FireAndForget();
        }
    }
}
