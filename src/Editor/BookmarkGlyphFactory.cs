using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

namespace BookmarkStudio
{
    internal sealed class BookmarkGlyphFactory : IGlyphFactory
    {
        public UIElement GenerateGlyph(IWpfTextViewLine line, IGlyphTag tag)
        {
            var bookmarkTag = tag as BookmarkGlyphTag;
            var tooltip = BuildTooltip(bookmarkTag);
            Brush background = GetGlyphBrush(bookmarkTag);
            ContextMenu? contextMenu = null;
            var bookmarkId = bookmarkTag?.BookmarkId;

            if (bookmarkTag is not null && !string.IsNullOrEmpty(bookmarkTag.BookmarkId))
            {
                contextMenu = BuildGlyphContextMenu(bookmarkTag.BookmarkId);
            }

            // If there's a shortcut number, render it as text on the glyph
            if (bookmarkTag?.ShortcutNumber.HasValue == true)
            {
                var container = new Grid
                {
                    Width = 14,
                    Height = 14,
                    Margin = new Thickness(0, 0, 0, 0),
                    ToolTip = tooltip,
                    ContextMenu = contextMenu,
                    Tag = bookmarkId,
                    Cursor = Cursors.Hand,
                };

                container.Children.Add(new Border
                {
                    Background = background,
                    CornerRadius = new CornerRadius(3),
                });

                container.Children.Add(new TextBlock
                {
                    Text = bookmarkTag.ShortcutNumber.Value.ToString(),
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

            var glyph = new Border
            {
                Width = 14,
                Height = 14,
                Background = background,
                CornerRadius = new CornerRadius(3),
                ToolTip = tooltip,
                Margin = new Thickness(0, 0, 0, 0),
                ContextMenu = contextMenu,
                Tag = bookmarkId,
                Cursor = Cursors.Hand,
            };

            return glyph;
        }

        private static ContextMenu BuildGlyphContextMenu(string bookmarkId)
        {
            var menu = new ContextMenu();

            menu.Items.Add(BookmarkContextMenuHelper.CreateRemoveBookmarkMenuItem(bookmarkId, BookmarkManagerToolWindow.RefreshIfVisibleAsync));
            menu.Items.Add(new Separator());
            menu.Items.Add(BookmarkContextMenuHelper.CreateAssignShortcutSubmenu(bookmarkId, BookmarkManagerToolWindow.RefreshIfVisibleAsync));
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

        private static object BuildTooltip(BookmarkGlyphTag? bookmarkTag)
        {
            if (bookmarkTag is null)
            {
                return "BookmarkStudio bookmark";
            }

            var hasLabel = !string.IsNullOrWhiteSpace(bookmarkTag.Label);
            var hasShortcut = bookmarkTag.ShortcutNumber.HasValue;

            if (hasLabel && hasShortcut)
            {
                return CreateThemedTooltip(bookmarkTag.Label, bookmarkTag.ShortcutNumber.Value);
            }

            if (hasLabel)
            {
                return bookmarkTag.Label;
            }

            if (hasShortcut)
            {
                return CreateThemedTooltip(null, bookmarkTag.ShortcutNumber.Value);
            }

            return "BookmarkStudio bookmark";
        }

        private static ToolTip CreateThemedTooltip(string? label, int slotNumber)
        {
            var shortcut = string.Concat("Alt+Shift+", slotNumber.ToString(CultureInfo.InvariantCulture));

            var panel = new StackPanel
            {
                Orientation = Orientation.Vertical,
            };

            if (!string.IsNullOrWhiteSpace(label))
            {
                var labelText = new TextBlock
                {
                    Text = label,
                    FontWeight = FontWeights.SemiBold,
                };
                labelText.SetResourceReference(TextBlock.ForegroundProperty, EnvironmentColors.ToolTipTextBrushKey);
                panel.Children.Add(labelText);
            }

            var shortcutText = new TextBlock
            {
                Text = shortcut,
                Opacity = 0.8,
            };
            shortcutText.SetResourceReference(TextBlock.ForegroundProperty, EnvironmentColors.ToolTipTextBrushKey);
            panel.Children.Add(shortcutText);

            var tooltip = new ToolTip
            {
                Content = panel,
            };
            tooltip.SetResourceReference(ToolTip.BackgroundProperty, EnvironmentColors.ToolTipBrushKey);
            tooltip.SetResourceReference(ToolTip.BorderBrushProperty, EnvironmentColors.ToolTipBorderBrushKey);
            tooltip.SetResourceReference(ToolTip.ForegroundProperty, EnvironmentColors.ToolTipTextBrushKey);

            return tooltip;
        }
    }
}
