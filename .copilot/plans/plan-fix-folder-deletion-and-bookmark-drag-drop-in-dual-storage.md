# 🎯 Fix Folder Deletion and Bookmark Drag-Drop in Dual Storage

## Understanding
The user reports that deleting folders and dragging bookmarks between folders don't work. This is because the dual-storage tree (Personal/Solution) requires knowing which storage location a folder belongs to, but child folders don't track their storage location. The operations service methods use `UpdateWorkspaceAsync` which operates on a single workspace, not the dual storage system.

## Assumptions
- Child `FolderNodeViewModel` instances don't have `StorageLocation` set (only root nodes do)
- The operations service needs to know which storage (Personal/Solution) to update
- Bookmarks already have `StorageLocation` property set correctly
- We need to propagate storage location to child folders OR determine it at operation time

## Approach
The fix requires two changes:
1. Propagate `StorageLocation` from root nodes to all child `FolderNodeViewModel` instances during tree building
2. Update the operations to use dual-storage-aware methods that operate on the correct workspace based on the bookmark/folder's storage location

For folder operations, we need to add new session/metadata methods that can update a specific storage location's workspace. For bookmark move within same storage, we need to pass the storage location to the operation.

## Key Files
- src/ToolWindows/BookmarkManagerViewModel.cs - BuildFolderNode needs to propagate StorageLocation
- src/ToolWindows/BookmarkManagerViewModel.cs - DeleteSelectedAsync and MoveSelectedBookmarkToFolderAsync need storage-aware calls
- src/Services/BookmarkOperationsService.cs - Add storage-location-aware folder/bookmark operations
- src/Services/BookmarkMetadataStore.cs - Add method to update specific storage location workspace
- src/Services/BookmarkStudioSession.cs - Add method to update specific storage location

## Risks & Open Questions
- Need to ensure backward compatibility with single-storage operations
- The folder move operation (MoveFolderAsync) also needs the same fix

**Progress**: 100% [██████████]

**Last Updated**: 2026-03-21 15:36:40

## 📝 Plan Steps
- ✅ **Add StorageLocation parameter to FolderNodeViewModel constructor and propagate in BuildFolderNode**
- ✅ **Add UpdateWorkspaceAtLocationAsync method to BookmarkMetadataStore**
- ✅ **Add UpdateWorkspaceAtLocationAsync method to BookmarkStudioSession**
- ✅ **Add storage-location-aware DeleteFolderRecursiveAsync overload to BookmarkOperationsService**
- ✅ **Add storage-location-aware MoveBookmarkToFolderAsync overload to BookmarkOperationsService**
- ✅ **Add storage-location-aware MoveFolderAsync overload to BookmarkOperationsService**
- ✅ **Update ViewModel DeleteSelectedAsync to pass storage location**
- ✅ **Update ViewModel MoveSelectedBookmarkToFolderAsync to pass storage location**
- ✅ **Update ViewModel MoveFolderAsync to pass storage location**
- ✅ **Build and run tests to verify fixes**

