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
}
