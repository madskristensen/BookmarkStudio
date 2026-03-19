# 🎯 Move tool window sort and group combos into the toolbar

## Understanding
The sort and group selectors currently live inside the Bookmark Manager WPF control. They need to be removed from the content area and surfaced as actual controls on the tool window toolbar while preserving the existing view model behavior.
## Assumptions
- The existing tool window toolbar already hosts commands and is the correct place for the new selectors.
- The sort and group state should remain owned by `BookmarkManagerViewModel` so existing grouping and sorting logic stays unchanged.
- The project uses standard Visual Studio command routing for toolbar combo boxes, so combo definitions and command handlers can be added without introducing new dependencies.
## Approach
Add two toolbar combo box commands to the VSCT and command id constants, then register command handlers that expose the available sort and group options from `BookmarkManagerViewModel` and update the active control when the user changes selections. The tool window class will provide safe accessors that allow command handlers to read and write the active view model state without directly reaching into WPF bindings.

Then remove the bottom-row sort/group combo UI from `BookmarkManagerControl.xaml` and adjust the remaining layout so the selected bookmark details still render cleanly. After the edits, build and run relevant tests if any exist.
## Key Files
- `BookmarkStudio/ToolWindows/BookmarkManagerControl.xaml` - removes the embedded sort/group selectors from the content UI.
- `BookmarkStudio/ToolWindows/BookmarkManagerControl.xaml.cs` - exposes view model-backed sort/group access needed by toolbar commands.
- `BookmarkStudio/ToolWindows/BookmarkManagerToolWindow.cs` - provides active-control access for toolbar command handlers.
- `BookmarkStudio/Commands/BookmarkCommands.cs` - registers and implements toolbar combo command handlers.
- `BookmarkStudio/VSCommandTable.cs` - adds command ids for the toolbar combo boxes and their list commands.
- `BookmarkStudio/VSCommandTable.vsct` - declares the toolbar combo controls.
## Risks & Open Questions
- Visual Studio toolbar combo boxes require correct command argument handling for both current text and item lists; if the command routing differs from the expected SDK pattern, registration may need adjustment.
- If there is no active tool window control when the toolbar queries status, the handlers must degrade safely.

**Progress**: 100% [██████████]

**Last Updated**: 2026-03-19 21:54:22

## 📝 Plan Steps
- ✅ **Add toolbar combo command definitions.**
- ✅ **Expose sort and group state through the tool window and control layers.**
- ✅ **Implement toolbar combo command handlers.**
- ✅ **Remove the in-content combo boxes and tidy the layout.**
- ✅ **Build and validate the extension.**

