# 🎯 Selection Background and Expanded Folder Persistence

## Understanding
The user wants two changes:
1. The selection highlight in the TreeView should extend all the way to the left edge, not just from the content column
2. Remember which folder nodes are expanded and restore that state when the solution is loaded

## Assumptions
- The expanded state should be persisted alongside bookmarks in the same JSON file
- Only folder nodes need expansion tracking (bookmark items don't have children)
- The BookmarkWorkspaceState class is the right place to store expanded folders

## Approach
For the selection background, the current template has a 2-column Grid where the selection Border (`Bd`) only spans column 1. I'll add a separate background Border that spans both columns and is controlled by the selection triggers.

For expanded folder persistence:
1. Add `ExpandedFolders` collection to `BookmarkWorkspaceState`
2. Update `BookmarkMetadataStore` to serialize/deserialize expanded folders
3. Add `IsExpanded` property to `FolderNodeViewModel` that can be bound
4. Wire up the expansion state when building the tree and save it on changes

## Key Files
- `src/ToolWindows/BookmarkManagerControl.xaml` - TreeViewItem template for selection background
- `src/Services/BookmarkModels.cs` - BookmarkWorkspaceState class
- `src/Services/BookmarkMetadataStore.cs` - JSON serialization
- `src/ToolWindows/BookmarkManagerViewModel.cs` - FolderNodeViewModel expansion binding
- `src/Services/BookmarkRepositoryService.cs` - Loading/saving workspace state

## Risks & Open Questions
- Need to ensure expansion state is saved when folders are expanded/collapsed

**Progress**: 100% [██████████]

**Last Updated**: 2026-03-20 21:48:04

## 📝 Plan Steps
- ✅ **Modify TreeViewItem template to extend selection background across both columns**
- ✅ **Add ExpandedFolders property to BookmarkWorkspaceState**
- ✅ **Update BookmarkMetadataStore to serialize expanded folders in JSON**
- ✅ **Update BookmarkMetadataStore to deserialize expanded folders from JSON**
- ✅ **Add IsExpanded property to FolderNodeViewModel with change notification**
- ✅ **Update BookmarkManagerViewModel to apply expanded state when building tree**
- ✅ **Wire up expansion change event to persist state**

