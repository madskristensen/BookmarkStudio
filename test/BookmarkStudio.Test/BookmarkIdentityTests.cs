using System.IO;

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
    public void NormalizeFolderPath_WhenValueIsRootAlias_ReturnsEmptyString()
    {
        string result = BookmarkIdentity.NormalizeFolderPath("Root");

        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void NormalizeFolderPath_WhenValueStartsWithRootAlias_StripsAlias()
    {
        string result = BookmarkIdentity.NormalizeFolderPath("Root/Team/Sub");

        Assert.AreEqual("Team/Sub", result);
    }

    [TestMethod]
    public void CreateExactMatchKey_WhenCalled_UsesNormalizedPathAndLineNumber()
    {
        string result = BookmarkIdentity.CreateExactMatchKey(".\\src\\..\\src", 12);

        StringAssert.EndsWith(result, "|12");
        StringAssert.Contains(result, "SRC");
    }

    [TestMethod]
    public void GetRepositoryRelativePath_WhenGitRootExists_ReturnsRepoRelativePath()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "BookmarkStudio.Tests", Guid.NewGuid().ToString("N"));
        string repoRoot = Path.Combine(testRoot, "repo");
        string filePath = Path.Combine(repoRoot, "src", "file.cs");

        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, "// test");

        string result = BookmarkIdentity.GetRepositoryRelativePath(filePath);

        Assert.AreEqual("src/file.cs", result);
    }

    [TestMethod]
    public void GetRepositoryRelativePath_WhenPathIsOutsideRepository_ReturnsFileName()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "BookmarkStudio.Tests", Guid.NewGuid().ToString("N"));
        string filePath = Path.Combine(testRoot, "outside", "file.cs");

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, "// test");

        string result = BookmarkIdentity.GetRepositoryRelativePath(filePath);

        Assert.AreEqual("file.cs", result);
    }

    [TestMethod]
    public void NormalizeDocumentPath_WhenNull_ReturnsEmptyString()
    {
        string result = BookmarkIdentity.NormalizeDocumentPath(null);

        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void NormalizeDocumentPath_WhenCasingDiffers_ReturnsSameNormalizedPath()
    {
        string lower = Path.Combine(Path.GetTempPath(), "test", "file.cs");
        string upper = Path.Combine(Path.GetTempPath(), "TEST", "FILE.CS");

        string normalizedLower = BookmarkIdentity.NormalizeDocumentPath(lower);
        string normalizedUpper = BookmarkIdentity.NormalizeDocumentPath(upper);

        Assert.AreEqual(normalizedLower, normalizedUpper);
    }

    [TestMethod]
    public void NormalizeFolderPath_WhenNull_ReturnsEmptyString()
    {
        string result = BookmarkIdentity.NormalizeFolderPath(null);

        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void NormalizeFolderPath_WhenWhitespaceOnly_ReturnsEmptyString()
    {
        string result = BookmarkIdentity.NormalizeFolderPath("   \t\n  ");

        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void NormalizeFolderPath_WhenBackslashesUsed_ConvertsToForwardSlashes()
    {
        string result = BookmarkIdentity.NormalizeFolderPath(@"Features\Auth\Login");

        Assert.AreEqual("Features/Auth/Login", result);
    }

    [TestMethod]
    public void NormalizeFolderPath_WhenMixedSeparators_NormalizesToForwardSlashes()
    {
        string result = BookmarkIdentity.NormalizeFolderPath(@"Features/Auth\Login/Views");

        Assert.AreEqual("Features/Auth/Login/Views", result);
    }

    [TestMethod]
    public void NormalizeFolderPath_WhenEmptySegmentsExist_RemovesThem()
    {
        string result = BookmarkIdentity.NormalizeFolderPath("Features//Auth///Login");

        Assert.AreEqual("Features/Auth/Login", result);
    }

    [TestMethod]
    public void NormalizeFolderPath_WhenLeadingOrTrailingSlashes_TrimsThem()
    {
        string result = BookmarkIdentity.NormalizeFolderPath("/Features/Auth/");

        Assert.AreEqual("Features/Auth", result);
    }

    [TestMethod]
    public void NormalizeFolderPath_WhenRootAliasHasDifferentCasing_StillStripsIt()
    {
        string result = BookmarkIdentity.NormalizeFolderPath("ROOT/Team/Sub");

        Assert.AreEqual("Team/Sub", result);
    }

    [TestMethod]
    public void NormalizeLineText_WhenWhitespaceAround_TrimsWhitespace()
    {
        string result = BookmarkIdentity.NormalizeLineText("  \t code here  \n ");

        Assert.AreEqual("code here", result);
    }

    [TestMethod]
    public void CreateExactMatchKey_WhenLineNumberIsZero_IncludesZero()
    {
        string result = BookmarkIdentity.CreateExactMatchKey(@"C:\file.cs", 0);

        StringAssert.EndsWith(result, "|0");
    }

    [TestMethod]
    public void CreateExactMatchKey_WhenSamePathAndLine_ProducesSameKey()
    {
        string key1 = BookmarkIdentity.CreateExactMatchKey(@"C:\repo\src\file.cs", 42);
        string key2 = BookmarkIdentity.CreateExactMatchKey(@"c:\REPO\SRC\FILE.CS", 42);

        Assert.AreEqual(key1, key2);
    }

    [TestMethod]
    public void CreateExactMatchKey_WhenDifferentLines_ProducesDifferentKeys()
    {
        string key1 = BookmarkIdentity.CreateExactMatchKey(@"C:\file.cs", 10);
        string key2 = BookmarkIdentity.CreateExactMatchKey(@"C:\file.cs", 11);

        Assert.AreNotEqual(key1, key2);
    }

    [TestMethod]
    public void GetRepositoryRelativePath_WhenNullPath_ReturnsEmptyString()
    {
        string result = BookmarkIdentity.GetRepositoryRelativePath(null);

        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void GetRepositoryRelativePath_WhenEmptyPath_ReturnsEmptyString()
    {
        string result = BookmarkIdentity.GetRepositoryRelativePath(string.Empty);

        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void GetRepositoryRelativePath_WhenNestedInRepo_ReturnsFullRelativePath()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "BookmarkStudio.Tests", Guid.NewGuid().ToString("N"));
        string repoRoot = Path.Combine(testRoot, "repo");
        string filePath = Path.Combine(repoRoot, "src", "Features", "Auth", "LoginService.cs");

        Directory.CreateDirectory(Path.Combine(repoRoot, ".git"));
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, "// test");

        try
        {
            string result = BookmarkIdentity.GetRepositoryRelativePath(filePath);

            Assert.AreEqual("src/Features/Auth/LoginService.cs", result);
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }
}
