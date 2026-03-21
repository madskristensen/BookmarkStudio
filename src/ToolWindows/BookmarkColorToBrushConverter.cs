using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace BookmarkStudio
{
    internal sealed class BookmarkColorToBrushConverter : IValueConverter
    {
        private static readonly IReadOnlyDictionary<BookmarkColor, Brush> BrushesByColor = CreateBrushesByColor();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is BookmarkColor color)
            {
                return GetBrush(color);
            }

            return Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();

        internal static Brush GetBrush(BookmarkColor color)
        {
            if (BrushesByColor.TryGetValue(color, out Brush brush))
            {
                return brush;
            }

            return BrushesByColor[BookmarkColor.Orange];
        }

        private static IReadOnlyDictionary<BookmarkColor, Brush> CreateBrushesByColor()
        {
            return new Dictionary<BookmarkColor, Brush>
            {
                { BookmarkColor.Red, CreateBrush(0xE5, 0x39, 0x35) },
                { BookmarkColor.Orange, CreateBrush(0xFB, 0x8C, 0x00) },
                { BookmarkColor.Yellow, CreateBrush(0xFD, 0xD8, 0x35) },
                { BookmarkColor.Green, CreateBrush(0x43, 0xA0, 0x47) },
                { BookmarkColor.Blue, CreateBrush(0x1E, 0x88, 0xE5) },
                { BookmarkColor.Purple, CreateBrush(0x8E, 0x24, 0xAA) },
                { BookmarkColor.Pink, CreateBrush(0xEC, 0x40, 0x7A) },
                { BookmarkColor.Teal, CreateBrush(0x00, 0x89, 0x7B) },
            };
        }

        private static Brush CreateBrush(byte r, byte g, byte b)
        {
            SolidColorBrush brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }

    internal sealed class BoolToVisibilityInverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue && boolValue)
            {
                return System.Windows.Visibility.Collapsed;
            }

            return System.Windows.Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    internal sealed class SlotNumberToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int)
            {
                return System.Windows.Visibility.Visible;
            }

            return System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}