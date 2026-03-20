using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace BookmarkStudio
{
    internal sealed class BookmarkColorToBrushConverter : IValueConverter
    {
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
            switch (color)
            {
                case BookmarkColor.Red: return new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35));
                case BookmarkColor.Orange: return new SolidColorBrush(Color.FromRgb(0xFB, 0x8C, 0x00));
                case BookmarkColor.Yellow: return new SolidColorBrush(Color.FromRgb(0xFD, 0xD8, 0x35));
                case BookmarkColor.Green: return new SolidColorBrush(Color.FromRgb(0x43, 0xA0, 0x47));
                case BookmarkColor.Blue: return new SolidColorBrush(Color.FromRgb(0x1E, 0x88, 0xE5));
                case BookmarkColor.Purple: return new SolidColorBrush(Color.FromRgb(0x8E, 0x24, 0xAA));
                case BookmarkColor.Pink: return new SolidColorBrush(Color.FromRgb(0xEC, 0x40, 0x7A));
                case BookmarkColor.Teal: return new SolidColorBrush(Color.FromRgb(0x00, 0x89, 0x7B));
                default: return new SolidColorBrush(Color.FromRgb(0xFB, 0x8C, 0x00));
            }
        }
    }
}
