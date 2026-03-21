using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;

namespace BookmarkStudio
{
    internal static class ThemedContextMenuHelper
    {
        private static Style? _contextMenuStyle;
        private static Style? _menuItemStyle;
        private static Style? _separatorStyle;

        public static void ApplyVsTheme(ContextMenu menu)
        {
            if (menu is null)
            {
                return;
            }

            menu.Style = GetContextMenuStyle();
            menu.Opened += (sender, e) => ApplyThemeToMenuItems(menu.Items);
        }

        private static void ApplyThemeToMenuItems(ItemCollection items)
        {
            foreach (object item in items)
            {
                if (item is MenuItem menuItem)
                {
                    menuItem.Style = GetMenuItemStyle();

                    if (menuItem.HasItems)
                    {
                        ApplyThemeToMenuItems(menuItem.Items);
                    }
                }
                else if (item is Separator separator)
                {
                    separator.Style = GetSeparatorStyle();
                }
            }
        }

        private static Style GetContextMenuStyle()
        {
            if (_contextMenuStyle is not null)
            {
                return _contextMenuStyle;
            }

            _contextMenuStyle = new Style(typeof(ContextMenu));
            _contextMenuStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            _contextMenuStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(2)));
            _contextMenuStyle.Setters.Add(new Setter(Grid.IsSharedSizeScopeProperty, true));

            ControlTemplate template = new ControlTemplate(typeof(ContextMenu));
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.SetResourceReference(Border.BackgroundProperty, VsBrushes.CommandBarMenuBackgroundGradientBeginKey);
            border.SetResourceReference(Border.BorderBrushProperty, VsBrushes.CommandBarMenuBorderKey);
            border.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            border.SetBinding(Border.PaddingProperty, new Binding("Padding") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            border.SetValue(Border.SnapsToDevicePixelsProperty, true);

            FrameworkElementFactory itemsPresenter = new FrameworkElementFactory(typeof(ItemsPresenter));
            itemsPresenter.SetBinding(ItemsPresenter.SnapsToDevicePixelsProperty, new Binding("SnapsToDevicePixels") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
            border.AppendChild(itemsPresenter);

            template.VisualTree = border;
            _contextMenuStyle.Setters.Add(new Setter(Control.TemplateProperty, template));
            _contextMenuStyle.Seal();

            return _contextMenuStyle;
        }

        private static Style GetMenuItemStyle()
        {
            if (_menuItemStyle is not null)
            {
                return _menuItemStyle;
            }

            _menuItemStyle = new Style(typeof(MenuItem));
            _menuItemStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            _menuItemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
            _menuItemStyle.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 22.0));

            ControlTemplate template = new ControlTemplate(typeof(MenuItem));

            // Main border
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.Name = "Border";
            border.SetValue(Border.BackgroundProperty, Brushes.Transparent);
            border.SetValue(Border.BorderThicknessProperty, new Thickness(0));
            border.SetValue(Border.SnapsToDevicePixelsProperty, true);

            // Grid for layout
            FrameworkElementFactory grid = new FrameworkElementFactory(typeof(Grid));

            FrameworkElementFactory col0 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col0.SetValue(ColumnDefinition.WidthProperty, new GridLength(26));
            col0.SetValue(ColumnDefinition.SharedSizeGroupProperty, "MenuItemIconColumnGroup");

