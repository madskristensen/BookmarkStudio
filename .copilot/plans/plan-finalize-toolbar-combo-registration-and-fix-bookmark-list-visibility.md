# 🎯 Finalize toolbar combo registration and fix bookmark list visibility

## Understanding
The tool window sort and group combos must be correctly defined in VSCT and tied to the tool window toolbar. There is also a behavior issue where the status reports bookmarks loaded but the list can appear empty or misleading.
## Assumptions
- The existing toolbar should remain on the tool window pane and combo commands should continue to route through command handlers.
- The current VSCT toolbar exists but should be aligned to the expected SDK toolbar declaration pattern.
- The list visibility issue is at least partly a state mismatch between loaded item count and visible filtered items.
## Approach
Normalize the VSCT toolbar definition for tool window combo controls, keep command ids in sync, and retain toolbar command registration in package initialization. Then adjust view-model status and selection behavior so the UI state is consistent with visible rows, and the first bookmark is selected when appropriate.

Finally, build and verify no new warnings were introduced.
## Key Files
- `BookmarkStudio/VSCommandTable.vsct` - toolbar and combo declarations.
- `BookmarkStudio/ToolWindows/BookmarkManagerViewModel.cs` - visible count/status and selection behavior.
- `BookmarkStudio/ToolWindows/BookmarkManagerToolWindow.cs` - toolbar registration validation context.
## Risks & Open Questions
- If the empty-list symptom is caused by an external data source issue, additional diagnostics may still be needed.
- Toolbar XML tweaks must preserve existing command placements.

**Progress**: 100% [██████████]

**Last Updated**: 2026-03-19 22:02:32

## 📝 Plan Steps
- ✅ **Normalize the VSCT toolbar and combo declarations.**
- ✅ **Improve bookmark list state handling in the view model.**
- ✅ **Verify toolbar registration path and combo command flow remains intact.**
- ✅ **Build and check warnings for regressions.**
- ✅ **Summarize changes and remaining caveats.**

