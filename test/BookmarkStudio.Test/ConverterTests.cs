using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BookmarkStudio.Test;

[TestClass]
public class ConverterTests
{
    [TestMethod]
    public void TreeViewItemToIndentConverter_ConvertBack_ThrowsNotSupportedException()
    {
        var converter = new TreeViewItemToIndentConverter();

        Assert.ThrowsExactly<NotSupportedException>(() =>
            converter.ConvertBack(new Thickness(10, 0, 0, 0), typeof(object), null, CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void TreeViewItemToIndentConverter_Convert_WhenValueIsNotTreeViewItem_ReturnsZeroThickness()
    {
        var converter = new TreeViewItemToIndentConverter();

        object result = converter.Convert("not a TreeViewItem", typeof(Thickness), null, CultureInfo.InvariantCulture);

        Assert.AreEqual(new Thickness(0), result);
    }

    [TestMethod]
    public void TreeViewItemToIndentConverter_Convert_WhenValueIsNull_ReturnsZeroThickness()
    {
        var converter = new TreeViewItemToIndentConverter();

        object result = converter.Convert(null, typeof(Thickness), null, CultureInfo.InvariantCulture);

        Assert.AreEqual(new Thickness(0), result);
    }

    [TestMethod]
    public void BookmarkColorToBrushConverter_ConvertBack_ThrowsNotSupportedException()
    {
        var converter = new BookmarkColorToBrushConverter();

        Assert.ThrowsExactly<NotSupportedException>(() =>
            converter.ConvertBack(Brushes.Blue, typeof(BookmarkColor), null, CultureInfo.InvariantCulture));
    }
}
