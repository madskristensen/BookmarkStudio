using System;
using System.Collections.Generic;
using System.Linq;

namespace BookmarkStudio.Test;

[TestClass]
public class BookmarkRefreshMonitorServiceTests
{
    [TestMethod]
    public void GetRemovableDocumentPaths_WhenFileStillExistsOnDisk_DoesNotRemove()
    {
        // Closing a tab or removing a file from a project while it still exists on
        // disk must not delete its bookmarks (issue #20 - "Close all tabs").
        HashSet<string> result = BookmarkRefreshMonitorService.GetRemovableDocumentPaths(
            new[] { @"C:\repo\open-file.cs" },
            _ => true);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void GetRemovableDocumentPaths_WhenFileDeletedFromDisk_RemovesNormalizedPath()
    {
        HashSet<string> result = BookmarkRefreshMonitorService.GetRemovableDocumentPaths(
            new[] { @"C:\repo\deleted-file.cs" },
            _ => false);

        Assert.AreEqual(1, result.Count);
        Assert.Contains(BookmarkIdentity.NormalizeDocumentPath(@"C:\repo\deleted-file.cs"), result);
    }

    [TestMethod]
    public void GetRemovableDocumentPaths_WhenMixedExistence_OnlyRemovesMissingFiles()
    {
        var missing = @"C:\repo\gone.cs";
        var present = @"C:\repo\still-here.cs";

        HashSet<string> result = BookmarkRefreshMonitorService.GetRemovableDocumentPaths(
            new[] { missing, present },
            path => string.Equals(path, present, StringComparison.Ordinal));

        Assert.AreEqual(1, result.Count);
        Assert.Contains(BookmarkIdentity.NormalizeDocumentPath(missing), result);
        Assert.IsFalse(result.Contains(BookmarkIdentity.NormalizeDocumentPath(present)));
    }

    [TestMethod]
    public void GetRemovableDocumentPaths_WhenPathIsNullOrWhitespace_IsSkipped()
    {
        HashSet<string> result = BookmarkRefreshMonitorService.GetRemovableDocumentPaths(
            new[] { string.Empty, "   ", null! },
            _ => false);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void GetRemovableDocumentPaths_WhenFilePathsNull_ReturnsEmpty()
    {
        HashSet<string> result = BookmarkRefreshMonitorService.GetRemovableDocumentPaths(null!, _ => false);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void GetRemovableDocumentPaths_WhenDuplicatePathsMissing_RemovesSingleNormalizedEntry()
    {
        HashSet<string> result = BookmarkRefreshMonitorService.GetRemovableDocumentPaths(
            new[] { @"C:\repo\gone.cs", @"C:\repo\gone.cs" },
            _ => false);

        Assert.AreEqual(1, result.Count);
    }
}
