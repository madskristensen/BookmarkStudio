# 🎯 Add Context Menu Commands for Storage Location

## Understanding
Add "Move to Solution" and "Move to Personal" context menu commands on the Root node in the Bookmark Manager, allowing users to move bookmarks between personal (.vs) and solution-level storage.

## Assumptions
- Context menu commands will appear only on the Root node
- Commands will be enabled/disabled based on current storage location
- Will reuse existing context menu infrastructure in BookmarkManagerControl.xaml

## Approach
Examine existing context menu patterns in the XAML and add new menu items that call the ViewModel's MoveToSolutionAsync/MoveToPersonalAsync methods. Commands will be visibility-bound to CanMoveToSolution/CanMoveToPersonal properties.

## Key Files
- `src/ToolWindows/BookmarkManagerControl.xaml` - Context menu definition
- `src/ToolWindows/BookmarkManagerControl.xaml.cs` - Event handlers

**Progress**: 100% [██████████]

**Last Updated**: 2026-03-20 23:46:04

## 📝 Plan Steps
- ✅ **Examine existing context menu structure in BookmarkManagerControl.xaml**
- ✅ **Add "Move to Solution" and "Move to Personal" menu items to the Root node context menu**
- ✅ **Add click handlers in BookmarkManagerControl.xaml.cs**
- ✅ **Build and verify**

