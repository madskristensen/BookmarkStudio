using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;
using Microsoft.VisualStudio.Text.Tagging;

namespace BookmarkStudio
{
    internal sealed class BookmarkGlyphFactory : IGlyphFactory
    {
        public UIElement GenerateGlyph(IWpfTextViewLine line, IGlyphTag tag)
        {
            BookmarkGlyphTag? bookmarkTag = tag as BookmarkGlyphTag;
            string tooltip = BuildTooltip(bookmarkTag);
            Brush background = GetGlyphBrush(bookmarkTag);
            ContextMenu? contextMenu = null;

            if (bookmarkTag is not null && !string.IsNullOrEmpty(bookmarkTag.BookmarkId))
            {
                contextMenu = BuildGlyphContextMenu(bookmarkTag.BookmarkId);
            }

            // If there's a slot number, render it as text on the glyph
            if (bookmarkTag?.SlotNumber.HasValue == true)
            {
                Grid container = new Grid
                {
                    Width = 14,
                    Height = 14,
                    Margin = new Thickness(0, 0, 0, 0),
                    ToolTip = tooltip,
                    ContextMenu = contextMenu,
                };

                container.Children.Add(new Border
                {
                    Background = background,
                    CornerRadius = new CornerRadius(3),
                });

                container.Children.Add(new TextBlock
                {
                    Text = bookmarkTag.SlotNumber.Value.ToString(),
                    Foreground = Brushes.White,
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    FontFamily = new FontFamily("Segoe UI"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = Colors.Black,
                        BlurRadius = 1,
                        ShadowDepth = 0,
                        Opacity = 0.5,
                    },
                });

                return container;
            }

            Border glyph = new Border
            {
                Width = 14,
                Height = 14,
                Background = background,
                CornerRadius = new CornerRadius(3),
                ToolTip = tooltip,
                Margin = new Thickness(0, 0, 0, 0),
                ContextMenu = contextMenu,
            };

            return glyph;
        }

        private static ContextMenu BuildGlyphContextMenu(string bookmarkId)
        {
            ContextMenu menu = new ContextMenu();
            AddColorMenuItem(menu, "Blue (default)", BookmarkColor.Blue, bookmarkId);
            AddColorMenuItem(menu, "Red", BookmarkColor.Red, bookmarkId);
            AddColorMenuItem(menu, "Orange", BookmarkColor.Orange, bookmarkId);
            AddColorMenuItem(menu, "Yellow", BookmarkColor.Yellow, bookmarkId);
            AddColorMenuItem(menu, "Green", BookmarkColor.Green, bookmarkId);
            AddColorMenuItem(menu, "Purple", BookmarkColor.Purple, bookmarkId);
            AddColorMenuItem(menu, "Pink", BookmarkColor.Pink, bookmarkId);
            AddColorMenuItem(menu, "Teal", BookmarkColor.Teal, bookmarkId);
            menu.Items.Add(new Separator());
            AddRenameMenuItem(menu, bookmarkId);
            ThemedContextMenuHelper.ApplyVsTheme(menu);
            return menu;
        }

        private static void AddRenameMenuItem(ContextMenu menu, string bookmarkId)
        {
            MenuItem item = new MenuItem { Header = "Rename" };
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
                    await BookmarkManagerToolWindow.RefreshIfVisibleAsync(bookmarkId, CancellationToken.None);
                }).FireAndForget();
            };

            menu.Items.Add(item);
        }

        private static void AddColorMenuItem(ContextMenu menu, string header, BookmarkColor color, string bookmarkId)
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
                    await BookmarkManagerToolWindow.RefreshIfVisibleAsync(bookmarkId, CancellationToken.None);
                }).FireAndForget();
            };

            menu.Items.Add(item);
        }

        private static Brush GetGlyphBrush(BookmarkGlyphTag? bookmarkTag)
        {
            if (bookmarkTag is null)
            {
                return BookmarkColorToBrushConverter.GetBrush(BookmarkColor.None);
            }

            return BookmarkColorToBrushConverter.GetBrush(bookmarkTag.Color);
        }

        private static string BuildTooltip(BookmarkGlyphTag? bookmarkTag)
        {
            if (bookmarkTag is null)
            {
                return "BookmarkStudio bookmark";
            }

            bool hasLabel = !string.IsNullOrWhiteSpace(bookmarkTag.Label);
            bool hasSlot = bookmarkTag.SlotNumber.HasValue;

            if (hasLabel && hasSlot)
            {
                return string.Concat(bookmarkTag.Label, " [Slot ", bookmarkTag.SlotNumber.GetValueOrDefault().ToString(), "]");
            }

            if (hasLabel)
            {
                return bookmarkTag.Label;
            }

            if (hasSlot)
            {
                return string.Concat("Slot ", bookmarkTag.SlotNumber.GetValueOrDefault().ToString());
            }

            return "BookmarkStudio bookmark";
        }
    }
}
