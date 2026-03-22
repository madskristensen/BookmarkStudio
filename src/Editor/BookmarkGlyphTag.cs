using Microsoft.VisualStudio.Text.Editor;

namespace BookmarkStudio
{
    internal sealed class BookmarkGlyphTag : IGlyphTag
    {
        public string BookmarkId { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public int? ShortcutNumber { get; set; }

        public BookmarkColor Color { get; set; }
    }
}
