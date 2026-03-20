# 🎯 Fix bookmark tree row alignment and inactive selection visuals

## Understanding
Adjust the Bookmark Manager tree row layout so bookmark color indicators start at the item indentation level directly before bookmark names, keep line and slot values consistently aligned under their headers, and use a subtle light gray inactive selection visual instead of the current blue/white treatment when the window is not focused.
## Assumptions
- The relevant UI is implemented in the WPF control for the Bookmark Manager tool window.
- Existing tree depth margin offsets are the main cause of inconsistent column alignment.
- A small view-model layout property rename or removal is acceptable if only used by this view.
## Approach
Update the tree item style and bookmark item template in [src/ToolWindows/BookmarkManagerControl.xaml](src/ToolWindows/BookmarkManagerControl.xaml) to place the color marker in the Name column immediately before text and to make non-name columns use a consistent depth compensation value that lines up with headers. Add explicit inactive-selection triggers to render a subtle light-gray translucent background and keep normal text color when selection is not active.

Refine depth-based margin properties in [src/ToolWindows/BookmarkManagerViewModel.cs](src/ToolWindows/BookmarkManagerViewModel.cs) so column offsets match the tree indentation model and no longer force color markers to a global left alignment. Add or update unit coverage in [test/BookmarkStudio.Test/BookmarkManagerViewModelTests.cs](test/BookmarkStudio.Test/BookmarkManagerViewModelTests.cs) for layout-related computed margins to prevent regressions.
## Key Files
- src/ToolWindows/BookmarkManagerControl.xaml - Tree row template and selection styling.
- src/ToolWindows/BookmarkManagerViewModel.cs - Computed margin properties used by row bindings.
- test/BookmarkStudio.Test/BookmarkManagerViewModelTests.cs - Validation for depth-based layout properties.
## Risks & Open Questions
- TreeView indentation may vary by theme/template, so depth compensation constants may need tuning after build-time verification.
- Inactive selection styling should remain readable across light and dark themes.

**Progress**: 100% [██████████]

**Last Updated**: 2026-03-20 21:34:02

## 📝 Plan Steps
- ✅ **Inspect existing style and margin constants for bookmark rows.**
- ✅ **Update TreeView item style triggers for inactive selection appearance.**
- ✅ **Rework bookmark row XAML so the color indicator sits directly before the name text at the item indentation start.**
- ✅ **Adjust view model margin properties to keep file, line text, line, and slot columns aligned to headers across depths.**
- ✅ **Add/adjust unit tests for depth-based layout margin behavior.**
- ✅ **Build and run unit tests to verify no regressions or warnings.**

