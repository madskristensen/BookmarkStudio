using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace BookmarkStudio
{
    /// <summary>
    /// Provides overview mark taggers for bookmarks in the vertical scrollbar.
    /// </summary>
    [Export(typeof(IViewTaggerProvider))]
    [TagType(typeof(OverviewMarkTag))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class BookmarkOverviewTaggerProvider : IViewTaggerProvider
    {
        [Import]
        internal ITextDocumentFactoryService TextDocumentFactoryService = null!;

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer) where T : ITag
        {
            if (textView is null || buffer is null || textView.TextBuffer != buffer)
            {
                return null!;
            }

            if (!TextDocumentFactoryService.TryGetTextDocument(buffer, out ITextDocument document))
            {
                return null!;
            }

            BookmarkOverviewTagger tagger = buffer.Properties.GetOrCreateSingletonProperty(
                typeof(BookmarkOverviewTagger),
                () => new BookmarkOverviewTagger(buffer, document.FilePath));

            tagger.AttachToView(textView);
            return tagger as ITagger<T> ?? null!;
        }
    }

    /// <summary>
    /// Creates overview marks (scrollbar markers) for bookmarks.
    /// </summary>
    internal sealed class BookmarkOverviewTagger : ITagger<OverviewMarkTag>, IDisposable
    {
        private readonly ITextBuffer _buffer;
        private readonly string _documentPath;
        private readonly string _normalizedDocumentPath;
        private int _attachedViewCount;
        private int _isDisposed;
        private volatile IReadOnlyList<ManagedBookmark> _cachedDocumentBookmarks = Array.Empty<ManagedBookmark>();

        public BookmarkOverviewTagger(ITextBuffer buffer, string documentPath)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _documentPath = documentPath ?? string.Empty;
            _normalizedDocumentPath = BookmarkIdentity.NormalizeDocumentPath(_documentPath);
            RefreshCachedBookmarks();
            BookmarkStudioSession.Current.BookmarksChanged += OnBookmarksChanged;
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

        public IEnumerable<ITagSpan<OverviewMarkTag>> GetTags(NormalizedSnapshotSpanCollection spans)
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
            foreach (ManagedBookmark bookmark in documentBookmarks)
            {
                int lineIndex = bookmark.LineNumber - 1;
                if (lineIndex < startLine || lineIndex > endLine || lineIndex < 0 || lineIndex >= snapshot.LineCount)
                {
                    continue;
                }

                ITextSnapshotLine line = snapshot.GetLineFromLineNumber(lineIndex);
                int spanLength = line.Length > 0 ? 1 : 0;
                SnapshotSpan span = new SnapshotSpan(line.Start, spanLength);

                string formatName = BookmarkOverviewMarkFormatNames.GetFormatName(bookmark.Color);
                yield return new TagSpan<OverviewMarkTag>(span, new OverviewMarkTag(formatName));
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) == 1)
            {
                return;
            }

            BookmarkStudioSession.Current.BookmarksChanged -= OnBookmarksChanged;
        }

        private bool MatchesDocumentPath(ManagedBookmark bookmark)
            => string.Equals(BookmarkIdentity.NormalizeDocumentPath(bookmark.DocumentPath), _normalizedDocumentPath, StringComparison.Ordinal);

        private void RefreshCachedBookmarks()
        {
            _cachedDocumentBookmarks = BookmarkStudioSession.Current.CachedBookmarks
                .Where(MatchesDocumentPath)
                .ToArray();
        }

        private void OnBookmarksChanged(object sender, EventArgs e)
        {
            if (Volatile.Read(ref _isDisposed) == 1)
            {
                return;
            }

            RefreshCachedBookmarks();

            ITextSnapshot snapshot = _buffer.CurrentSnapshot;
            SnapshotSpan fullSpan = new SnapshotSpan(snapshot, 0, snapshot.Length);
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(fullSpan));
        }
    }
}
