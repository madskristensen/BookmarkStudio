using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell;

namespace BookmarkStudio
{
    /// <summary>
    /// Provides shared context menu functionality for bookmark operations.
    /// Used by both the margin glyph context menu and the treeview item context menu.
    /// </summary>
    internal static class BookmarkContextMenuHelper
    {
        /// <summary>
        /// Creates a complete "Assign Slot" submenu with slots 1-9 and a "Clear Slot" option.
        /// </summary>
        /// <param name="bookmarkId">The bookmark ID to operate on.</param>
        /// <param name="refreshCallback">Optional callback to invoke after slot operations complete.</param>
        public static MenuItem CreateAssignSlotSubmenu(string bookmarkId, Func<string, CancellationToken, Task>? refreshCallback = null)
        {
            MenuItem submenu = new MenuItem { Header = "Assign Slot" };
            submenu.SubmenuOpened += (sender, e) => PopulateSlotHeaders(submenu);

            for (int i = 1; i <= 9; i++)
            {
                MenuItem slotItem = new MenuItem { Header = i.ToString(), Tag = i };
                int slotNumber = i;
                slotItem.Click += (sender, e) =>
                {
                    ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                    {
                        await BookmarkOperationsService.Current.AssignSlotAsync(slotNumber, bookmarkId, CancellationToken.None);
                        if (refreshCallback is not null)
                        {
                            await refreshCallback(bookmarkId, CancellationToken.None);
                        }
                    }).FireAndForget();
                };
                submenu.Items.Add(slotItem);
            }

            // Add separator and Clear Slot inside the submenu
            submenu.Items.Add(new Separator());
            MenuItem clearSlotItem = new MenuItem { Header = "Clear Slot" };
            clearSlotItem.Click += (sender, e) =>
            {
                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await BookmarkOperationsService.Current.ClearSlotAsync(bookmarkId, CancellationToken.None);
                    if (refreshCallback is not null)
                    {
                        await refreshCallback(bookmarkId, CancellationToken.None);
                    }
                }).FireAndForget();
            };
            submenu.Items.Add(clearSlotItem);

            return submenu;
        }

        /// <summary>
        /// Creates a complete "Set Color" submenu with all color options.
        /// </summary>
        /// <param name="bookmarkId">The bookmark ID to operate on.</param>
        /// <param name="refreshCallback">Optional callback to invoke after color operations complete.</param>
        public static MenuItem CreateSetColorSubmenu(string bookmarkId, Func<string, CancellationToken, Task>? refreshCallback = null)
        {
            MenuItem submenu = new MenuItem { Header = "Set Color" };

            AddColorMenuItem(submenu, "Blue (default)", BookmarkColor.Blue, bookmarkId, refreshCallback);
            AddColorMenuItem(submenu, "Red", BookmarkColor.Red, bookmarkId, refreshCallback);
            AddColorMenuItem(submenu, "Orange", BookmarkColor.Orange, bookmarkId, refreshCallback);
            AddColorMenuItem(submenu, "Yellow", BookmarkColor.Yellow, bookmarkId, refreshCallback);
            AddColorMenuItem(submenu, "Green", BookmarkColor.Green, bookmarkId, refreshCallback);
            AddColorMenuItem(submenu, "Purple", BookmarkColor.Purple, bookmarkId, refreshCallback);
            AddColorMenuItem(submenu, "Pink", BookmarkColor.Pink, bookmarkId, refreshCallback);
            AddColorMenuItem(submenu, "Teal", BookmarkColor.Teal, bookmarkId, refreshCallback);

            return submenu;
        }

        /// <summary>
        /// Creates a "Rename" menu item with the standard Rename icon.
        /// </summary>
        /// <param name="bookmarkId">The bookmark ID to operate on.</param>
        /// <param name="refreshCallback">Optional callback to invoke after rename completes.</param>
        public static MenuItem CreateRenameMenuItem(string bookmarkId, Func<string, CancellationToken, Task>? refreshCallback = null)
        {
            CrispImage icon = new CrispImage
            {
                Moniker = KnownMonikers.Rename,
                Width = 16,
                Height = 16,
            };

            MenuItem item = new MenuItem { Header = "Rename", Icon = icon };
            item.Click += (sender, e) =>
            {
                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    ManagedBookmark bookmark = await BookmarkOperationsService.Current.GetBookmarkAsync(bookmarkId, CancellationToken.None);

                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(CancellationToken.None);
                    string? newLabel = TextPromptWindow.Show("Edit Label", "Enter a label for the bookmark:", bookmark.Label, selectTextOnLoad: true);
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
        /// Updates slot menu item headers to show current bookmark assignments.
        /// Call this from the SubmenuOpened event handler.
        /// </summary>
        /// <param name="submenu">The Assign Slot submenu.</param>
        public static void PopulateSlotHeaders(MenuItem submenu)
        {
            Dictionary<int, string> slotAssignments = GetSlotAssignments();

            foreach (object item in submenu.Items)
            {
                if (item is MenuItem slotMenuItem && slotMenuItem.Tag is int slotNumber)
                {
                    if (slotAssignments.TryGetValue(slotNumber, out string bookmarkLabel))
                    {
                        slotMenuItem.Header = string.Concat(slotNumber.ToString(System.Globalization.CultureInfo.InvariantCulture), " - ", bookmarkLabel);
                    }
                    else
                    {
                        slotMenuItem.Header = string.Concat(slotNumber.ToString(System.Globalization.CultureInfo.InvariantCulture), " - Unassigned");
                    }
                }
            }
        }

        /// <summary>
        /// Gets a dictionary mapping slot numbers to bookmark labels.
        /// </summary>
        public static Dictionary<int, string> GetSlotAssignments()
        {
            Dictionary<int, string> assignments = new Dictionary<int, string>();

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
                    if (bookmark.SlotNumber.HasValue && bookmark.SlotNumber.Value >= 1 && bookmark.SlotNumber.Value <= 9)
                    {
                        string label = string.IsNullOrWhiteSpace(bookmark.Label)
                            ? bookmark.FileName
                            : bookmark.Label;
                        assignments[bookmark.SlotNumber.Value] = label;
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
            Border icon = new Border
            {
                Width = 10,
                Height = 10,
                Background = BookmarkColorToBrushConverter.GetBrush(color),
                CornerRadius = new CornerRadius(2),
            };

            MenuItem item = new MenuItem { Header = header, Icon = icon };
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
