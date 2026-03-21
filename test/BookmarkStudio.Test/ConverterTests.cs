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
        TreeViewItemToIndentConverter converter = new TreeViewItemToIndentConverter();

        Assert.ThrowsExactly<NotSupportedException>(() =>
            converter.ConvertBack(new Thickness(10, 0, 0, 0), typeof(object), null, CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void TreeViewItemToIndentConverter_Convert_WhenValueIsNotTreeViewItem_ReturnsZeroThickness()
    {
        TreeViewItemToIndentConverter converter = new TreeViewItemToIndentConverter();

        object result = converter.Convert("not a TreeViewItem", typeof(Thickness), null, CultureInfo.InvariantCulture);

        Assert.AreEqual(new Thickness(0), result);
    }

    [TestMethod]
    public void TreeViewItemToIndentConverter_Convert_WhenValueIsNull_ReturnsZeroThickness()
    {
        TreeViewItemToIndentConverter converter = new TreeViewItemToIndentConverter();

        object result = converter.Convert(null, typeof(Thickness), null, CultureInfo.InvariantCulture);

        Assert.AreEqual(new Thickness(0), result);
    }

    [TestMethod]
    public void BookmarkColorToBrushConverter_ConvertBack_ThrowsNotSupportedException()
    {
        BookmarkColorToBrushConverter converter = new BookmarkColorToBrushConverter();

        Assert.ThrowsExactly<NotSupportedException>(() =>
            converter.ConvertBack(Brushes.Blue, typeof(BookmarkColor), null, CultureInfo.InvariantCulture));
    }
}
