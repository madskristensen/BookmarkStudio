# 🎯 Remove details panel and move actions to toolbar

## Understanding
The user wants the bookmark manager tool window to look like the VS breakpoint window - a full-height grid with actions in the toolbar, no details/editing panel beneath. Actions like delete, navigate, edit label, edit group, assign/clear slot, and clear group should move to toolbar buttons/combos.

## Assumptions
- The existing TextPromptWindow dialog can be reused for editing label and group text via toolbar buttons
- Slot assignment works well as a dropdown combo in the toolbar (matching the existing sort/group combo pattern)
- The context menu on the ListView should be kept for quick access
- Double-click to navigate remains unchanged
- The status text can move to the VS status bar instead of an in-grid text block

## Approach
Add new VSCT button/combo entries for: Navigate, Delete, Edit Label, Edit Group, Clear Group, Assign Slot (combo), Clear Slot. Create corresponding command classes following the existing `BookmarkCommandBase` pattern. The Edit Label/Group commands will use `TextPromptWindow.Show()` to prompt for input. The Assign Slot command will use a combo like sort/group. Then strip the XAML down to just the ListView filling the entire space, and clean up the code-behind to remove the now-unnecessary button click handlers.

## Key Files
- `BookmarkStudio\VSCommandTable.vsct` - add new buttons, combo, and IDSymbols
- `BookmarkStudio\VSCommandTable.cs` - add new PackageIds constants
- `BookmarkStudio\ToolWindows\BookmarkManagerControl.xaml` - strip to just ListView
- `BookmarkStudio\ToolWindows\BookmarkManagerControl.xaml.cs` - remove button handlers
- `BookmarkStudio\Commands\BookmarkManagerToolbarCombos.cs` - add slot combo handlers
- New command files for Navigate, Delete, EditLabel, EditGroup, ClearGroup, ClearSlot

## Risks & Open Questions
- The VSCT file is order-sensitive for priorities; new buttons need appropriate priority values
- The csproj may need manual terminal edits since IDE blocks direct edits

**Progress**: 100% [██████████]

**Last Updated**: 2026-03-19 22:42:51

## 📝 Plan Steps
- ✅ **Update VSCommandTable.vsct to add new toolbar buttons (Navigate, Delete, Edit Label, Edit Group, Clear Group, Clear Slot) and the Assign Slot combo with IDSymbols**
- ✅ **Update VSCommandTable.cs to add new PackageIds constants for all new commands**
- ✅ **Create NavigateToBookmarkCommand.cs**
- ✅ **Create DeleteBookmarkCommand.cs**
- ✅ **Create EditBookmarkLabelCommand.cs using TextPromptWindow**
- ✅ **Create EditBookmarkGroupCommand.cs using TextPromptWindow**
- ✅ **Create ClearBookmarkGroupCommand.cs**
- ✅ **Create ClearBookmarkSlotCommand.cs**
- ✅ **Add Assign Slot combo handlers to BookmarkManagerToolbarCombos.cs**
- ✅ **Simplify BookmarkManagerControl.xaml to just a full-height ListView**
- ✅ **Clean up BookmarkManagerControl.xaml.cs to remove button handlers**
- ✅ **Add new command files to the csproj and verify build**

