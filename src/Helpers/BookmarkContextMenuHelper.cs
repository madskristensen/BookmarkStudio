using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Imaging;

namespace BookmarkStudio
{
    /// <summary>
    /// Provides shared context menu functionality for bookmark operations.
    /// Used by both the margin glyph context menu and the treeview item context menu.
    /// </summary>
    internal static class BookmarkContextMenuHelper
    {
        /// <summary>
        /// Creates a complete "Assign Shortcut" submenu with shortcuts 1-9 and a "Clear Shortcut" option.
        /// </summary>
        /// <param name="bookmarkId">The bookmark ID to operate on.</param>
        /// <param name="refreshCallback">Optional callback to invoke after shortcut operations complete.</param>
        public static MenuItem CreateAssignShortcutSubmenu(string bookmarkId, Func<string, CancellationToken, Task>? refreshCallback = null)
        {
            var submenu = new MenuItem { Header = "Assign shortcut" };
            submenu.SubmenuOpened += (sender, e) => PopulateShortcutHeaders(submenu);

            for (var i = 1; i <= 9; i++)
            {
                var shortcutItem = new MenuItem { Header = i.ToString(), Tag = i };
                var shortcutNumber = i;
                shortcutItem.Click += (sender, e) =>
                {
                    ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                    {
                        await BookmarkOperationsService.Current.AssignShortcutAsync(shortcutNumber, bookmarkId, CancellationToken.None);
                        if (refreshCallback is not null)
                        {
                            await refreshCallback(bookmarkId, CancellationToken.None);
                        }
                    }).FireAndForget();
                };
                submenu.Items.Add(shortcutItem);
            }

