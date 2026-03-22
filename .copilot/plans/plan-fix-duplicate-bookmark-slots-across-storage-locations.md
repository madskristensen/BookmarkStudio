# 🎯 Fix Duplicate Bookmark Slots Across Storage Locations

## Understanding
The user has identified that both Personal (`.vs/.bookmarks.json`) and Workspace (`.bookmarks.json`) bookmark files can contain bookmarks with the same slot number (1-9), causing duplicates. When both locations have a bookmark assigned to the same slot, workspace bookmarks should take precedence, and the conflicting personal bookmark should become a non-slotted bookmark (slot = null).

## Assumptions
- Slot numbers are integers 1-9 (as seen in `FindNextAvailableSlot`)
- Workspace/Solution bookmarks have higher priority than Personal bookmarks
- The slot conflict should be resolved at load time, not at save time
- The slot clearing for personal bookmarks should only affect the in-memory representation, not persist the change back to the personal file

## Approach
The fix should be applied in `RefreshDualAsync` method in `BookmarkStudioSession.cs` (lines 426-462). After loading both states but before combining them into the cached list, we need to identify conflicting slots and clear the slot number from personal bookmarks that conflict with workspace bookmarks. The workspace slots collection should be computed first, and then personal bookmarks should have their slots cleared if they conflict. This should modify the metadata objects before calling `ToManagedBookmarks`.

## Key Files
- `src/Services/BookmarkStudioSession.cs` - Contains `RefreshDualAsync` where bookmarks are combined

## Risks & Open Questions
- Should the slot clearing be persisted back to the personal file, or just be in-memory? (Assuming in-memory only to avoid unexpected file changes)

**Progress**: 100% [██████████]

**Last Updated**: 2026-03-22 00:43:49

## 📝 Plan Steps
- ✅ **Add a helper method `ResolveSlotConflicts` to clear slot numbers from personal bookmarks that conflict with workspace bookmarks**
- ✅ **Call the new helper method in `RefreshDualAsync` after loading both states but before combining**
- ✅ **Build and verify the solution compiles without errors**
- ✅ **Run existing tests to ensure no regressions**

