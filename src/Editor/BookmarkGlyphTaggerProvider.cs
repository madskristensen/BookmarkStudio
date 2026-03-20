using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.Utilities;

namespace BookmarkStudio
{
    [Export(typeof(IViewTaggerProvider))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    [TagType(typeof(BookmarkGlyphTag))]
    internal sealed class BookmarkGlyphTaggerProvider : IViewTaggerProvider
    {
        [Import]
        internal ITextDocumentFactoryService TextDocumentFactoryService = null!;

        public ITagger<T> CreateTagger<T>(ITextView textView, ITextBuffer buffer)
            where T : ITag
        {
            if (textView is null || buffer is null || textView.TextBuffer != buffer)
            {
                return null!;
            }

            if (!TextDocumentFactoryService.TryGetTextDocument(buffer, out ITextDocument document))
            {
                return null!;
            }

            BookmarkGlyphTagger tagger = buffer.Properties.GetOrCreateSingletonProperty(() => new BookmarkGlyphTagger(buffer, document.FilePath));
            tagger.AttachToView(textView);
            return tagger as ITagger<T> ?? null!;
        }
    }
}
