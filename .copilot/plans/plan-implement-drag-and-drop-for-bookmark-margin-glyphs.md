# 🎯 Implement Drag-and-Drop for Bookmark Margin Glyphs

## Understanding
The user wants to enable dragging bookmark glyphs in the editor margin to move them to different lines. When a glyph is dragged and dropped on another line, the bookmark's line number should be updated.

## Assumptions
- The glyph margin uses WPF and the existing mouse processor infrastructure
- BookmarkId is stored in the glyph tag and can be used to identify which bookmark to move
- The IWpfTextViewHost provides access to convert Y coordinates to line numbers
- Visual feedback during drag should show a preview indicator

## Approach
I'll extend the existing `BookmarkGlyphMouseProcessor` to handle left mouse button events for drag detection. The glyph's `Tag` property will store the `BookmarkId` for identification during drag. When the user releases the mouse on a different line, we'll call a new method in `BookmarkOperationsService` to update the bookmark's line number. I'll also add visual feedback using an adorner layer to show where the bookmark will be dropped.

## Key Files
- [BookmarkGlyphMouseProcessor.cs](src/Editor/BookmarkGlyphMouseProcessor.cs) - Add drag-and-drop handling
- [BookmarkGlyphFactory.cs](src/Editor/BookmarkGlyphFactory.cs) - Store BookmarkId in glyph Tag and set cursors
- [BookmarkOperationsService.cs](src/Services/BookmarkOperationsService.cs) - Add method to move bookmark to new line

## Risks & Open Questions
- Need to ensure drag only starts after a small movement threshold to avoid accidental drags
- Visual feedback may need testing to ensure it works with different VS themes

**Progress**: 100% [██████████]

**Last Updated**: 2026-03-21 19:33:32

## 📝 Plan Steps
- ✅ **Add MoveBookmarkToLineAsync method to BookmarkOperationsService**
- ✅ **Update BookmarkGlyphFactory to store BookmarkId in glyph Tag property and set grab cursor**
- ✅ **Implement drag-and-drop logic in BookmarkGlyphMouseProcessor with visual feedback**
- ✅ **Build and verify no compilation errors**

