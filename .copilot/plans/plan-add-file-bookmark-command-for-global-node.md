# 🎯 Add File Bookmark Command for Global Node

## Understanding
The user wants to add a new "Add File..." command to the context menu of the Global root node. This command should open a file picker, allow selecting any file from disk, and create a bookmark for it. The file name becomes the bookmark name, line number is 0, the absolute file path is stored, and the tooltip shows the absolute path (stored in LineText). No shortcut number should be assigned to file bookmarks.

## Assumptions
- The Global node context menu is defined in `BookmarkManagerControl.xaml` within the folder data template
- The IsGlobalRoot property on FolderNodeViewModel can be used to show/hide the menu item
- Bookmarks are added via the session and metadata store
- The lineText field in the JSON will store the absolute path for tooltip display
- Line number should be 0 for file-level bookmarks
- No shortcut number means ShortcutNumber = null
- Use AddDocument KnownMoniker for the icon

## Approach
1. Add a new menu item "Add File..." to the folder context menu in XAML, visible only when IsGlobalRoot is true
2. Add a click handler in BookmarkManagerControl.xaml.cs that opens a file dialog
3. Add a method in BookmarkManagerViewModel to add a file bookmark to the global storage
4. Implement the file bookmark creation logic with:
   - Label = file name
   - DocumentPath = absolute file path
   - LineNumber = 0
   - LineText = absolute file path (for tooltip)
   - ShortcutNumber = null
   - StorageLocation = Global

## Key Files
- `src/ToolWindows/BookmarkManagerControl.xaml` - Add the context menu item
- `src/ToolWindows/BookmarkManagerControl.xaml.cs` - Add click handler with file picker dialog
- `src/ToolWindows/BookmarkManagerViewModel.cs` - Add method to create file bookmark in global storage

## Risks & Open Questions
- None identified; the codebase already has patterns for adding bookmarks to specific storage locations

**Progress**: 100% [██████████]

**Last Updated**: 2026-03-23 18:32:18

## 📝 Plan Steps
- ✅ **Add "Add File..." menu item to folder context menu in XAML with AddDocument icon and visibility bound to IsGlobalRoot**
- ✅ **Add AddFileMenuItem_Click handler in BookmarkManagerControl.xaml.cs that opens Win32 OpenFileDialog**
- ✅ **Add AddFileBookmarkToGlobalAsync method in BookmarkManagerViewModel to create the bookmark**

