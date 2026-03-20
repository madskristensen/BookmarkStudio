# 🎯 Implement Bookmark Storage Location Feature

## Understanding
Add the ability to move bookmarks between personal (`.vs` folder) and solution-level (repo root) storage, with visual indicators showing the current location in the Root node and status bar.

## Assumptions
- File will be renamed from `bookmarks.json` to `.bookmarks.json`
- Personal location: `.vs/.bookmarks.json`
- Solution location: `.bookmarks.json` at repo/solution root
- Solution file takes precedence if both exist (move overwrites)
- Root node displays "Root (personal)" or "Root (solution)"
- Status bar shows relative path like `.vs/.bookmarks.json` or `.bookmarks.json`

## Approach
1. Update `BookmarkMetadataStore` to use `.bookmarks.json` naming and add location awareness
2. Add storage location enum and methods to `BookmarkOperationsService`
3. Update `FolderNodeViewModel` to show location in Root node display
4. Update status text in ViewModel to show storage path
5. Add context menu commands for moving between locations

## Key Files
- `src/Services/BookmarkMetadataStore.cs` - Storage path logic and move operations
- `src/Services/BookmarkOperationsService.cs` - Public API for location operations
- `src/ToolWindows/BookmarkManagerViewModel.cs` - Root node display and status text
- `src/VSCommandTable.vsct` - New menu commands (if needed)

## Risks & Open Questions
- Migration: existing `bookmarks.json` files need to be found (backwards compatibility)
- What if user doesn't have write access to repo root?

**Progress**: 100% [██████████]

**Last Updated**: 2026-03-20 23:43:14

## 📝 Plan Steps
- ✅ **Add BookmarkStorageLocation enum and location detection to BookmarkMetadataStore**
- ✅ **Update GetStoragePath to use `.bookmarks.json` with backwards compatibility for old name**
- ✅ **Add MoveToLocation method in BookmarkMetadataStore to move file between locations**
- ✅ **Add GetStorageLocation and MoveBookmarksAsync methods to BookmarkOperationsService**
- ✅ **Update FolderNodeViewModel to accept and display storage location in Root node name**
- ✅ **Update BookmarkManagerViewModel to pass location info to Root node and show path in status**
- ✅ **Build and verify no new warnings introduced**

