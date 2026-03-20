using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace BookmarkStudio.Test;

[TestClass]
public class RepositoryAndModelTests
{
    [TestMethod]
    public void GetRequiredBookmark_WhenBookmarkExists_ReturnsBookmark()
    {
        BookmarkMetadata expected = new BookmarkMetadata { BookmarkId = "abc" };

        BookmarkMetadata actual = BookmarkRepositoryService.GetRequiredBookmark(new[] { expected }, "abc");

        Assert.AreSame(expected, actual);
    }

    [TestMethod]
    public void GetRequiredBookmark_WhenBookmarkMissing_Throws()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            BookmarkRepositoryService.GetRequiredBookmark(new[] { new BookmarkMetadata { BookmarkId = "abc" } }, "missing"));
    }

    [TestMethod]
    public void FindBySnapshot_WhenExactMatchKeyMatches_ReturnsBookmark()
    {
        BookmarkSnapshot snapshot = new BookmarkSnapshot { DocumentPath = @"C:\repo\file.cs", LineNumber = 12 };
        BookmarkMetadata expected = new BookmarkMetadata { DocumentPath = @"C:\repo\file.cs", LineNumber = 12 };

        BookmarkMetadata? actual = BookmarkRepositoryService.FindBySnapshot(new[] { expected }, snapshot);

        Assert.AreSame(expected, actual);
    }

    [TestMethod]
    public void ToManagedBookmarks_WhenCalled_OrdersBySlotThenGroupThenPathThenLine()
    {
        BookmarkMetadata[] metadata =
        {
            new BookmarkMetadata { BookmarkId = "3", Group = "B", DocumentPath = @"C:\repo\b.cs", LineNumber = 8, SlotNumber = null },
            new BookmarkMetadata { BookmarkId = "2", Group = "A", DocumentPath = @"C:\repo\a.cs", LineNumber = 20, SlotNumber = 2 },
            new BookmarkMetadata { BookmarkId = "1", Group = "A", DocumentPath = @"C:\repo\a.cs", LineNumber = 10, SlotNumber = 1 },
            new BookmarkMetadata { BookmarkId = "4", Group = "A", DocumentPath = @"C:\repo\a.cs", LineNumber = 2, SlotNumber = null },
        };

        IReadOnlyList<ManagedBookmark> managed = BookmarkRepositoryService.ToManagedBookmarks(metadata);

        CollectionAssert.AreEqual(new[] { "1", "2", "4", "3" }, managed.Select(item => item.BookmarkId).ToArray());
    }

    [TestMethod]
    public void UpdateFromSnapshot_WhenCreatedUtcMissing_InitializesAndCopiesSnapshot()
    {
        DateTime seenUtc = new DateTime(2025, 01, 02, 03, 04, 05, DateTimeKind.Utc);
        BookmarkSnapshot snapshot = new BookmarkSnapshot
        {
            DocumentPath = @"C:\repo\file.cs",
            LineNumber = 42,
            LineText = "  value  ",
        };

        BookmarkMetadata metadata = new BookmarkMetadata();

        metadata.UpdateFromSnapshot(snapshot, seenUtc);

        Assert.AreEqual(snapshot.DocumentPath, metadata.DocumentPath);
        Assert.AreEqual(snapshot.LineNumber, metadata.LineNumber);
        Assert.AreEqual(snapshot.LineText, metadata.LineText);
        Assert.AreEqual(seenUtc, metadata.CreatedUtc);
        Assert.AreEqual(seenUtc, metadata.LastSeenUtc);
    }

    [TestMethod]
    public void ToManagedBookmark_WhenCalled_MapsFieldsAndTrimsLineText()
    {
        DateTime createdUtc = new DateTime(2024, 10, 10, 8, 30, 0, DateTimeKind.Utc);
        DateTime visitedUtc = new DateTime(2024, 10, 11, 8, 30, 0, DateTimeKind.Utc);
        BookmarkMetadata metadata = new BookmarkMetadata
        {
            BookmarkId = "id-1",
            DocumentPath = @"C:\repo\file.cs",
            LineNumber = 15,
            LineText = "  preview text  ",
            SlotNumber = 4,
            Label = "label",
            Group = "group/sub",
            Color = BookmarkColor.Teal,
            CreatedUtc = createdUtc,
            LastVisitedUtc = visitedUtc,
        };

        ManagedBookmark managed = metadata.ToManagedBookmark();

        Assert.AreEqual("id-1", managed.BookmarkId);
        Assert.AreEqual(@"C:\repo\file.cs", managed.DocumentPath);
        Assert.AreEqual(15, managed.LineNumber);
        Assert.AreEqual("preview text", managed.LineText);
        Assert.AreEqual(4, managed.SlotNumber);
        Assert.AreEqual("label", managed.Label);
        Assert.AreEqual("group/sub", managed.Group);
        Assert.AreEqual(BookmarkColor.Teal, managed.Color);
        Assert.AreEqual(createdUtc, managed.CreatedUtc);
        Assert.AreEqual(visitedUtc, managed.LastVisitedUtc);
    }

    [TestMethod]
    public async Task MetadataStore_SaveAndLoadWorkspaceAsync_WhenRoundTripped_PreservesFoldersAndBookmarks()
    {
        BookmarkMetadataStore store = new BookmarkMetadataStore();
        string solutionPath = CreateTestSolutionPath();
        string solutionDirectory = Path.GetDirectoryName(solutionPath)!;

        BookmarkWorkspaceState workspace = new BookmarkWorkspaceState();
        workspace.FolderPaths.Add(string.Empty);
        workspace.FolderPaths.Add("FolderA");
        workspace.FolderPaths.Add("FolderA/Sub");
        workspace.Bookmarks.Add(new BookmarkMetadata
        {
            BookmarkId = "id-1",
            DocumentPath = Path.Combine(solutionDirectory, "src", "file.cs"),
            LineNumber = 23,
            LineText = "preview",
            SlotNumber = 2,
            Label = "label",
            Group = "FolderA/Sub",
            Color = BookmarkColor.Green,
            CreatedUtc = new DateTime(2025, 02, 03, 04, 05, 06, DateTimeKind.Utc),
            LastSeenUtc = new DateTime(2025, 02, 03, 04, 05, 06, DateTimeKind.Utc),
        });

        await store.SaveWorkspaceAsync(solutionPath, workspace, CancellationToken.None);
        BookmarkWorkspaceState loaded = await store.LoadWorkspaceAsync(solutionPath, CancellationToken.None);

        Assert.AreEqual(1, loaded.Bookmarks.Count);
        Assert.IsTrue(loaded.FolderPaths.Contains("FolderA"));
        Assert.IsTrue(loaded.FolderPaths.Contains("FolderA/Sub"));
        Assert.AreEqual("FolderA/Sub", loaded.Bookmarks[0].Group);
        Assert.AreEqual(23, loaded.Bookmarks[0].LineNumber);
    }

    [TestMethod]
    public async Task MetadataStore_LoadWorkspaceAsync_WhenLegacyBookmarksFormat_LoadsAndNormalizes()
    {
        BookmarkMetadataStore store = new BookmarkMetadataStore();
        string solutionPath = CreateTestSolutionPath();
        string solutionDirectory = Path.GetDirectoryName(solutionPath)!;
        string storagePath = store.GetStoragePath(solutionPath);
        Directory.CreateDirectory(Path.GetDirectoryName(storagePath)!);
        File.WriteAllText(storagePath,
            "{\"bookmarks\":[{\"id\":\"id-1\",\"documentPath\":\"src\\\\file.cs\",\"lineNumber\":3,\"columnNumber\":11,\"lineText\":\"x\",\"group\":\"Legacy/Sub\",\"color\":\"orange\"}]}");

        BookmarkWorkspaceState loaded = await store.LoadWorkspaceAsync(solutionPath, CancellationToken.None);

        Assert.AreEqual(1, loaded.Bookmarks.Count);
        Assert.AreEqual(Path.Combine(solutionDirectory, "src", "file.cs"), loaded.Bookmarks[0].DocumentPath);
        Assert.AreEqual("Legacy/Sub", loaded.Bookmarks[0].Group);
        Assert.IsTrue(loaded.FolderPaths.Contains("Legacy"));
        Assert.IsTrue(loaded.FolderPaths.Contains("Legacy/Sub"));
    }

    [TestMethod]
    public async Task ToggleAsync_WhenSnapshotNotFound_AddsBookmarkWithLowestAvailableSlot()
    {
        BookmarkMetadataStore store = new BookmarkMetadataStore();
        BookmarkRepositoryService repository = new BookmarkRepositoryService(store);
        string solutionPath = CreateTestSolutionPath();
        DateTime now = new DateTime(2025, 03, 04, 12, 00, 00, DateTimeKind.Utc);

        await store.SaveAsync(solutionPath, new[]
        {
            new BookmarkMetadata { BookmarkId = "a", DocumentPath = @"C:\repo\a.cs", LineNumber = 1, SlotNumber = 1, LastSeenUtc = now },
            new BookmarkMetadata { BookmarkId = "b", DocumentPath = @"C:\repo\b.cs", LineNumber = 2, SlotNumber = 2, LastSeenUtc = now },
            new BookmarkMetadata { BookmarkId = "c", DocumentPath = @"C:\repo\c.cs", LineNumber = 3, SlotNumber = 4, LastSeenUtc = now },
        }, CancellationToken.None);

        BookmarkSnapshot snapshot = new BookmarkSnapshot
        {
            DocumentPath = @"C:\repo\new.cs",
            LineNumber = 10,
            LineText = "line",
        };

        ManagedBookmark? added = await repository.ToggleAsync(solutionPath, snapshot, CancellationToken.None);

        Assert.IsNotNull(added);
        Assert.AreEqual(3, added.SlotNumber);
        Assert.AreEqual("Bookmark1", added.Label);
    }

    [TestMethod]
    public async Task ToggleAsync_WhenDefaultBookmarkLabelsExist_AssignsNextAvailableDefaultLabel()
    {
        BookmarkMetadataStore store = new BookmarkMetadataStore();
        BookmarkRepositoryService repository = new BookmarkRepositoryService(store);
        string solutionPath = CreateTestSolutionPath();

        await store.SaveAsync(solutionPath, new[]
        {
            new BookmarkMetadata { BookmarkId = "a", DocumentPath = @"C:\repo\a.cs", LineNumber = 1, Label = "Bookmark1" },
            new BookmarkMetadata { BookmarkId = "b", DocumentPath = @"C:\repo\b.cs", LineNumber = 2, Label = "Bookmark2" },
            new BookmarkMetadata { BookmarkId = "c", DocumentPath = @"C:\repo\c.cs", LineNumber = 3, Label = "Other" },
        }, CancellationToken.None);

        ManagedBookmark? added = await repository.ToggleAsync(solutionPath, new BookmarkSnapshot
        {
            DocumentPath = @"C:\repo\new.cs",
            LineNumber = 100,
            LineText = "line",
        }, CancellationToken.None);

        Assert.IsNotNull(added);
        Assert.AreEqual("Bookmark3", added.Label);
    }

    [TestMethod]
    public void Repository_DeleteFolderRecursive_RemovesNestedFoldersAndBookmarks()
    {
        BookmarkWorkspaceState workspace = new BookmarkWorkspaceState();
        workspace.FolderPaths.Add(string.Empty);
        workspace.FolderPaths.Add("A");
        workspace.FolderPaths.Add("A/B");
        workspace.FolderPaths.Add("A/B/C");
        workspace.FolderPaths.Add("X");
        workspace.Bookmarks.Add(new BookmarkMetadata { BookmarkId = "1", Group = "A/B" });
        workspace.Bookmarks.Add(new BookmarkMetadata { BookmarkId = "2", Group = "A/B/C" });
        workspace.Bookmarks.Add(new BookmarkMetadata { BookmarkId = "3", Group = "X" });

        bool changed = BookmarkRepositoryService.DeleteFolderRecursive(workspace, "A/B");

        Assert.IsTrue(changed);
        Assert.AreEqual(1, workspace.Bookmarks.Count);
        Assert.AreEqual("3", workspace.Bookmarks[0].BookmarkId);
        Assert.IsFalse(workspace.FolderPaths.Contains("A/B"));
        Assert.IsFalse(workspace.FolderPaths.Contains("A/B/C"));
        Assert.IsTrue(workspace.FolderPaths.Contains("X"));
    }

    [TestMethod]
    public void Repository_EnsureFolderPath_WhenNestedPathProvided_RegistersNormalizedAncestors()
    {
        BookmarkWorkspaceState workspace = new BookmarkWorkspaceState();

        BookmarkRepositoryService.EnsureFolderPath(workspace, @" Team\\Backlog/Sprint1 ");

        Assert.IsTrue(workspace.FolderPaths.Contains("Team"));
        Assert.IsTrue(workspace.FolderPaths.Contains("Team/Backlog"));
        Assert.IsTrue(workspace.FolderPaths.Contains("Team/Backlog/Sprint1"));
    }

    [TestMethod]
    public void Repository_RenameFolder_WhenRenamingNestedFolder_UpdatesDescendantsAndBookmarks()
    {
        BookmarkWorkspaceState workspace = new BookmarkWorkspaceState();
        workspace.FolderPaths.Add(string.Empty);
        workspace.FolderPaths.Add("Team");
        workspace.FolderPaths.Add("Team/Backlog");
        workspace.FolderPaths.Add("Team/Backlog/Sprint1");
        workspace.FolderPaths.Add("Other");
        workspace.Bookmarks.Add(new BookmarkMetadata { BookmarkId = "1", Group = "Team/Backlog" });
        workspace.Bookmarks.Add(new BookmarkMetadata { BookmarkId = "2", Group = "Team/Backlog/Sprint1" });
        workspace.Bookmarks.Add(new BookmarkMetadata { BookmarkId = "3", Group = "Other" });

        bool renamed = BookmarkRepositoryService.RenameFolder(workspace, "Team/Backlog", "Done");

        Assert.IsTrue(renamed);
        Assert.IsTrue(workspace.FolderPaths.Contains("Team/Done"));
        Assert.IsTrue(workspace.FolderPaths.Contains("Team/Done/Sprint1"));
        Assert.IsFalse(workspace.FolderPaths.Contains("Team/Backlog"));
        Assert.IsFalse(workspace.FolderPaths.Contains("Team/Backlog/Sprint1"));
        Assert.AreEqual("Team/Done", workspace.Bookmarks.Single(item => item.BookmarkId == "1").Group);
        Assert.AreEqual("Team/Done/Sprint1", workspace.Bookmarks.Single(item => item.BookmarkId == "2").Group);
        Assert.AreEqual("Other", workspace.Bookmarks.Single(item => item.BookmarkId == "3").Group);
    }

    [TestMethod]
    public void Repository_RenameFolder_WhenTargetMatchesSourceCaseInsensitive_ReturnsFalseAndDoesNotChangeState()
    {
        BookmarkWorkspaceState workspace = new BookmarkWorkspaceState();
        workspace.FolderPaths.Add(string.Empty);
        workspace.FolderPaths.Add("Team");
        workspace.FolderPaths.Add("Team/Backlog");
        workspace.Bookmarks.Add(new BookmarkMetadata { BookmarkId = "1", Group = "Team/Backlog" });

        bool renamed = BookmarkRepositoryService.RenameFolder(workspace, "Team/Backlog", "BACKLOG");

        Assert.IsFalse(renamed);
        Assert.IsTrue(workspace.FolderPaths.Contains("Team/Backlog"));
        Assert.AreEqual("Team/Backlog", workspace.Bookmarks.Single(item => item.BookmarkId == "1").Group);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Repository_DeleteFolderRecursive_WhenFolderPathMissing_ReturnsFalseAndPreservesState(string folderPath)
    {
        BookmarkWorkspaceState workspace = new BookmarkWorkspaceState();
        workspace.FolderPaths.Add(string.Empty);
        workspace.FolderPaths.Add("A");
        workspace.Bookmarks.Add(new BookmarkMetadata { BookmarkId = "1", Group = "A" });

        bool changed = BookmarkRepositoryService.DeleteFolderRecursive(workspace, folderPath);

        Assert.IsFalse(changed);
        Assert.AreEqual(1, workspace.Bookmarks.Count);
        Assert.IsTrue(workspace.FolderPaths.Contains("A"));
    }

    [TestMethod]
    public void Repository_MoveBookmarkToFolder_WhenNestedFolderProvided_RegistersAncestors()
    {
        BookmarkWorkspaceState workspace = new BookmarkWorkspaceState();
        workspace.FolderPaths.Add(string.Empty);
        workspace.Bookmarks.Add(new BookmarkMetadata { BookmarkId = "1", Group = string.Empty });

        BookmarkRepositoryService.MoveBookmarkToFolder(workspace, "1", "Feature/Area/Task");

        Assert.AreEqual("Feature/Area/Task", workspace.Bookmarks.Single(item => item.BookmarkId == "1").Group);
        Assert.IsTrue(workspace.FolderPaths.Contains("Feature"));
        Assert.IsTrue(workspace.FolderPaths.Contains("Feature/Area"));
        Assert.IsTrue(workspace.FolderPaths.Contains("Feature/Area/Task"));
    }

    [TestMethod]
    public void MetadataStore_GetStoragePath_WhenSolutionPathWhitespace_UsesTransientPath()
    {
        BookmarkMetadataStore store = new BookmarkMetadataStore();

        string path = store.GetStoragePath(" ");

        string expectedRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BookmarkStudio",
            "Transient");
        StringAssert.StartsWith(path, expectedRoot);
        StringAssert.EndsWith(path, "bookmarks.json");
    }

    private static string CreateTestSolutionPath()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "BookmarkStudio.Tests", Guid.NewGuid().ToString("N"));
        return Path.Combine(testRoot, "demo.sln");
    }
}