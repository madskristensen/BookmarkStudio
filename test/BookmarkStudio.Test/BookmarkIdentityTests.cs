using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BookmarkStudio.Test;

[TestClass]
public class BookmarkIdentityTests
{
    [TestMethod]
    public void NormalizeDocumentPath_WhenPathIsWhitespace_ReturnsEmptyString()
    {
        string result = BookmarkIdentity.NormalizeDocumentPath("   ");

        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void NormalizeDocumentPath_WhenTrailingSeparatorDiffers_ReturnsSameNormalizedPath()
    {
        string root = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string withSeparator = root + Path.DirectorySeparatorChar;

        string normalizedWithoutSeparator = BookmarkIdentity.NormalizeDocumentPath(root);
        string normalizedWithSeparator = BookmarkIdentity.NormalizeDocumentPath(withSeparator);

        Assert.AreEqual(normalizedWithoutSeparator, normalizedWithSeparator);
    }

    [TestMethod]
    public void NormalizeDocumentPath_WhenRelativePathProvided_ReturnsFullPath()
    {
        string result = BookmarkIdentity.NormalizeDocumentPath(".");

        Assert.AreEqual(Path.GetFullPath(".").TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant(), result);
    }

    [TestMethod]
    public void NormalizeLineText_WhenNull_ReturnsEmptyString()
    {
        string result = BookmarkIdentity.NormalizeLineText(null);

        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void CreateExactMatchKey_WhenCalled_UsesNormalizedPathAndLineNumber()
    {
        string result = BookmarkIdentity.CreateExactMatchKey(".\\src\\..\\src", 12);

        StringAssert.EndsWith(result, "|12");
        StringAssert.Contains(result, "SRC");
    }
}
