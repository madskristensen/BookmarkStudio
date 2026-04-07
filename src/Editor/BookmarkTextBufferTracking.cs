using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace BookmarkStudio
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    internal sealed class BookmarkTextViewCreationListener : IWpfTextViewCreationListener
    {
        [Import]
        internal ITextDocumentFactoryService TextDocumentFactoryService = null!;

        public void TextViewCreated(IWpfTextView textView)
        {
            if (textView is null)
            {
                return;
            }

            if (!TextDocumentFactoryService.TryGetTextDocument(textView.TextBuffer, out ITextDocument document))
            {
                return;
            }

            BookmarkTextBufferTracker tracker = textView.TextBuffer.Properties.GetOrCreateSingletonProperty(() => new BookmarkTextBufferTracker(textView.TextBuffer, document.FilePath));
            tracker.AttachToView(textView);
        }
    }

    internal sealed class BookmarkTextBufferTracker : IDisposable
    {
        private const int DebounceDelayMs = 500;

        private readonly string _documentPath;
        private readonly string _normalizedDocumentPath;
        private readonly SemaphoreSlim _updateGate = new(1, 1);
        private readonly ITextBuffer _textBuffer;
        private readonly object _trackingPointsLock = new();
        private readonly object _debounceCtsLock = new();
        private Dictionary<string, ITrackingPoint> _trackingPoints = new(StringComparer.Ordinal);
        private int _attachedViewCount;
        private int _isDisposed;
        private CancellationTokenSource? _debounceCts;

        public BookmarkTextBufferTracker(ITextBuffer textBuffer, string documentPath)
        {
            _textBuffer = textBuffer ?? throw new ArgumentNullException(nameof(textBuffer));
            _documentPath = documentPath ?? string.Empty;
            _normalizedDocumentPath = BookmarkIdentity.NormalizeDocumentPath(_documentPath);
            InitializeTrackingPoints();
            _textBuffer.ChangedLowPriority += OnTextBufferChanged;
            BookmarkStudioSession.Current.BookmarksChanged += OnBookmarksChanged;
        }

        public void AttachToView(IWpfTextView textView)
        {
            if (textView is null)
            {
                return;
            }

            _ = Interlocked.Increment(ref _attachedViewCount);
            void closedHandler(object sender, EventArgs args)
            {
                textView.Closed -= closedHandler;
                if (Interlocked.Decrement(ref _attachedViewCount) == 0)
                {
                    Dispose();
                }
            }

            textView.Closed += closedHandler;
        }

        private void OnTextBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            if (Volatile.Read(ref _isDisposed) == 1)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_normalizedDocumentPath) || e is null || e.Before is null || e.After is null || e.Changes.Count == 0)
            {
                return;
            }

            // Cancel any pending debounced update and create new CTS under lock
            CancellationToken debounceToken;
            lock (_debounceCtsLock)
            {
                _debounceCts?.Cancel();
                _debounceCts?.Dispose();
                _debounceCts = new CancellationTokenSource();
                debounceToken = _debounceCts.Token;
            }

            // Check disposal again before scheduling async work to avoid queuing
            // unnecessary operations during solution close
            if (Volatile.Read(ref _isDisposed) == 1)
            {
                return;
            }

            ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                try
                {
                    // Debounce: wait before processing to coalesce rapid changes
                    await Task.Delay(DebounceDelayMs, debounceToken);
                }
                catch (OperationCanceledException)
                {
                    // A newer change came in or disposal occurred, skip this one
                    return;
                }

                if (Volatile.Read(ref _isDisposed) == 1)
                {
                    return;
                }

                await _updateGate.WaitAsync(debounceToken);
                try
                {
                    if (Volatile.Read(ref _isDisposed) == 1)
                    {
                        return;
                    }

                    IReadOnlyList<ManagedBookmark> bookmarks = await BookmarkStudioSession.Current.TryUpdateBookmarksAsync(
                        metadata => UpdateBookmarksFromTrackingPoints(metadata),
                        debounceToken);

                    if (bookmarks.Any(bookmark => MatchesDocumentPath(bookmark.DocumentPath)))
                    {
                        await BookmarkManagerToolWindow.RefreshIfVisibleAsync(debounceToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Disposal or cancellation occurred, exit gracefully
                }
                finally
                {
                    _ = _updateGate.Release();
                }
            }).FireAndForget();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) == 1)
            {
                return;
            }

            lock (_debounceCtsLock)
            {
                _debounceCts?.Cancel();
                _debounceCts?.Dispose();
                _debounceCts = null;
            }

            _textBuffer.ChangedLowPriority -= OnTextBufferChanged;
            BookmarkStudioSession.Current.BookmarksChanged -= OnBookmarksChanged;
            _updateGate.Dispose();
        }

        private void InitializeTrackingPoints()
        {
            ITextSnapshot snapshot = _textBuffer.CurrentSnapshot;
            IReadOnlyList<ManagedBookmark> bookmarks = BookmarkStudioSession.Current.CachedBookmarks;
            var newTrackingPoints = new Dictionary<string, ITrackingPoint>(StringComparer.Ordinal);

            foreach (ManagedBookmark bookmark in bookmarks)
            {
                if (!MatchesDocumentPath(bookmark.DocumentPath))
                {
                    continue;
                }

                var lineIndex = bookmark.LineNumber - 1;
                if (lineIndex < 0 || lineIndex >= snapshot.LineCount)
                {
                    continue;
                }

                ITextSnapshotLine line = snapshot.GetLineFromLineNumber(lineIndex);
                newTrackingPoints[bookmark.BookmarkId] = CreateStableTrackingPoint(line);
            }

            lock (_trackingPointsLock)
            {
                _trackingPoints = newTrackingPoints;
            }
        }

        /// <summary>
        /// When bookmarks are added or changed, eagerly create tracking points for any
        /// new bookmarks that don't already have one. This ensures tracking points exist
        /// BEFORE the next text change, preventing the stale-line-number bug where a
        /// bookmark's metadata line number is applied against a post-edit snapshot.
        /// </summary>
        private void OnBookmarksChanged(object sender, EventArgs e)
        {
            if (Volatile.Read(ref _isDisposed) == 1)
            {
                return;
            }

            ITextSnapshot snapshot = _textBuffer.CurrentSnapshot;
            IReadOnlyList<ManagedBookmark> bookmarks = BookmarkStudioSession.Current.CachedBookmarks;

            Dictionary<string, ITrackingPoint> trackingPoints;
            lock (_trackingPointsLock)
            {
                trackingPoints = _trackingPoints;
            }

            Dictionary<string, ITrackingPoint>? updated = null;

            foreach (ManagedBookmark bookmark in bookmarks)
            {
                if (!MatchesDocumentPath(bookmark.DocumentPath))
                {
                    continue;
                }

                var lineIndex = bookmark.LineNumber - 1;
                if (lineIndex < 0 || lineIndex >= snapshot.LineCount)
                {
                    continue;
                }

                // Check whether the existing tracking point already matches the
                // bookmark's metadata line number.  When a bookmark is moved (e.g.
                // dragged to a new line), the metadata is updated but the old
                // tracking point still resolves to the original position.  We must
                // replace it so that subsequent text edits track from the new location.
                if (trackingPoints.TryGetValue(bookmark.BookmarkId, out ITrackingPoint existingPoint))
                {
                    int trackedLine = existingPoint.GetPoint(snapshot).GetContainingLine().LineNumber;
                    if (trackedLine == lineIndex)
                    {
                        continue;
                    }
                }

                if (updated is null)
                {
                    updated = new Dictionary<string, ITrackingPoint>(trackingPoints, StringComparer.Ordinal);
                }

                ITextSnapshotLine line = snapshot.GetLineFromLineNumber(lineIndex);
                updated[bookmark.BookmarkId] = CreateStableTrackingPoint(line);
            }

            if (updated is not null)
            {
                lock (_trackingPointsLock)
                {
                    _trackingPoints = updated;
                }
            }
        }

        /// <summary>
        /// Gets the current tracked line number for a bookmark, or null if not tracked.
        /// This provides real-time position information for the glyph tagger.
        /// </summary>
        public int? GetTrackedLineNumber(string bookmarkId)
        {
            Dictionary<string, ITrackingPoint> trackingPoints;
            lock (_trackingPointsLock)
            {
                trackingPoints = _trackingPoints;
            }

            if (!trackingPoints.TryGetValue(bookmarkId, out ITrackingPoint trackingPoint))
            {
                return null;
            }

            ITextSnapshot snapshot = _textBuffer.CurrentSnapshot;
            SnapshotPoint point = trackingPoint.GetPoint(snapshot);
            return point.GetContainingLine().LineNumber + 1;
        }

        private bool UpdateBookmarksFromTrackingPoints(List<BookmarkMetadata> metadata)
        {
            var documentBookmarks = metadata.Where(bookmark => MatchesDocumentPath(bookmark.DocumentPath)).ToList();
            if (documentBookmarks.Count == 0)
            {
                return false;
            }

            ITextSnapshot snapshot = _textBuffer.CurrentSnapshot;
            var changed = false;

            Dictionary<string, ITrackingPoint> trackingPoints;
            lock (_trackingPointsLock)
            {
                trackingPoints = _trackingPoints;
            }

            var newTrackingPoints = new Dictionary<string, ITrackingPoint>(trackingPoints, StringComparer.Ordinal);

            foreach (BookmarkMetadata bookmark in documentBookmarks)
            {
                int newLineNumber;
                string newLineText;

                if (trackingPoints.TryGetValue(bookmark.BookmarkId, out ITrackingPoint trackingPoint))
                {
                    // Use tracking point for accurate position
                    SnapshotPoint currentPoint = trackingPoint.GetPoint(snapshot);
                    ITextSnapshotLine line = currentPoint.GetContainingLine();
                    newLineNumber = line.LineNumber + 1;
                    newLineText = line.GetText();
                }
                else
                {
                    // No tracking point - create one from current bookmark position
                    var lineIndex = Clamp(bookmark.LineNumber - 1, 0, Math.Max(snapshot.LineCount - 1, 0));
                    ITextSnapshotLine line = snapshot.GetLineFromLineNumber(lineIndex);
                    newLineNumber = line.LineNumber + 1;
                    newLineText = line.GetText();

                    // Create tracking point for future edits
                    newTrackingPoints[bookmark.BookmarkId] = CreateStableTrackingPoint(line);
                }

                if (bookmark.LineNumber == newLineNumber
                    && string.Equals(bookmark.LineText, newLineText, StringComparison.Ordinal))
                {
                    continue;
                }

                var translatedBookmark = new BookmarkSnapshot
                {
                    DocumentPath = _documentPath,
                    LineNumber = newLineNumber,
                    LineText = newLineText,
                };

                bookmark.UpdateFromSnapshot(translatedBookmark);
                changed = true;
            }

            // Update tracking points if any were added
            if (newTrackingPoints.Count != trackingPoints.Count)
            {
                lock (_trackingPointsLock)
                {
                    _trackingPoints = newTrackingPoints;
                }
            }

            return changed;
        }

        private bool MatchesDocumentPath(string documentPath)
            => string.Equals(BookmarkIdentity.NormalizeDocumentPath(documentPath), _normalizedDocumentPath, StringComparison.Ordinal);

        /// <summary>
        /// Creates a tracking point anchored at a stable position within the line content,
        /// avoiding the ambiguous line-start boundary that can cause drift when edits occur
        /// near line breaks.
        /// </summary>
        internal static ITrackingPoint CreateStableTrackingPoint(ITextSnapshotLine line)
        {
            ITextSnapshot snapshot = line.Snapshot;
            int anchorOffset = GetFirstNonWhitespaceOffset(line.GetText());
            int anchorPosition = line.Start.Position + anchorOffset;

            // When the anchor is inside line content, use Positive so insertions
            // at the anchor push the point forward (keeping it with the content).
            // When the anchor is at line.Start (empty/whitespace-only line), use
            // Negative so insertions at the boundary don't push the point onto
            // the next line.
            PointTrackingMode mode = anchorOffset > 0
                ? PointTrackingMode.Positive
                : PointTrackingMode.Negative;

            return snapshot.CreateTrackingPoint(anchorPosition, mode);
        }

        /// <summary>
        /// Returns the offset of the first non-whitespace character in the text,
        /// or 0 if the text is empty or contains only whitespace.
        /// </summary>
        internal static int GetFirstNonWhitespaceOffset(string lineText)
        {
            if (string.IsNullOrEmpty(lineText))
            {
                return 0;
            }

            for (int i = 0; i < lineText.Length; i++)
            {
                if (!char.IsWhiteSpace(lineText[i]))
                {
                    return i;
                }
            }

            return 0;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum)
            {
                return minimum;
            }

            return value > maximum ? maximum : value;
        }
    }
}