            FrameworkElementFactory col1 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col1.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));

            FrameworkElementFactory col2 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col2.SetValue(ColumnDefinition.WidthProperty, new GridLength(17));

            grid.AppendChild(col0);
            grid.AppendChild(col1);
            grid.AppendChild(col2);

            // Icon gutter background
            FrameworkElementFactory iconGutter = new FrameworkElementFactory(typeof(Border));
            iconGutter.SetValue(Grid.ColumnProperty, 0);
            iconGutter.SetResourceReference(Border.BackgroundProperty, VsBrushes.CommandBarMenuIconBackgroundKey);

            // Icon
            FrameworkElementFactory icon = new FrameworkElementFactory(typeof(ContentPresenter));
            icon.SetValue(Grid.ColumnProperty, 0);
            icon.SetValue(ContentPresenter.ContentSourceProperty, "Icon");
            icon.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            icon.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            icon.SetValue(FrameworkElement.WidthProperty, 16.0);
            icon.SetValue(FrameworkElement.HeightProperty, 16.0);
            icon.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 0, 4, 0));

            // Header
            FrameworkElementFactory header = new FrameworkElementFactory(typeof(ContentPresenter));
            header.SetValue(Grid.ColumnProperty, 1);
            header.SetValue(ContentPresenter.ContentSourceProperty, "Header");
            header.SetValue(ContentPresenter.RecognizesAccessKeyProperty, true);
            header.SetValue(FrameworkElement.MarginProperty, new Thickness(6, 4, 6, 4));
            header.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);

            // Submenu arrow
            FrameworkElementFactory arrow = new FrameworkElementFactory(typeof(Path));
            arrow.Name = "Arrow";
            arrow.SetValue(Grid.ColumnProperty, 2);
            arrow.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            arrow.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            arrow.SetResourceReference(Shape.FillProperty, VsBrushes.CommandBarMenuSubmenuGlyphKey);
            arrow.SetValue(Path.DataProperty, Geometry.Parse("M0,0 L4,3.5 L0,7 z"));
            arrow.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);

            // Submenu popup
            FrameworkElementFactory popup = new FrameworkElementFactory(typeof(Popup));
            popup.Name = "PART_Popup";
            popup.SetValue(Popup.AllowsTransparencyProperty, true);
            popup.SetValue(Popup.PlacementProperty, PlacementMode.Right);
            popup.SetValue(Popup.FocusableProperty, false);
            popup.SetValue(Popup.PopupAnimationProperty, PopupAnimation.Fade);
            popup.SetBinding(Popup.IsOpenProperty, new Binding("IsSubmenuOpen") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });

            FrameworkElementFactory popupBorder = new FrameworkElementFactory(typeof(Border));
            popupBorder.SetResourceReference(Border.BackgroundProperty, VsBrushes.CommandBarMenuBackgroundGradientBeginKey);
            popupBorder.SetResourceReference(Border.BorderBrushProperty, VsBrushes.CommandBarMenuBorderKey);
            popupBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            popupBorder.SetValue(Border.PaddingProperty, new Thickness(2));
            popupBorder.SetValue(Grid.IsSharedSizeScopeProperty, true);

            FrameworkElementFactory popupItemsPresenter = new FrameworkElementFactory(typeof(ItemsPresenter));
            popupBorder.AppendChild(popupItemsPresenter);
            popup.AppendChild(popupBorder);

            grid.AppendChild(iconGutter);
            grid.AppendChild(icon);
            grid.AppendChild(header);
            grid.AppendChild(arrow);
            grid.AppendChild(popup);
            border.AppendChild(grid);

            template.VisualTree = border;

            // Triggers
            Trigger hasItemsTrigger = new Trigger { Property = MenuItem.HasItemsProperty, Value = true };
            hasItemsTrigger.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible, "Arrow"));
            template.Triggers.Add(hasItemsTrigger);

            Trigger highlightTrigger = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
            highlightTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new DynamicResourceExtension(EnvironmentColors.CommandBarMenuItemMouseOverBrushKey), "Border"));
            template.Triggers.Add(highlightTrigger);

            Trigger disabledTrigger = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
            disabledTrigger.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension(EnvironmentColors.CommandBarTextInactiveBrushKey)));
            template.Triggers.Add(disabledTrigger);

            _menuItemStyle.Setters.Add(new Setter(Control.TemplateProperty, template));
            _menuItemStyle.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension(EnvironmentColors.CommandBarTextActiveBrushKey)));
            _menuItemStyle.Seal();

            return _menuItemStyle;
        }

        private static Style GetSeparatorStyle()
        {
            if (_separatorStyle is not null)
            {
                return _separatorStyle;
            }

            _separatorStyle = new Style(typeof(Separator));
            _separatorStyle.Setters.Add(new Setter(FrameworkElement.HeightProperty, 1.0));

            ControlTemplate template = new ControlTemplate(typeof(Separator));

            FrameworkElementFactory grid = new FrameworkElementFactory(typeof(Grid));
            grid.SetValue(UIElement.SnapsToDevicePixelsProperty, true);

            FrameworkElementFactory col0 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col0.SetValue(ColumnDefinition.WidthProperty, new GridLength(26));
            col0.SetValue(ColumnDefinition.SharedSizeGroupProperty, "MenuItemIconColumnGroup");

            FrameworkElementFactory col1 = new FrameworkElementFactory(typeof(ColumnDefinition));
            col1.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));

            grid.AppendChild(col0);
            grid.AppendChild(col1);

            FrameworkElementFactory gutterBorder = new FrameworkElementFactory(typeof(Border));
            gutterBorder.SetValue(Grid.ColumnProperty, 0);
            gutterBorder.SetResourceReference(Border.BackgroundProperty, VsBrushes.CommandBarMenuIconBackgroundKey);

            FrameworkElementFactory line = new FrameworkElementFactory(typeof(Rectangle));
            line.SetValue(Grid.ColumnProperty, 1);
            line.SetValue(FrameworkElement.HeightProperty, 1.0);
            line.SetValue(FrameworkElement.MarginProperty, new Thickness(2, 0, 2, 0));
            line.SetResourceReference(Shape.FillProperty, VsBrushes.CommandBarMenuSeparatorKey);

            grid.AppendChild(gutterBorder);
            grid.AppendChild(line);

            template.VisualTree = grid;
            _separatorStyle.Setters.Add(new Setter(Control.TemplateProperty, template));
            _separatorStyle.Seal();

            return _separatorStyle;
        }
    }
}

