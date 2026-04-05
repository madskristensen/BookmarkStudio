namespace BookmarkStudio.Test;

[TestClass]
public class BookmarkTextBufferTrackingTests
{
    [TestMethod]
    public void GetFirstNonWhitespaceOffset_WhenLineHasLeadingSpaces_ReturnsFirstContentOffset()
    {
        int offset = BookmarkTextBufferTracker.GetFirstNonWhitespaceOffset("        Console.WriteLine(\"test\");");

        Assert.AreEqual(8, offset);
    }

    [TestMethod]
    public void GetFirstNonWhitespaceOffset_WhenLineHasNoLeadingWhitespace_ReturnsZero()
    {
        int offset = BookmarkTextBufferTracker.GetFirstNonWhitespaceOffset("namespace SomeNamespace;");

        Assert.AreEqual(0, offset);
    }

    [TestMethod]
    public void GetFirstNonWhitespaceOffset_WhenLineIsEmpty_ReturnsZero()
    {
        int offset = BookmarkTextBufferTracker.GetFirstNonWhitespaceOffset("");

        Assert.AreEqual(0, offset);
    }

    [TestMethod]
    public void GetFirstNonWhitespaceOffset_WhenLineIsNull_ReturnsZero()
    {
        int offset = BookmarkTextBufferTracker.GetFirstNonWhitespaceOffset(null!);

        Assert.AreEqual(0, offset);
    }

    [TestMethod]
    public void GetFirstNonWhitespaceOffset_WhenLineIsAllWhitespace_ReturnsZero()
    {
        int offset = BookmarkTextBufferTracker.GetFirstNonWhitespaceOffset("        ");

        Assert.AreEqual(0, offset);
    }

    [TestMethod]
    public void GetFirstNonWhitespaceOffset_WhenLineHasTabs_ReturnsFirstContentOffset()
    {
        int offset = BookmarkTextBufferTracker.GetFirstNonWhitespaceOffset("\t\tvar x = 1;");

        Assert.AreEqual(2, offset);
    }

    [TestMethod]
    public void GetFirstNonWhitespaceOffset_WhenLineHasMixedWhitespace_ReturnsFirstContentOffset()
    {
        int offset = BookmarkTextBufferTracker.GetFirstNonWhitespaceOffset("  \t  {");

        Assert.AreEqual(5, offset);
    }

    [TestMethod]
    public void GetFirstNonWhitespaceOffset_WhenLineIsSingleChar_ReturnsZero()
    {
        int offset = BookmarkTextBufferTracker.GetFirstNonWhitespaceOffset("{");

        Assert.AreEqual(0, offset);
    }

    [TestMethod]
    public void GetFirstNonWhitespaceOffset_WhenLineHasFourSpaceIndent_ReturnsFour()
    {
        int offset = BookmarkTextBufferTracker.GetFirstNonWhitespaceOffset("    public static void DoWork()");

        Assert.AreEqual(4, offset);
    }
}
