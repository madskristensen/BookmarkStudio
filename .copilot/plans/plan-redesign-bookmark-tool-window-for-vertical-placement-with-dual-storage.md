# 🎯 Redesign Bookmark Tool Window for Vertical Placement with Dual Storage

## Understanding
The user wants to redesign the Bookmark Studio tool window to be optimized for vertical placement (like Solution Explorer) instead of horizontal grid-based layout. The new design should show both Personal (.vs/.bookmarks.json) and Solution (.bookmarks.json) as separate root nodes in the tree, allowing users to have bookmarks in both locations and drag items between them.

## Assumptions
- The current system already supports Personal and Solution storage locations, but only one is active at a time
- The AzureExplorer pattern with full-width selection and TreeViewItemToIndentConverter should be followed
- Drag-and-drop should work for moving bookmarks and folders between Personal and Solution storage
- Both storage files should be loaded and displayed simultaneously
- Column headers (Name, File, Line Text, Line, Slot) should be removed for vertical optimization

## Approach
1. Create a TreeViewItemToIndentConverter for full-width selection with proper indentation
2. Completely redesign the XAML to use a simplified tree view without column headers, using the AzureExplorer pattern for full-width selection
3. Modify the ViewModel to support dual storage with separate root nodes for Personal and Solution
4. Update BookmarkMetadataStore to load/save both storage locations independently
5. Update BookmarkOperationsService to support operations on specific storage locations
6. Modify drag-and-drop to support moving items between storage locations (which is a move + location change)
7. Update data templates to show only the bookmark name and a simple indicator

## Key Files
- src/ToolWindows/BookmarkManagerControl.xaml - Main UI, needs complete redesign
- src/ToolWindows/BookmarkManagerControl.xaml.cs - Code-behind for drag-drop between storages
- src/ToolWindows/BookmarkManagerViewModel.cs - ViewModel needs dual-root support
- src/Services/BookmarkMetadataStore.cs - Storage service needs dual-load capability
- src/Services/BookmarkOperationsService.cs - Operations need storage-aware methods
- src/ToolWindows/TreeViewItemToIndentConverter.cs - New file for indent calculation

## Risks & Open Questions
- Performance impact of loading two storage files instead of one
- Conflict resolution if same bookmark exists in both locations
- How to handle expansion state for dual root nodes

**Progress**: 87% [████████░░]

**Last Updated**: 2026-03-21 14:48:07

## 📝 Plan Steps
- ✅ **Create TreeViewItemToIndentConverter helper class for full-width selection indentation**
- ✅ **Update BookmarkModels to add StorageLocation property to ManagedBookmark**
- ✅ **Update BookmarkMetadataStore to support loading both Personal and Solution storage independently**
- ✅ **Update BookmarkOperationsService to support dual storage operations and moving between locations**
- ✅ **Update BookmarkManagerViewModel to support dual root nodes (Personal and Solution) and storage-aware operations**
- ✅ **Redesign BookmarkManagerControl.xaml with simplified vertical tree view using full-width selection pattern**
- ✅ **Update BookmarkManagerControl.xaml.cs to handle drag-drop between storage locations**
- 🔄 **Build and verify compilation**