            // Add separator and Clear Shortcut inside the submenu
            submenu.Items.Add(new Separator());
            var clearShortcutItem = new MenuItem { Header = "Clear Shortcut" };
            clearShortcutItem.Click += (sender, e) =>
            {
                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await BookmarkOperationsService.Current.ClearShortcutAsync(bookmarkId, CancellationToken.None);
                    if (refreshCallback is not null)
                    {
                        await refreshCallback(bookmarkId, CancellationToken.None);
                    }
                }).FireAndForget();
            };
            submenu.Items.Add(clearShortcutItem);

            return submenu;
        }

        /// <summary>
        /// Creates a complete "Set Color" submenu with all color options.
        /// </summary>
        /// <param name="bookmarkId">The bookmark ID to operate on.</param>
        /// <param name="refreshCallback">Optional callback to invoke after color operations complete.</param>
        public static MenuItem CreateSetColorSubmenu(string bookmarkId, Func<string, CancellationToken, Task>? refreshCallback = null)
        {
            var submenu = new MenuItem { Header = "Set Color" };
            BookmarkColor defaultColor = General.Instance.DefaultBookmarkColor;

            AddColorMenuItem(submenu, GetColorLabel("Blue", BookmarkColor.Blue, defaultColor), BookmarkColor.Blue, bookmarkId, refreshCallback);
            AddColorMenuItem(submenu, GetColorLabel("Red", BookmarkColor.Red, defaultColor), BookmarkColor.Red, bookmarkId, refreshCallback);
            AddColorMenuItem(submenu, GetColorLabel("Orange", BookmarkColor.Orange, defaultColor), BookmarkColor.Orange, bookmarkId, refreshCallback);
            AddColorMenuItem(submenu, GetColorLabel("Yellow", BookmarkColor.Yellow, defaultColor), BookmarkColor.Yellow, bookmarkId, refreshCallback);
            AddColorMenuItem(submenu, GetColorLabel("Green", BookmarkColor.Green, defaultColor), BookmarkColor.Green, bookmarkId, refreshCallback);
            AddColorMenuItem(submenu, GetColorLabel("Purple", BookmarkColor.Purple, defaultColor), BookmarkColor.Purple, bookmarkId, refreshCallback);
            AddColorMenuItem(submenu, GetColorLabel("Pink", BookmarkColor.Pink, defaultColor), BookmarkColor.Pink, bookmarkId, refreshCallback);
            AddColorMenuItem(submenu, GetColorLabel("Teal", BookmarkColor.Teal, defaultColor), BookmarkColor.Teal, bookmarkId, refreshCallback);

            return submenu;
        }

        private static string GetColorLabel(string name, BookmarkColor color, BookmarkColor defaultColor)
            => color == defaultColor ? $"{name} (default)" : name;

        /// <summary>
        /// Creates a "Rename" menu item with the standard Rename icon.
        /// </summary>
        /// <param name="bookmarkId">The bookmark ID to operate on.</param>
        /// <param name="refreshCallback">Optional callback to invoke after rename completes.</param>
        public static MenuItem CreateRenameMenuItem(string bookmarkId, Func<string, CancellationToken, Task>? refreshCallback = null)
        {
            var icon = new CrispImage
            {
                Moniker = KnownMonikers.Rename,
                Width = 16,
                Height = 16,
            };

            var item = new MenuItem { Header = "Rename", Icon = icon };
            item.Click += (sender, e) =>
            {
                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    ManagedBookmark bookmark = await BookmarkOperationsService.Current.GetBookmarkAsync(bookmarkId, CancellationToken.None);

                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(CancellationToken.None);
                    var newLabel = TextPromptWindow.Show("Edit Label", "Enter a label for the bookmark:", bookmark.Label, selectTextOnLoad: true);
                    if (newLabel is null)
                    {
                        return;
                    }

                    await BookmarkOperationsService.Current.RenameLabelAsync(bookmarkId, newLabel, CancellationToken.None);
                    if (refreshCallback is not null)
                    {
                        await refreshCallback(bookmarkId, CancellationToken.None);
                    }
                }).FireAndForget();
            };

            return item;
        }

        /// <summary>
        /// Updates shortcut menu item headers to show current bookmark assignments.
        /// Call this from the SubmenuOpened event handler.
        /// </summary>
        /// <param name="submenu">The Assign Shortcut submenu.</param>
        public static void PopulateShortcutHeaders(MenuItem submenu)
        {
            Dictionary<int, string> shortcutAssignments = GetShortcutAssignments();

            foreach (var item in submenu.Items)
            {
                if (item is MenuItem shortcutMenuItem && shortcutMenuItem.Tag is int shortcutNumber)
                {
                    if (shortcutAssignments.TryGetValue(shortcutNumber, out var bookmarkLabel))
                    {
                        shortcutMenuItem.Header = string.Concat(shortcutNumber.ToString(System.Globalization.CultureInfo.InvariantCulture), " - ", bookmarkLabel);
                    }
                    else
                    {
                        shortcutMenuItem.Header = string.Concat(shortcutNumber.ToString(System.Globalization.CultureInfo.InvariantCulture), " - <Unassigned>");
                    }
                }
            }
        }

        /// <summary>
        /// Gets a dictionary mapping shortcut numbers to bookmark labels.
        /// </summary>
        public static Dictionary<int, string> GetShortcutAssignments()
        {
            var assignments = new Dictionary<int, string>();

            try
            {
                DualBookmarkWorkspaceState dualState = ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    return await BookmarkOperationsService.Current.RefreshDualAsync(CancellationToken.None);
                });

                IEnumerable<ManagedBookmark> allBookmarks = BookmarkRepositoryService.ToManagedBookmarks(dualState.PersonalState.Bookmarks)
                    .Concat(BookmarkRepositoryService.ToManagedBookmarks(dualState.SolutionState.Bookmarks));

                foreach (ManagedBookmark bookmark in allBookmarks)
                {
                    if (bookmark.ShortcutNumber.HasValue && bookmark.ShortcutNumber.Value >= 1 && bookmark.ShortcutNumber.Value <= 9)
                    {
                        var label = string.IsNullOrWhiteSpace(bookmark.Label)
                            ? bookmark.FileName
                            : bookmark.Label;
                        assignments[bookmark.ShortcutNumber.Value] = label;
                    }
                }
            }
            catch
            {
                // If we can't get bookmarks, just return empty assignments
            }

            return assignments;
        }

        private static void AddColorMenuItem(MenuItem submenu, string header, BookmarkColor color, string bookmarkId, Func<string, CancellationToken, Task>? refreshCallback)
        {
            var icon = new Border
            {
                Width = 10,
                Height = 10,
                Background = BookmarkColorToBrushConverter.GetBrush(color),
                CornerRadius = new CornerRadius(2),
            };

            var item = new MenuItem { Header = header, Icon = icon };
            item.Click += (sender, e) =>
            {
                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await BookmarkOperationsService.Current.SetColorAsync(bookmarkId, color, CancellationToken.None);
                    if (refreshCallback is not null)
                    {
                        await refreshCallback(bookmarkId, CancellationToken.None);
                    }
                }).FireAndForget();
            };

            submenu.Items.Add(item);
        }
    }
}
