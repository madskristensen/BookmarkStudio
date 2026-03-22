[marketplace]: <https://marketplace.visualstudio.com/items?itemName=MadsKristensen.BookmarkStudio>
[vsixgallery]: <https://www.vsixgallery.com/extension/BookmarkStudio.7ed28d42-37b3-4773-8a6e-e9ca6403a0fc>
[repo]: <https://github.com/madskristensen/BookmarkStudio>

# Bookmark Studio for Visual Studio

[![Build](https://github.com/madskristensen/BookmarkStudio/actions/workflows/build.yaml/badge.svg)](https://github.com/madskristensen/BookmarkStudio/actions/workflows/build.yaml)

Download this extension from the [Visual Studio Marketplace][marketplace]
or get the latest CI build from [Open VSIX Gallery][vsixgallery].

--------------------------------------

**Bookmark Studio helps you manage bookmarks across your entire solution, with quick keyboard access.**

**Zero learning curve** - Bookmark Studio uses the same keyboard shortcuts as Visual Studio's built-in bookmark features. Your existing muscle memory works perfectly, so you can start using enhanced bookmarks immediately without changing how you work.

![Tool window](art/bookmark-manager.png)

## Key Features at a Glance

- **Shortcuts** - Auto-assign, reassign, clear, and jump directly to bookmarks using **Ctrl+Alt+1-9**
- **Toolbar access** - Easily navigate to bookmarks from the Standard toolbar
- **Folder organization** - Group bookmarks into folders with drag-and-drop support
- **Color-coded bookmarks** - Assign Blue, Red, Orange, Yellow, Green, Purple, Pink, or Teal
- **Fast navigation** - Go to next/previous bookmark and jump from the manager
- **Search and filtering** - Filter bookmarks by name, file, preview text, location, slot, or color
- **Editor glyphs** - Colored glyphs in the margin with contextual actions and drag-and-drop functionality
- **Persistent bookmarks** - Saves labels, colors, slots, folders, and history per solution

![Glyphs](art/glyphs.png)

## Why Bookmark Studio?

Visual Studio bookmarks are useful, but lack modern features to keep up with complex codebases and workflows. Bookmark Studio aims to correct that by providing a powerful, yet familiar bookmark management experience that integrates seamlessly with your existing habits and workflows.

## Features

### Toggle bookmarks

Toggle bookmarks with the familiar **Ctrl+K, Ctrl+K** shortcut. Bookmark Studio will automatically assign the first available shortcut (1-9) to new bookmarks when possible, so you can quickly jump to them with **Ctrl+Alt+1-9**.

You can also drag the bookmark glyph in the margin to a new line to move the bookmark.

![Drag and drop of glyph](art/glyph-drag-drop.gif)

### Bookmark Manager Tool Window

Open **View > Bookmark Manager** to manage bookmarks in a dedicated tree view.

<!-- TODO: Add screenshot of the Bookmark Manager tool window showing the tree view with folders and bookmarks -->

- Search bookmarks by name, file, path, preview text, location, slot, or color
- Organize bookmarks into folders
- Navigate by double-click or context menu
- Edit label, assign/clear slot, set color, copy location, and delete bookmarks

### Toolbar Access

Bookmark Studio adds a new dropdown to the Standard toolbar for quick access to your numbered bookmarks.

![Toolbar](art/toolbar.png)

### Folder Organization

Group related bookmarks into folders for better organization:

- Create folders from the toolbar or context menu
- Drag and drop bookmarks between folders in the Bookmark Manager
- Drag bookmark glyphs directly from the editor margin into folders
- Rename or delete folders from the context menu
- Folders persist with your bookmark data

<!-- TODO: Add screenshot showing folder organization with bookmarks grouped by feature or task -->

### Color Support

Each bookmark has a color and shows as a colored square in both:

- Bookmark Manager tree view
- Editor glyph margin

![Glyphs](art/set-color.png)

You can change color from:

- Bookmark Manager context menu (**Set Color**)
- Glyph context menu in the editor margin

### Shortcut-based Navigation (1-9)

Bookmark Studio supports quick-access shortcuts:

- New bookmarks are auto-assigned the first available shortcut when possible
- Assign a bookmark to any shortcut from 1 to 9
- Reassigning a shortcut moves it to the selected bookmark
- Clear shortcut assignment without removing the bookmark
- Navigate directly with **Go To Bookmark Shortcut 1-9** commands

Default keybindings:

- **Alt+Shift+1** through **Alt+Shift+9**

### Built-in Bookmark Command Integration

Bookmark Studio intercepts Visual Studio built-in bookmark commands so existing workflows continue to work while using Bookmark Studio metadata and UI.

The following built-in commands are intercepted:

| Command                       | Default Shortcut           | Description                                               |
| ----------------------------- | -------------------------- | --------------------------------------------------------- |
| Toggle Bookmark               | Ctrl+K, Ctrl+K             | Toggle a bookmark at the current line                     |
| Next Bookmark                 | Ctrl+K, Ctrl+N             | Navigate to the next bookmark in the solution             |
| Previous Bookmark             | Ctrl+K, Ctrl+P             | Navigate to the previous bookmark in the solution         |
| Next Bookmark in Document     | Ctrl+Shift+K, Ctrl+Shift+N | Navigate to the next bookmark in the current document     |
| Previous Bookmark in Document | Ctrl+Shift+K, Ctrl+Shift+P | Navigate to the previous bookmark in the current document |
| Clear Bookmarks in Document   | Ctrl+Shift+K, Ctrl+Shift+L | Remove all bookmarks in the current document              |

This means you can continue using your familiar keyboard shortcuts while benefiting from Bookmark Studio's enhanced features like colors, labels, folders, and slots.

### Tracking and Refresh

Bookmark Studio keeps bookmarks accurate as files change:

- Tracks bookmark positions as text moves
- Refreshes bookmark data when documents open, save, or focus changes
- Updates manager and glyphs as metadata changes

## Commands

Bookmark Studio provides commands for:

- Open Bookmark Manager
- Refresh Bookmark Manager
- Add Folder
- Toggle Bookmark Studio bookmark at caret
- Go to next Bookmark Studio bookmark
- Go to previous Bookmark Studio bookmark
- Go to bookmark slot 1 through 9
- Delete selected bookmark (from manager toolbar)

## Storage

Bookmark Studio looks for `bookmarks.json` in the following order:

1. **Solution root** - `{solutionDirectory}/bookmarks.json`
2. **Repository root** - `{repoRoot}/bookmarks.json`
3. **Default (.vs folder)** - `{solutionDirectory}/.vs/bookmarks.json`

If no solution is loaded, Bookmark Studio uses a transient location under local app data.

### Sharing Bookmarks with Your Team

To share bookmarks with your team via source control:

1. Copy `.vs/bookmarks.json` to your solution root or repository root
2. Commit `bookmarks.json` to source control
3. Team members will automatically use the shared bookmarks file

This is useful for marking important code locations, onboarding landmarks, or review checkpoints that the whole team should see.

## Contribute

[Issues](https://github.com/madskristensen/BookmarkStudio/issues), ideas, and pull requests are welcome.
