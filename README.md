# BookmarkStudio

[![Build](https://github.com/madskristensen/BookmarkStudio/actions/workflows/build.yaml/badge.svg)](https://github.com/madskristensen/BookmarkStudio/actions/workflows/build.yaml)

BookmarkStudio is a Visual Studio extension for managing code bookmarks across an entire solution. It adds a dedicated bookmark manager, richer bookmark metadata, quick slot navigation, export options, and bookmark tracking that stays useful as files change.

## Install

Download from the [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=MadsKristensen.BookmarkStudio) or grab the latest CI build from the [Open VSIX Gallery](https://www.vsixgallery.com/).

## Features

- Toggle a BookmarkStudio bookmark at the current caret location.
- Open a dedicated **Bookmark Manager** tool window from **View > Other Windows**.
- Navigate to the next or previous BookmarkStudio bookmark.
- Assign bookmarks to quick-access slots **1-9** and jump directly to those slots.
- Add a custom **label** and **group** to each bookmark.
- Search bookmarks from the tool window search box.
- Sort bookmarks by **slot**, **file**, **line**, **label**, **group**, or **created date**.
- Group bookmarks by **group**, **file**, or **slot**.
- Double-click a bookmark or use the context menu to navigate to it.
- Copy a bookmark location to the clipboard.
- Export bookmarks as **plain text**, **Markdown**, or **CSV**.
- Show bookmark glyphs in the editor margin.
- Automatically refresh bookmark data when documents open, save, or window focus changes.
- Track bookmark positions as text moves in edited files.
- Persist bookmark metadata per solution.

## Bookmark metadata

Each bookmark can store:

- Slot number
- Label
- Group
- File and line location
- Line preview text
- Created timestamp
- Last visited timestamp

## How to use

1. Run **Toggle BookmarkStudio Bookmark** to add or remove a bookmark on the current line.
2. Open **Bookmark Manager** from **View > Other Windows**.
3. Select a bookmark to edit its **Label**, **Group**, or **Slot**.
4. Use **Navigate**, **Remove**, **Clear Group**, **Clear Slot**, or **Copy Location** from the tool window.
5. Use the manager toolbar to **refresh** or **export** bookmarks.

## Commands

BookmarkStudio exposes commands for:

- Opening the Bookmark Manager
- Toggling the current bookmark
- Navigating to the next bookmark
- Navigating to the previous bookmark
- Assigning slot 1 through 9
- Going to slot 1 through 9
- Renaming a bookmark label
- Setting or clearing a bookmark group
- Clearing a bookmark slot
- Copying a bookmark location
- Exporting bookmarks

These commands can be bound to keyboard shortcuts from Visual Studio's keyboard settings.

## Storage

BookmarkStudio stores solution-specific bookmark metadata under:

`.vs/<solution-name>/BookmarkStudio/bookmark-metadata.dat`

If no solution is available, it falls back to a transient location under local app data.

## Compatibility

- Visual Studio 2022
- x64 and Arm64 installations
- Requires the Visual Studio core editor component

## Contribute

[Issues](https://github.com/madskristensen/BookmarkStudio/issues), ideas, and pull requests are welcome.
