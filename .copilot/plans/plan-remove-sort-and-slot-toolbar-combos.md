# 🎯 Remove Sort and Slot Toolbar Combos

## Understanding
Remove the Sort and Slot dropdown combos from the Bookmark Manager toolbar. Sorting is now handled via column header clicks. The Group combo remains.

## Assumptions
- The existing column-header sorting in `SortByColumn` provides all needed sort functionality
- The default sort (Slot > File > Line) should remain when no column header is clicked
- The Group combo stays intact
- ViewModel's `SelectedSlotNumber` and `AssignSelectedSlotAsync` remain because the context menu still uses them
- `SortOptions`, `SelectedSortMode`, and sort-combo-only fields can be fully removed from the ViewModel
- `SlotOptions` and `SelectedSlot` on the Control can be removed (only used by the toolbar combo)

## Approach
Remove definitions bottom-up: VSCT command table, generated CS IDs, toolbar combo handlers, tool window bridge methods, control properties, and finally ViewModel members. Simplify `ApplySort` to use the default Slot sort when no column sort is active.

## Key Files
- `BookmarkStudio/VSCommandTable.vsct` - combo definitions and IDSymbols
- `BookmarkStudio/VSCommandTable.cs` - generated command IDs
- `BookmarkStudio/Commands/BookmarkManagerToolbarCombos.cs` - combo command handlers
- `BookmarkStudio/ToolWindows/BookmarkManagerToolWindow.cs` - static bridge methods
- `BookmarkStudio/ToolWindows/BookmarkManagerControl.xaml.cs` - control properties
- `BookmarkStudio/ToolWindows/BookmarkManagerViewModel.cs` - sort mode state and logic

## Risks & Open Questions
- None significant; the Group combo is left untouched

**Progress**: 100% [██████████]

**Last Updated**: 2026-03-19 23:22:54

## 📝 Plan Steps
- ✅ **Remove Sort and Slot combo definitions and IDSymbols from VSCommandTable.vsct**
- ✅ **Remove Sort and Slot command IDs from VSCommandTable.cs**
- ✅ **Remove Sort and Slot handlers from BookmarkManagerToolbarCombos.cs**
- ✅ **Remove Sort and Slot bridge methods from BookmarkManagerToolWindow.cs**
- ✅ **Remove Sort and Slot properties from BookmarkManagerControl.xaml.cs**
- ✅ **Remove SortOptions, SelectedSortMode, and simplify ApplySort in BookmarkManagerViewModel.cs**
- ✅ **Build and verify no errors**

