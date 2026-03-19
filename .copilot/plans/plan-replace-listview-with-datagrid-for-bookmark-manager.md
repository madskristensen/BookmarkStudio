# 🎯 Replace ListView with DataGrid for Bookmark Manager

## Understanding
Replace the ListView/GridView control with a DataGrid to get proper column headers with built-in sorting support, themed correctly for VS. Follow the pattern from CommentsVS's CodeAnchorsControl.xaml.

## Assumptions
- The DataGrid's built-in column sorting (via `Sorting` event) replaces the manual `ColumnHeader_Click`/`UpdateSortIndicator`/`ClearSortIndicator` approach
- The existing ViewModel sorting logic (`SortByColumn`) is reused - we just hook it from the DataGrid's `Sorting` event instead
- GroupStyle is dropped since DataGrid doesn't natively support grouping in the same way - the toolbar group combo still works via the ViewModel's `ICollectionView`
- The context menu and all menu item click handlers remain unchanged

## Approach
Replace the entire ListView block in the XAML with a DataGrid following the CommentsVS pattern: styles for DataGridCell, DataGridRow, and DataGridColumnHeader in UserControl.Resources, then DataGridTextColumn definitions with ElementStyle for each column. In the code-behind, remove all GridViewColumnHeader-specific fields and methods (`_originalHeaders`, `_lastSortedHeader`, `ColumnHeader_Click`, `GetSortProperty`, `UpdateSortIndicator`, `ClearSortIndicator`), and add a `Sorting` event handler that delegates to `_viewModel.SortByColumn()`. The ViewModel property change listener for `ColumnSortProperty` is simplified since the DataGrid manages its own sort glyphs.

## Key Files
- BookmarkStudio\ToolWindows\BookmarkManagerControl.xaml - replace ListView with DataGrid
- BookmarkStudio\ToolWindows\BookmarkManagerControl.xaml.cs - remove ListView-specific code, add DataGrid sorting handler

## Risks & Open Questions
- DataGrid grouping display differs from ListView GroupStyle - groups will still apply via ICollectionView but won't have visual group headers
- The DataGrid's built-in sort direction glyph should replace the manual arrow indicator

**Progress**: 100% [██████████]

**Last Updated**: 2026-03-19 23:14:29

## 📝 Plan Steps
- ✅ **Replace the entire XAML content to use DataGrid with VS-themed styles following CommentsVS pattern**
- ✅ **Update code-behind to remove ListView/GridView-specific fields and methods, add DataGrid Sorting handler**
- ✅ **Build and verify compilation**

