# 🎯 Implement Bookmark Cleanup for Deleted/Moved/Renamed Files

## Understanding
The user wants a comprehensive cleanup system for orphaned bookmarks - bookmarks pointing to files that no longer exist. This includes: (1) lazy cleanup when loading bookmarks, (2) navigation-time cleanup with user prompt, (3) manual cleanup via the refresh button, and (4) real-time file event monitoring for deletions, moves, and renames inside VS.

## Assumptions
- File existence checks should use `System.IO.File.Exists` with the bookmark's `DocumentPath`
- The `BookmarkRefreshMonitorService` is a stub service that can be expanded for file event monitoring
- `IVsTrackProjectDocumentsEvents2` is the appropriate VS interface for monitoring file operations
- Navigation-time cleanup should prompt the user before deleting stale bookmarks
- File renames/moves should update bookmark paths rather than delete them

## Approach
The implementation will be layered:
1. Add a `RemoveStaleBookmarksAsync` method to [BookmarkStudioSession](src/Services/BookmarkStudioSession.cs) that checks file existence and removes invalid bookmarks
2. Modify `RefreshAsync` to optionally run cleanup (lazy cleanup)
3. Expand [BookmarkRefreshMonitorService](src/Services/BookmarkRefreshMonitorService.cs) to implement `IVsTrackProjectDocumentsEvents2` for real-time file event monitoring
4. Update [BookmarkOperationsService](src/Services/BookmarkOperationsService.cs) `NavigateToBookmarkAsync` to check file existence and prompt for deletion on failure
5. The refresh command already calls `RefreshAsync`, so cleanup will be triggered automatically

## Key Files
- src/Services/BookmarkStudioSession.cs - Add cleanup logic and integrate with refresh
- src/Services/BookmarkRefreshMonitorService.cs - Implement file event monitoring
- src/Services/BookmarkOperationsService.cs - Add navigation-time file validation
- src/BookmarkStudioPackage.cs - Wire up the file event monitor

## Risks & Open Questions
- Large solutions with many bookmarks could have slow refresh if checking file existence for each; mitigate by doing cleanup asynchronously
- File renames outside of VS won't be detected until next refresh
- User may want to keep bookmarks for files that are temporarily deleted (e.g., branch switching); the prompt on navigation handles this

**Progress**: 100% [██████████]

**Last Updated**: 2026-03-21 21:08:39

## 📝 Plan Steps
- ✅ **Add RemoveStaleBookmarksAsync method to BookmarkStudioSession**
- ✅ **Update RefreshAsync in BookmarkStudioSession to call cleanup**
- ✅ **Add file existence check and user prompt in NavigateToBookmarkAsync**
- ✅ **Implement IVsTrackProjectDocumentsEvents2 in BookmarkRefreshMonitorService**
- ✅ **Wire up BookmarkRefreshMonitorService in BookmarkStudioPackage**
- ✅ **Update RefreshBookmarkManagerCommand to show cleanup results**
- ✅ **Build and verify no compilation errors**

