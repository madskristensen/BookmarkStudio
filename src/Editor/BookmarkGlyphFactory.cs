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
            menu.Items.Add(BookmarkContextMenuHelper.CreateEditNoteMenuItem(bookmarkId, BookmarkManagerToolWindow.RefreshIfVisibleAsync));

            MenuItem deleteNoteItem = BookmarkContextMenuHelper.CreateDeleteNoteMenuItem(bookmarkId, BookmarkManagerToolWindow.RefreshIfVisibleAsync);
            menu.Items.Add(deleteNoteItem);

            menu.Opened += (sender, e) =>
            {
                deleteNoteItem.Visibility = HasNote(bookmarkId) ? Visibility.Visible : Visibility.Collapsed;
            };

            ThemedContextMenuHelper.ApplyVsTheme(menu);
            return menu;
        }

        private static bool HasNote(string bookmarkId)
        {
            foreach (ManagedBookmark bookmark in BookmarkStudioSession.Current.CachedBookmarks)
            {
                if (string.Equals(bookmark.BookmarkId, bookmarkId, StringComparison.Ordinal))
                {
                    return !string.IsNullOrWhiteSpace(bookmark.Note);
                }
            }

            return false;
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
            var hasNote = !string.IsNullOrWhiteSpace(bookmarkTag.Note);
            var hasShortcut = bookmarkTag.ShortcutNumber.HasValue;

            if (!hasLabel && !hasNote && !hasShortcut)
            {
                return "BookmarkStudio bookmark";
            }

            return CreateThemedTooltip(
                hasLabel ? bookmarkTag.Label : null,
                hasNote ? bookmarkTag.Note : null,
                hasShortcut ? bookmarkTag.ShortcutNumber : null);
        }

        private static ToolTip CreateThemedTooltip(string? label, string? note, int? slotNumber)
        {
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

            if (!string.IsNullOrWhiteSpace(note))
            {
                var noteText = new TextBlock
                {
                    Text = note,
                    FontStyle = FontStyles.Italic,
                    Opacity = 0.85,
                    Margin = new Thickness(0, !string.IsNullOrWhiteSpace(label) ? 2 : 0, 0, 0),
                };
                noteText.SetResourceReference(TextBlock.ForegroundProperty, EnvironmentColors.ToolTipTextBrushKey);
                panel.Children.Add(noteText);
            }

            if (slotNumber.HasValue)
            {
                var shortcut = string.Concat("Alt+Shift+", slotNumber.Value.ToString(CultureInfo.InvariantCulture));
                var shortcutText = new TextBlock
                {
                    Text = shortcut,
                    Opacity = 0.8,
                    Margin = new Thickness(0, panel.Children.Count > 0 ? 4 : 0, 0, 0),
                };
                shortcutText.SetResourceReference(TextBlock.ForegroundProperty, EnvironmentColors.ToolTipTextBrushKey);
                panel.Children.Add(shortcutText);
            }

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
