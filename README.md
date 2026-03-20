[marketplace]: <https://marketplace.visualstudio.com/items?itemName=MadsKristensen.BookmarkStudio>
[vsixgallery]: <https://www.vsixgallery.com/extension/BookmarkStudio.7ed28d42-37b3-4773-8a6e-e9ca6403a0fc>
[repo]: <https://github.com/madskristensen/BookmarkStudio>

# BookmarkStudio for Visual Studio

[![Build](https://github.com/madskristensen/BookmarkStudio/actions/workflows/build.yaml/badge.svg)](https://github.com/madskristensen/BookmarkStudio/actions/workflows/build.yaml)

Download this extension from the [Visual Studio Marketplace][marketplace]
or get the latest CI build from [Open VSIX Gallery][vsixgallery].

--------------------------------------

**BookmarkStudio helps you manage bookmarks across your entire solution, not just the current file.**

## ✨ Key Features at a Glance

- **Solution-wide Bookmark Manager** — View all bookmarks in one place
- **Color-coded bookmarks** — Assign Orange, Red, Yellow, Green, Blue, Purple, Pink, or Teal
- **Quick slots (1-9)** — Auto-assign, reassign, clear, and jump directly to slot bookmarks
- **Built-in command interception** — Existing VS bookmark shortcuts work with BookmarkStudio
- **Fast navigation** — Go to next/previous bookmark and jump from the manager
- **Search + sorting** — Filter bookmarks and sort by slot, file, line, label, preview, or color
- **Editor glyphs** — Colored glyphs in the margin with contextual actions
- **Export options** — Export as plain text, Markdown, or CSV
- **Persistent metadata** — Saves labels, colors, slots, and history per solution

## Why BookmarkStudio?

Visual Studio bookmarks are useful, but they can be hard to manage across larger solutions. BookmarkStudio adds a dedicated workflow for organizing, navigating, and exporting bookmarks with richer metadata and color support.

## Features

### Bookmark Manager Tool Window

Open **View > Other Windows > Bookmark Manager** to manage bookmarks in a dedicated grid.

- Search bookmarks by label, file, path, preview text, location, slot, or color
- Sort by slot, file, line, label, preview, or color
- Navigate by double-click or context menu
- Edit label, assign/clear slot, set color, copy location, and remove bookmarks

### Color Support

Each bookmark has a color and shows as a colored square in both:

- Bookmark Manager grid
- Editor glyph margin

Available colors:

- Orange (default)
- Red
- Yellow
- Green
- Blue
- Purple
- Pink
- Teal

You can change color from:

- Bookmark Manager context menu (**Set Color**)
- Glyph context menu in the editor margin

### Slot-based Navigation (1-9)

BookmarkStudio supports quick-access slots:

- New bookmarks are auto-assigned the first available slot when possible
- Assign a bookmark to any slot from 1 to 9
- Reassigning a slot moves it to the selected bookmark
- Clear slot assignment without removing the bookmark
- Navigate directly with **Go To Bookmark Slot 1-9** commands

Default keybindings:

- **Alt+Shift+1** through **Alt+Shift+9**

### Built-in Bookmark Command Integration

BookmarkStudio intercepts Visual Studio built-in bookmark commands so existing workflows continue to work while using BookmarkStudio metadata and UI.

### Export

Export bookmarks from the manager toolbar to:

- **Plain text** (`.txt`)
- **Markdown** (`.md`)
- **CSV** (`.csv`)

Exports include slot and color values.

### Tracking and Refresh

BookmarkStudio keeps bookmarks accurate as files change:

- Tracks bookmark positions as text moves
- Refreshes bookmark data when documents open, save, or focus changes
- Updates manager and glyphs as metadata changes

## Bookmark metadata

Each bookmark stores:

- Slot number (optional)
- Label
- Color
- File path, line, and column location
- Line preview text
- Created timestamp
- Last visited timestamp

## Commands

BookmarkStudio provides commands for:

- Open Bookmark Manager
- Refresh Bookmark Manager
- Toggle BookmarkStudio bookmark at caret
- Go to next BookmarkStudio bookmark
- Go to previous BookmarkStudio bookmark
- Go to bookmark slot 1 through 9
- Export bookmarks
- Delete selected bookmark (from manager toolbar)

## Storage

BookmarkStudio looks for `bookmarks.json` in the following order:

1. **Solution root** - `{solutionDirectory}/bookmarks.json`
2. **Repository root** - `{repoRoot}/bookmarks.json`
3. **Default (.vs folder)** - `{solutionDirectory}/.vs/bookmarks.json`

If no solution is loaded, BookmarkStudio uses a transient location under local app data.

### Sharing bookmarks with your team

To share bookmarks with your team via source control:

1. Copy `.vs/bookmarks.json` to your solution root or repository root
2. Commit `bookmarks.json` to source control
3. Team members will automatically use the shared bookmarks file

This is useful for marking important code locations, onboarding landmarks, or review checkpoints that the whole team should see.

## Compatibility

- Visual Studio 2022
- x64 and Arm64 installations
- Requires the Visual Studio core editor component

## Contribute

[Issues](https://github.com/madskristensen/BookmarkStudio/issues), ideas, and pull requests are welcome.
