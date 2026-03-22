# 🎯 Rename Slots to Shortcuts and Empty File Cleanup

## Understanding
The user wants to rename all occurrences of "slot(s)" to "shortcut(s)" across the codebase, and modify the persistence logic to delete the `.bookmark.json` file when all folders and bookmarks are removed instead of saving an empty file.

## Assumptions
- The rename should be case-preserving (Slot -> Shortcut, slot -> shortcut, SLOT -> SHORTCUT)
- Properties like `SlotNumber` will become `ShortcutNumber`
- Text mentions like "Assign Slot" become "Assign Shortcut"
- The VSCommandTable.cs is auto-generated but should be edited directly per instructions
- Empty state means: no bookmarks AND only the root folder path (empty string)

## Approach
I'll start by renaming all slot references in the core models, then propagate to services, commands, UI helpers, and finally the VSCT command table. For the empty file cleanup, I'll modify `BookmarkMetadataStore.SaveWorkspaceAsync` to check if the state is empty and delete the file instead of writing it.

Key files affected:
- `BookmarkModels.cs` - SlotNumber property in BookmarkMetadata and ManagedBookmark
- `BookmarkRepositoryService.cs` - FindNextAvailableSlot method and comments
- `BookmarkOperationsService.cs` - AssignSlot, ClearSlot, GoToSlot methods
- `GoToSlotCommands.cs` - File rename and class renames
- `BookmarkContextMenuHelper.cs` - CreateAssignSlotSubmenu, PopulateSlotHeaders
- `VSCommandTable.cs` - PackageIds for slot commands, menu names
- `VSCommandTable.vsct` - ButtonText and menu names for slots
- `BookmarkMetadataStore.cs` - SaveWorkspaceAsync for empty file deletion

## Key Files
- src/Services/BookmarkModels.cs - Core model with SlotNumber property
- src/Services/BookmarkRepositoryService.cs - Slot assignment logic
- src/Services/BookmarkOperationsService.cs - Slot operations API
- src/Services/BookmarkMetadataStore.cs - File persistence and empty state check
- src/Commands/GoToSlotCommands.cs - Commands to navigate by slot
- src/Helpers/BookmarkContextMenuHelper.cs - Context menu slot items
- src/VSCommandTable.vsct - Command definitions with Slot text

## Risks & Open Questions
- The JSON property name in persisted files still uses "SlotNumber" in the serialized data - changing that would be a breaking change, so keeping it as-is for backward compatibility

**Progress**: 87% [████████░░]

**Last Updated**: 2026-03-22 13:22:03

## 📝 Plan Steps
- ✅ **Rename SlotNumber to ShortcutNumber in BookmarkModels.cs (BookmarkMetadata and ManagedBookmark classes)**
- ✅ **Rename slot references in BookmarkRepositoryService.cs (FindNextAvailableSlot method and ToManagedBookmarks sorting)**
- ✅ **Rename slot references in BookmarkOperationsService.cs (AssignSlot, ClearSlot, GoToSlot methods)**
- ✅ **Rename GoToSlotCommands.cs file to GoToShortcutCommands.cs and update class names**
- ✅ **Rename slot references in BookmarkContextMenuHelper.cs (menu headers and method names)**
- ✅ **Rename slot references in VSCommandTable.cs (PackageIds)**
- ✅ **Rename slot references in VSCommandTable.vsct (ButtonText, CommandName, LocCanonicalName)**
- 🔄 **Add empty state detection and file deletion logic in BookmarkMetadataStore.SaveWorkspaceAsync**

