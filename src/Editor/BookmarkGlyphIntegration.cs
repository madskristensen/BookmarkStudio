using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace BookmarkStudio
{
    internal sealed class BookmarkGlyphTag : IGlyphTag
    {
        public string Label { get; set; } = string.Empty;

        public int? SlotNumber { get; set; }
    }

    [Export(typeof(IViewTaggerProvider))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [TagType(typeof(BookmarkGlyphTag))]
    internal sealed class BookmarkGlyphTaggerProvider : IViewTaggerProvider
    {
        [Import]
        internal ITextDocumentFactoryService TextDocumentFactoryService = null;

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer)
            where T : ITag
        {
            if (textView is null || buffer is null || textView.TextBuffer != buffer)
            {
                return null;
            }

            if (!TextDocumentFactoryService.TryGetTextDocument(buffer, out ITextDocument document))
            {
                return null;
            }

            return buffer.Properties.GetOrCreateSingletonProperty(() => new BookmarkGlyphTagger(buffer, document.FilePath)) as ITagger<T>;
        }
    }

    internal sealed class BookmarkGlyphTagger : ITagger<BookmarkGlyphTag>
    {
        private readonly ITextBuffer _buffer;
        private readonly string _documentPath;
        private readonly string _normalizedDocumentPath;

        public BookmarkGlyphTagger(ITextBuffer buffer, string documentPath)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
            _documentPath = documentPath ?? string.Empty;
            _normalizedDocumentPath = BookmarkIdentity.NormalizeDocumentPath(_documentPath);
            BookmarkStudioSession.Current.BookmarksChanged += OnBookmarksChanged;
        }

        public event EventHandler<SnapshotSpanEventArgs> TagsChanged;

        public IEnumerable<ITagSpan<BookmarkGlyphTag>> GetTags(NormalizedSnapshotSpanCollection spans)
        {
            if (spans is null || spans.Count == 0 || string.IsNullOrWhiteSpace(_normalizedDocumentPath))
            {
                yield break;
            }

            ITextSnapshot snapshot = spans[0].Snapshot;
            int startLine = spans[0].Start.GetContainingLine().LineNumber;
            int endLine = spans[spans.Count - 1].End.GetContainingLine().LineNumber;

            foreach (ManagedBookmark bookmark in BookmarkStudioSession.Current.CachedBookmarks.Where(MatchesDocumentPath))
            {
                int lineIndex = bookmark.LineNumber - 1;
                if (lineIndex < startLine || lineIndex > endLine || lineIndex < 0 || lineIndex >= snapshot.LineCount)
                {
                    continue;
                }

                ITextSnapshotLine line = snapshot.GetLineFromLineNumber(lineIndex);
                int spanLength = line.Length > 0 ? 1 : 0;
                SnapshotSpan span = new SnapshotSpan(line.Start, spanLength);
                BookmarkGlyphTag glyphTag = new BookmarkGlyphTag
                {
                    Label = bookmark.Label,
                    SlotNumber = bookmark.SlotNumber,
                };
                yield return new TagSpan<BookmarkGlyphTag>(span, glyphTag);
            }
        }

        private bool MatchesDocumentPath(ManagedBookmark bookmark)
            => string.Equals(BookmarkIdentity.NormalizeDocumentPath(bookmark.DocumentPath), _normalizedDocumentPath, StringComparison.Ordinal);

        private void OnBookmarksChanged(object sender, EventArgs e)
        {
            ITextSnapshot snapshot = _buffer.CurrentSnapshot;
            SnapshotSpan span = new SnapshotSpan(snapshot, 0, snapshot.Length);
            TagsChanged?.Invoke(this, new SnapshotSpanEventArgs(span));
        }
    }

    [Export(typeof(IGlyphFactoryProvider))]
    [Name("BookmarkStudioGlyph")]
    [Order(After = "VsTextMarker")]
    [ContentType("text")]
    [TagType(typeof(BookmarkGlyphTag))]
    internal sealed class BookmarkGlyphFactoryProvider : IGlyphFactoryProvider
    {
        public IGlyphFactory GetGlyphFactory(IWpfTextView view, IWpfTextViewMargin margin)
            => new BookmarkGlyphFactory();
    }

    internal sealed class BookmarkGlyphFactory : IGlyphFactory
    {
        public UIElement GenerateGlyph(IWpfTextViewLine line, IGlyphTag tag)
        {
            string tooltip = BuildTooltip(tag as BookmarkGlyphTag);

            Border glyph = new Border
            {
                Width = 10,
                Height = 10,
                Background = Brushes.Goldenrod,
                BorderBrush = Brushes.SaddleBrown,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                ToolTip = tooltip,
                Margin = new Thickness(0, 2, 0, 0),
            };

            return glyph;
        }

        private static string BuildTooltip(BookmarkGlyphTag bookmarkTag)
        {
            if (bookmarkTag is null)
            {
                return "BookmarkStudio bookmark";
            }

            bool hasLabel = !string.IsNullOrWhiteSpace(bookmarkTag.Label);
            bool hasSlot = bookmarkTag.SlotNumber.HasValue;

            if (hasLabel && hasSlot)
            {
                return string.Concat(bookmarkTag.Label, " [Slot ", bookmarkTag.SlotNumber.Value.ToString(), "]");
            }

            if (hasLabel)
            {
                return bookmarkTag.Label;
            }

            if (hasSlot)
            {
                return string.Concat("Slot ", bookmarkTag.SlotNumber.Value.ToString());
            }

            return "BookmarkStudio bookmark";
        }
    }
}
