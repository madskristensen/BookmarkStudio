# 🎯 Remove Move Commands and Update Delete/Clear Functionality

## Understanding
Remove "Move to Solution" and "Move to Personal" context menu items from root nodes (Personal and Workspace). Add a "Clear Bookmarks" command to root node context menus that triggers on Delete key with a confirmation dialog. Remove both "Delete Bookmark" and "Clear All Bookmarks" commands from the tool window toolbar.

## Assumptions
- The XAML file defines the folder node context menu template
- The BookmarkManagerControl.xaml.cs contains event handlers for menu items
- The VSCommandTable.vsct defines toolbar buttons and groups
- VSCommandTable.cs is auto-generated and should be edited directly
- Root nodes are identified by the IsRoot property
- A confirmation dialog should appear before clearing bookmarks from root nodes

## Approach
First, I'll modify the XAML to remove "Move to..." menu items and add "Clear Bookmarks" for root nodes. Then I'll add the corresponding event handler with confirmation dialog logic. Next, I'll remove the toolbar button definitions from VSCommandTable.vsct. I'll update the Delete key handler to work with root nodes for clearing. Finally, I'll update VSCommandTable.cs to reflect the removed commands.

## Key Files
- src/ToolWindows/BookmarkManagerControl.xaml - Context menu definitions
- src/ToolWindows/BookmarkManagerControl.xaml.cs - Event handlers and key binding logic
- src/ToolWindows/BookmarkManagerViewModel.cs - ViewModel methods for clearing bookmarks
- src/VSCommandTable.vsct - Toolbar button definitions
- src/VSCommandTable.cs - Generated command IDs (edit directly)

## Risks & Open Questions
- Need to ensure confirmation dialog works properly for root node deletion
- Need to verify the Clear operation clears only the bookmarks in that storage location (Personal or Solution)
- May need to add a ViewModel method for clearing bookmarks by storage location

**Progress**: 16% [█░░░░░░░░░]

**Last Updated**: 2026-03-21 18:28:35

## 📝 Plan Steps
- ✅ **Modify BookmarkManagerControl.xaml to remove Move commands and add Clear Bookmarks command**
- 🔄 **Add ClearBookmarksMenuItem_Click event handler with confirmation dialog**
-  **Update PreviewKeyDown handler to support Delete key on root nodes with confirmation**
-  **Add ViewModel method to clear bookmarks for a specific storage location if needed**
-  **Remove DeleteBookmarkCommand and ClearAllBookmarksCommand buttons from VSCommandTable.vsct**
-  **Update VSCommandTable.cs to remove the command IDs for toolbar buttons**

