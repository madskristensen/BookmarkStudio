using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace BookmarkStudio
{
    [Export(typeof(IGlyphMouseProcessorProvider))]
    [Name("BookmarkStudioGlyphMouseProcessor")]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class BookmarkGlyphMouseProcessorProvider : IGlyphMouseProcessorProvider
    {
        public IMouseProcessor GetAssociatedMouseProcessor(IWpfTextViewHost wpfTextViewHost, IWpfTextViewMargin margin)
            => new BookmarkGlyphMouseProcessor(wpfTextViewHost, margin);
    }
}
