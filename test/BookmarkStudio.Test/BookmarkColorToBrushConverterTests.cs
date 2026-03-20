using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using System.Windows.Media;

namespace BookmarkStudio.Test;

[TestClass]
public class BookmarkColorToBrushConverterTests
{
    [TestMethod]
    public void GetBrush_WhenColorValueIsUnknown_FallsBackToOrange()
    {
        Brush fallback = BookmarkColorToBrushConverter.GetBrush((BookmarkColor)999);
        Brush orange = BookmarkColorToBrushConverter.GetBrush(BookmarkColor.Orange);

        Assert.AreEqual(orange, fallback);
    }

    [TestMethod]
    public void Convert_WhenValueIsNotBookmarkColor_ReturnsTransparentBrush()
    {
        BookmarkColorToBrushConverter converter = new BookmarkColorToBrushConverter();

        object result = converter.Convert("not-a-color", typeof(Brush), null, CultureInfo.InvariantCulture);

        Assert.AreEqual(Brushes.Transparent, result);
    }
}
