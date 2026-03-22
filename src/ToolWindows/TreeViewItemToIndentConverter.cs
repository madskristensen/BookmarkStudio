using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace BookmarkStudio
{
    /// <summary>
    /// Converts a <see cref="TreeViewItem"/> to a <see cref="Thickness"/> whose left value
    /// is proportional to the item's nesting depth, allowing the selection highlight to
    /// span the full control width while content remains indented.
    /// </summary>
    public sealed class TreeViewItemToIndentConverter : IValueConverter
    {
        private const double IndentSize = 19.0;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is TreeViewItem item)
            {
                var indent = GetDepth(item) * IndentSize;

                // If "Negate" parameter is passed, return negative left margin
                if (parameter is string str && str.Equals("Negate", StringComparison.OrdinalIgnoreCase))
                {
                    return new Thickness(-indent, 0, 0, 0);
                }

                return new Thickness(indent, 0, 0, 0);
            }

            return new Thickness(0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        private static int GetDepth(TreeViewItem item)
        {
            var depth = 0;
            DependencyObject parent = VisualTreeHelper.GetParent(item);

            while (parent != null)
            {
                if (parent is TreeViewItem)
                {
                    depth++;
                }

                if (parent is TreeView)
                {
                    break;
                }

                parent = VisualTreeHelper.GetParent(parent);
            }

            return depth;
        }
    }
}
