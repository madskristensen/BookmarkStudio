using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

            menu.Items.Add(BookmarkContextMenuHelper.CreateAssignSlotSubmenu(bookmarkId, BookmarkManagerToolWindow.RefreshIfVisibleAsync));
            menu.Items.Add(new Separator());
            menu.Items.Add(BookmarkContextMenuHelper.CreateSetColorSubmenu(bookmarkId, BookmarkManagerToolWindow.RefreshIfVisibleAsync));
            menu.Items.Add(new Separator());
            menu.Items.Add(BookmarkContextMenuHelper.CreateRenameMenuItem(bookmarkId, BookmarkManagerToolWindow.RefreshIfVisibleAsync));

            ThemedContextMenuHelper.ApplyVsTheme(menu);
            return menu;
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
