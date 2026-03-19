# 🎯 Remove Toolbar Commands: Navigate, Edit Label, Edit Group, Clear Group, Clear Slot, Copy Location, Group Combo

## Understanding
Remove 7 toolbar commands (Navigate to Bookmark, Edit Label, Edit Group, Clear Group, Clear Slot, Copy Bookmark Location, and the Group combo) from the Bookmark Manager toolbar. These features remain accessible via context menu. Only Refresh, Export, and Delete stay on the toolbar.

## Assumptions
- Context menu handlers in BookmarkManagerControl.xaml.cs are separate implementations and are NOT removed
- ViewModel methods used by context menu (NavigateSelectedAsync, ClearSelectedGroupAsync, ClearSelectedSlotAsync, etc.) are kept
- `GetSelectedBookmark()` on ToolWindow is only used by EditBookmarkLabelCommand and EditBookmarkGroupCommand (both removed)
- BookmarkManagerToolbarCombos.cs can be fully deleted (only had Group combo)
- Grouping feature (`SelectedGroupMode`, `GroupOptions`, `ApplyGrouping`) becomes unreachable after removing the combo

## Approach
Remove VSCT definitions, generated IDs, delete command files and the combos file, clean up package initialization, then remove bridge methods and ViewModel grouping code that are no longer reachable. Build to verify.

## Key Files
- `BookmarkStudio/VSCommandTable.vsct` - toolbar button/combo definitions
- `BookmarkStudio/VSCommandTable.cs` - generated command IDs
- `BookmarkStudio/Commands/*.cs` - 6 command class files to delete + BookmarkManagerToolbarCombos.cs
- `BookmarkStudio/BookmarkStudioPackage.cs` - combo initialization
- `BookmarkStudio/ToolWindows/BookmarkManagerToolWindow.cs` - bridge methods
- `BookmarkStudio/ToolWindows/BookmarkManagerControl.xaml.cs` - control properties
- `BookmarkStudio/ToolWindows/BookmarkManagerViewModel.cs` - grouping state/logic

## Risks & Open Questions
- None; all removed functionality remains available via context menu

**Progress**: 100% [██████████]

**Last Updated**: 2026-03-19 23:39:47

## 📝 Plan Steps
- ✅ **Remove 7 button definitions, the Group combo, and their IDSymbols from VSCommandTable.vsct**
- ✅ **Remove corresponding command IDs from VSCommandTable.cs**
- ✅ **Delete the 6 command class files and BookmarkManagerToolbarCombos.cs**
- ✅ **Remove BookmarkManagerToolbarCombos.InitializeAsync call from BookmarkStudioPackage.cs**
- ✅ **Remove GetSelectedBookmark, GetGroupOptions, GetSelectedGroupMode, SetSelectedGroupMode from BookmarkManagerToolWindow.cs**
- ✅ **Remove GroupOptions, SelectedGroupMode, SelectedBookmark from BookmarkManagerControl.xaml.cs**
- ✅ **Remove GroupOptions, SelectedGroupMode, ApplyGrouping from BookmarkManagerViewModel.cs**
- ✅ **Build and verify no errors**

