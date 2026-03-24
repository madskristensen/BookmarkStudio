using System.IO;
using System.Linq;

namespace BookmarkStudio.Test;

[TestClass]
public class RepositoryAndModelTests
{
    [TestMethod]
    public void GetRequiredBookmark_WhenBookmarkExists_ReturnsBookmark()
    {
        var expected = new BookmarkMetadata { BookmarkId = "abc" };

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
        var snapshot = new BookmarkSnapshot { DocumentPath = @"C:\repo\file.cs", LineNumber = 12 };
        var expected = new BookmarkMetadata { DocumentPath = @"C:\repo\file.cs", LineNumber = 12 };

        BookmarkMetadata? actual = BookmarkRepositoryService.FindBySnapshot(new[] { expected }, snapshot);

        Assert.AreSame(expected, actual);
    }

    [TestMethod]
    public void ToManagedBookmarks_WhenCalled_OrdersBySlotThenGroupThenPathThenLine()
    {
        BookmarkMetadata[] metadata =
        [
            new() { BookmarkId = "3", Group = "B", DocumentPath = @"C:\repo\b.cs", LineNumber = 8, ShortcutNumber = null },
            new() { BookmarkId = "2", Group = "A", DocumentPath = @"C:\repo\a.cs", LineNumber = 20, ShortcutNumber = 2 },
            new() { BookmarkId = "1", Group = "A", DocumentPath = @"C:\repo\a.cs", LineNumber = 10, ShortcutNumber = 1 },
            new() { BookmarkId = "4", Group = "A", DocumentPath = @"C:\repo\a.cs", LineNumber = 2, ShortcutNumber = null },
        ];

        IReadOnlyList<ManagedBookmark> managed = BookmarkRepositoryService.ToManagedBookmarks(metadata);

        CollectionAssert.AreEqual(new[] { "1", "2", "4", "3" }, managed.Select(item => item.BookmarkId).ToArray());
    }

    [TestMethod]
    public void UpdateFromSnapshot_WhenCalled_CopiesSnapshotFields()
    {
        var snapshot = new BookmarkSnapshot
        {
            DocumentPath = @"C:\repo\file.cs",
            LineNumber = 42,
            LineText = "  value  ",
        };

        var metadata = new BookmarkMetadata();

        metadata.UpdateFromSnapshot(snapshot);

        Assert.AreEqual(snapshot.DocumentPath, metadata.DocumentPath);
        Assert.AreEqual(snapshot.LineNumber, metadata.LineNumber);
        Assert.AreEqual(snapshot.LineText, metadata.LineText);
    }

    [TestMethod]
    public void ToManagedBookmark_WhenCalled_MapsFieldsAndTrimsLineText()
    {
        var metadata = new BookmarkMetadata
        {
            BookmarkId = "id-1",
            DocumentPath = @"C:\repo\file.cs",
            LineNumber = 15,
            LineText = "  preview text  ",
            ShortcutNumber = 4,
            Label = "label",
            Group = "group/sub",
            Color = BookmarkColor.Teal,
        };

        var managed = metadata.ToManagedBookmark();

        Assert.AreEqual("id-1", managed.BookmarkId);
        Assert.AreEqual(@"C:\repo\file.cs", managed.DocumentPath);
        Assert.AreEqual(15, managed.LineNumber);
        Assert.AreEqual("preview text", managed.LineText);
        Assert.AreEqual(4, managed.ShortcutNumber);
        Assert.AreEqual("label", managed.Label);
        Assert.AreEqual("group/sub", managed.Group);
        Assert.AreEqual(BookmarkColor.Teal, managed.Color);
    }

    [TestMethod]
    public async Task MetadataStore_SaveAndLoadWorkspaceAsync_WhenRoundTripped_PreservesFoldersAndBookmarks()
    {
        var store = new BookmarkMetadataStore();
        string solutionPath = CreateTestSolutionPath();
        string solutionDirectory = Path.GetDirectoryName(solutionPath)!;

        var workspace = new BookmarkWorkspaceState();
        workspace.FolderPaths.Add(string.Empty);
        workspace.FolderPaths.Add("FolderA");
        workspace.FolderPaths.Add("FolderA/Sub");
        workspace.Bookmarks.Add(new BookmarkMetadata
        {
            BookmarkId = "id-1",
            DocumentPath = Path.Combine(solutionDirectory, "src", "file.cs"),
            LineNumber = 23,
            LineText = "preview",
            ShortcutNumber = 2,
            Label = "label",
            Group = "FolderA/Sub",
            Color = BookmarkColor.Green,
        });

        await store.SaveWorkspaceAsync(solutionPath, workspace, CancellationToken.None);
        BookmarkWorkspaceState loaded = await store.LoadWorkspaceAsync(solutionPath, CancellationToken.None);

        Assert.HasCount(1, loaded.Bookmarks);
        Assert.Contains("FolderA", loaded.FolderPaths);
        Assert.Contains("FolderA/Sub", loaded.FolderPaths);
        Assert.AreEqual("FolderA/Sub", loaded.Bookmarks[0].Group);
        Assert.AreEqual(23, loaded.Bookmarks[0].LineNumber);
    }

    [TestMethod]
    public async Task MetadataStore_LoadWorkspaceAsync_WhenLegacyBookmarksFormat_LoadsAndNormalizes()
    {
        var store = new BookmarkMetadataStore();
        string solutionPath = CreateTestSolutionPath();
        string solutionDirectory = Path.GetDirectoryName(solutionPath)!;
        string storagePath = store.GetStoragePath(solutionPath);
        Directory.CreateDirectory(Path.GetDirectoryName(storagePath)!);
        File.WriteAllText(storagePath,
            "{\"bookmarks\":[{\"id\":\"id-1\",\"documentPath\":\"src\\\\file.cs\",\"lineNumber\":3,\"columnNumber\":11,\"lineText\":\"x\",\"group\":\"Legacy/Sub\",\"color\":\"orange\"}]}");

        BookmarkWorkspaceState loaded = await store.LoadWorkspaceAsync(solutionPath, CancellationToken.None);

        Assert.HasCount(1, loaded.Bookmarks);
        Assert.AreEqual(Path.Combine(solutionDirectory, "src", "file.cs"), loaded.Bookmarks[0].DocumentPath);
        Assert.AreEqual("Legacy/Sub", loaded.Bookmarks[0].Group);
        Assert.Contains("Legacy", loaded.FolderPaths);
        Assert.Contains("Legacy/Sub", loaded.FolderPaths);
    }

    [TestMethod]
    public async Task ToggleAsync_WhenSnapshotNotFound_AddsBookmarkWithLowestAvailableSlot()
    {
        var store = new BookmarkMetadataStore();
        var repository = new BookmarkRepositoryService(store);
        string solutionPath = CreateTestSolutionPath();

        await store.SaveAsync(solutionPath, new[]
        {
            new BookmarkMetadata { BookmarkId = "a", DocumentPath = @"C:\repo\a.cs", LineNumber = 1, ShortcutNumber = 1 },
            new BookmarkMetadata { BookmarkId = "b", DocumentPath = @"C:\repo\b.cs", LineNumber = 2, ShortcutNumber = 2 },
            new BookmarkMetadata { BookmarkId = "c", DocumentPath = @"C:\repo\c.cs", LineNumber = 3, ShortcutNumber = 4 },
        }, CancellationToken.None);

        var snapshot = new BookmarkSnapshot
        {
            DocumentPath = @"C:\repo\new.cs",
            LineNumber = 10,
            LineText = "line",
        };

        ManagedBookmark? added = await repository.ToggleAsync(solutionPath, snapshot, CancellationToken.None);

        Assert.IsNotNull(added);
        Assert.AreEqual(3, added.ShortcutNumber);
        Assert.AreEqual("Bookmark1", added.Label);
    }

    [TestMethod]
    public async Task ToggleAsync_WhenDefaultBookmarkLabelsExist_AssignsNextAvailableDefaultLabel()
    {
        var store = new BookmarkMetadataStore();
        var repository = new BookmarkRepositoryService(store);
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
        var workspace = new BookmarkWorkspaceState();
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
        Assert.HasCount(1, workspace.Bookmarks);
        Assert.AreEqual("3", workspace.Bookmarks[0].BookmarkId);
        Assert.DoesNotContain("A/B", workspace.FolderPaths);
        Assert.DoesNotContain("A/B/C", workspace.FolderPaths);
        Assert.Contains("X", workspace.FolderPaths);
    }

    [TestMethod]
    public void Repository_EnsureFolderPath_WhenNestedPathProvided_RegistersNormalizedAncestors()
    {
        var workspace = new BookmarkWorkspaceState();

        BookmarkRepositoryService.EnsureFolderPath(workspace, @" Team\\Backlog/Sprint1 ");

        Assert.Contains("Team", workspace.FolderPaths);
        Assert.Contains("Team/Backlog", workspace.FolderPaths);
        Assert.Contains("Team/Backlog/Sprint1", workspace.FolderPaths);
    }

    [TestMethod]
    public void Repository_RenameFolder_WhenRenamingNestedFolder_UpdatesDescendantsAndBookmarks()
    {
        var workspace = new BookmarkWorkspaceState();
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
        Assert.Contains("Team/Done", workspace.FolderPaths);
        Assert.Contains("Team/Done/Sprint1", workspace.FolderPaths);
        Assert.DoesNotContain("Team/Backlog", workspace.FolderPaths);
        Assert.DoesNotContain("Team/Backlog/Sprint1", workspace.FolderPaths);
        Assert.AreEqual("Team/Done", workspace.Bookmarks.Single(item => item.BookmarkId == "1").Group);
        Assert.AreEqual("Team/Done/Sprint1", workspace.Bookmarks.Single(item => item.BookmarkId == "2").Group);
        Assert.AreEqual("Other", workspace.Bookmarks.Single(item => item.BookmarkId == "3").Group);
    }

    [TestMethod]
    public void Repository_RenameFolder_WhenTargetMatchesSourceCaseInsensitive_ReturnsFalseAndDoesNotChangeState()
    {
        var workspace = new BookmarkWorkspaceState();
        workspace.FolderPaths.Add(string.Empty);
        workspace.FolderPaths.Add("Team");
        workspace.FolderPaths.Add("Team/Backlog");
        workspace.Bookmarks.Add(new BookmarkMetadata { BookmarkId = "1", Group = "Team/Backlog" });

        bool renamed = BookmarkRepositoryService.RenameFolder(workspace, "Team/Backlog", "BACKLOG");

        Assert.IsFalse(renamed);
        Assert.Contains("Team/Backlog", workspace.FolderPaths);
        Assert.AreEqual("Team/Backlog", workspace.Bookmarks.Single(item => item.BookmarkId == "1").Group);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Repository_DeleteFolderRecursive_WhenFolderPathMissing_ReturnsFalseAndPreservesState(string folderPath)
    {
        var workspace = new BookmarkWorkspaceState();
        workspace.FolderPaths.Add(string.Empty);
        workspace.FolderPaths.Add("A");
        workspace.Bookmarks.Add(new BookmarkMetadata { BookmarkId = "1", Group = "A" });

        bool changed = BookmarkRepositoryService.DeleteFolderRecursive(workspace, folderPath);

        Assert.IsFalse(changed);
        Assert.HasCount(1, workspace.Bookmarks);
        Assert.Contains("A", workspace.FolderPaths);
    }

    [TestMethod]
    public void Repository_MoveBookmarkToFolder_WhenNestedFolderProvided_RegistersAncestors()
    {
        var workspace = new BookmarkWorkspaceState();
        workspace.FolderPaths.Add(string.Empty);
        workspace.Bookmarks.Add(new BookmarkMetadata { BookmarkId = "1", Group = string.Empty });

        BookmarkRepositoryService.MoveBookmarkToFolder(workspace, "1", "Feature/Area/Task");

        Assert.AreEqual("Feature/Area/Task", workspace.Bookmarks.Single(item => item.BookmarkId == "1").Group);
        Assert.Contains("Feature", workspace.FolderPaths);
        Assert.Contains("Feature/Area", workspace.FolderPaths);
        Assert.Contains("Feature/Area/Task", workspace.FolderPaths);
    }

    [TestMethod]
    public void MetadataStore_GetStoragePath_WhenSolutionPathWhitespace_UsesTransientPath()
    {
        var store = new BookmarkMetadataStore();

        string path = store.GetStoragePath(" ");

        string expectedRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BookmarkStudio",
            "Transient");
        StringAssert.StartsWith(path, expectedRoot);
        StringAssert.EndsWith(path, "bookmarks.json");
    }

    [TestMethod]
    public async Task MetadataStore_SaveAsync_WhenBookmarkRemovedFromFolder_PreservesEmptyFolder()
    {
        var store = new BookmarkMetadataStore();
        string solutionPath = CreateTestSolutionPath();

        // Create initial workspace with a folder containing one bookmark and an empty sibling folder
        var initialWorkspace = new BookmarkWorkspaceState();
        initialWorkspace.FolderPaths.Add(string.Empty);
        initialWorkspace.FolderPaths.Add("FolderA");
        initialWorkspace.FolderPaths.Add("EmptyFolder");
        initialWorkspace.Bookmarks.Add(new BookmarkMetadata
        {
            BookmarkId = "id-1",
            DocumentPath = @"C:\repo\file.cs",
            LineNumber = 10,
            Group = "FolderA",
        });
        await store.SaveWorkspaceAsync(solutionPath, initialWorkspace, CancellationToken.None);

        // Remove the bookmark (simulating deletion of last bookmark in FolderA)
        await store.SaveAsync(solutionPath, Array.Empty<BookmarkMetadata>(), CancellationToken.None);

        // Verify empty folders are preserved
        BookmarkWorkspaceState loaded = await store.LoadWorkspaceAsync(solutionPath, CancellationToken.None);
        Assert.IsEmpty(loaded.Bookmarks);
        Assert.Contains("FolderA", loaded.FolderPaths, "FolderA should be preserved even when empty");
        Assert.Contains("EmptyFolder", loaded.FolderPaths, "EmptyFolder should be preserved");
    }

    [TestMethod]
    public void Repository_MoveFolder_WhenMovingToNewParent_UpdatesPathsAndBookmarks()
    {
        var workspace = new BookmarkWorkspaceState();
        workspace.FolderPaths.Add(string.Empty);
        workspace.FolderPaths.Add("Source");
        workspace.FolderPaths.Add("Source/Child");
        workspace.FolderPaths.Add("Target");
        workspace.Bookmarks.Add(new BookmarkMetadata { BookmarkId = "1", Group = "Source" });
        workspace.Bookmarks.Add(new BookmarkMetadata { BookmarkId = "2", Group = "Source/Child" });
        workspace.Bookmarks.Add(new BookmarkMetadata { BookmarkId = "3", Group = "Target" });

        BookmarkRepositoryService.MoveFolder(workspace, "Source", "Target/Source");

        Assert.DoesNotContain("Source", workspace.FolderPaths);
        Assert.DoesNotContain("Source/Child", workspace.FolderPaths);
        Assert.Contains("Target/Source", workspace.FolderPaths);
        Assert.Contains("Target/Source/Child", workspace.FolderPaths);
        Assert.AreEqual("Target/Source", workspace.Bookmarks.Single(b => b.BookmarkId == "1").Group);
        Assert.AreEqual("Target/Source/Child", workspace.Bookmarks.Single(b => b.BookmarkId == "2").Group);
        Assert.AreEqual("Target", workspace.Bookmarks.Single(b => b.BookmarkId == "3").Group);
    }

    [TestMethod]
    public void Repository_MoveFolder_WhenSourceEqualsTarget_DoesNothing()
    {
        var workspace = new BookmarkWorkspaceState();
        workspace.FolderPaths.Add(string.Empty);
        workspace.FolderPaths.Add("Folder");
        workspace.Bookmarks.Add(new BookmarkMetadata { BookmarkId = "1", Group = "Folder" });

        BookmarkRepositoryService.MoveFolder(workspace, "Folder", "Folder");

        Assert.Contains("Folder", workspace.FolderPaths);
        Assert.AreEqual("Folder", workspace.Bookmarks.Single(b => b.BookmarkId == "1").Group);
    }

    [TestMethod]
    public void Repository_MoveFolder_WhenSourceIsEmpty_ThrowsArgumentException()
    {
        var workspace = new BookmarkWorkspaceState();
        workspace.FolderPaths.Add(string.Empty);

        Assert.ThrowsExactly<ArgumentException>(() =>
            BookmarkRepositoryService.MoveFolder(workspace, "", "Target"));
    }

    [TestMethod]
    public void FindNextAvailableSlot_WhenAllSlotsUsed_ReturnsNull()
    {
        BookmarkMetadata[] bookmarks = Enumerable.Range(1, 9)
            .Select(slot => new BookmarkMetadata { BookmarkId = slot.ToString(), ShortcutNumber = slot })
            .ToArray();

        int? result = BookmarkRepositoryService.FindNextAvailableShortcut(bookmarks);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void FindNextAvailableSlot_WhenNoBookmarks_ReturnsOne()
    {
        int? result = BookmarkRepositoryService.FindNextAvailableShortcut(Array.Empty<BookmarkMetadata>());

        Assert.AreEqual(1, result);
    }

    [TestMethod]
    public void FindNextAvailableSlot_WhenGapExists_ReturnsLowestAvailable()
    {
        BookmarkMetadata[] bookmarks =
        [
            new() { BookmarkId = "a", ShortcutNumber = 1 },
            new() { BookmarkId = "b", ShortcutNumber = 2 },
            new() { BookmarkId = "c", ShortcutNumber = 4 },
            new() { BookmarkId = "d", ShortcutNumber = 7 },
        ];

        int? result = BookmarkRepositoryService.FindNextAvailableShortcut(bookmarks);

        Assert.AreEqual(3, result);
    }

    [TestMethod]
    public void FindNextDefaultLabel_WhenNoBookmarks_ReturnsBookmark1()
    {
        string result = BookmarkRepositoryService.FindNextDefaultLabel(Array.Empty<BookmarkMetadata>());

        Assert.AreEqual("Bookmark1", result);
    }

    [TestMethod]
    public void FindNextDefaultLabel_WhenGapInNumbering_FillsGap()
    {
        BookmarkMetadata[] bookmarks =
        [
            new() { BookmarkId = "a", Label = "Bookmark1" },
            new() { BookmarkId = "b", Label = "Bookmark3" },
            new() { BookmarkId = "c", Label = "Custom Label" },
        ];

        string result = BookmarkRepositoryService.FindNextDefaultLabel(bookmarks);

        Assert.AreEqual("Bookmark2", result);
    }

    [TestMethod]
    public void FindNextDefaultLabel_WhenAllSequential_ReturnsNextInSequence()
    {
        BookmarkMetadata[] bookmarks =
        [
            new() { BookmarkId = "a", Label = "Bookmark1" },
            new() { BookmarkId = "b", Label = "Bookmark2" },
        ];

        string result = BookmarkRepositoryService.FindNextDefaultLabel(bookmarks);

        Assert.AreEqual("Bookmark3", result);
    }

    [TestMethod]
    public void FindNextAvailableLabel_WhenPreferredLabelIsUnused_ReturnsPreferredLabel()
    {
        BookmarkMetadata[] bookmarks =
        [
            new() { BookmarkId = "a", Label = "Existing" },
        ];

        string result = BookmarkRepositoryService.FindNextAvailableLabel(bookmarks, "MyMethod");

        Assert.AreEqual("MyMethod", result);
    }

    [TestMethod]
    public void FindNextAvailableLabel_WhenPreferredLabelIsUsed_ReturnsPreferredLabelWithSuffixOne()
    {
        BookmarkMetadata[] bookmarks =
        [
            new() { BookmarkId = "a", Label = "MyMethod" },
        ];

        string result = BookmarkRepositoryService.FindNextAvailableLabel(bookmarks, "MyMethod");

        Assert.AreEqual("MyMethod1", result);
    }

    [TestMethod]
    public void FindNextAvailableLabel_WhenSuffixGapExists_ReturnsLowestAvailableSuffix()
    {
        BookmarkMetadata[] bookmarks =
        [
            new() { BookmarkId = "a", Label = "MyMethod" },
            new() { BookmarkId = "b", Label = "MyMethod1" },
            new() { BookmarkId = "c", Label = "MyMethod3" },
        ];

        string result = BookmarkRepositoryService.FindNextAvailableLabel(bookmarks, "MyMethod");

        Assert.AreEqual("MyMethod2", result);
    }

    [TestMethod]
    public void FindNextAvailableLabel_WhenPreferredLabelIsBlank_UsesBookmarkBase()
    {
        BookmarkMetadata[] bookmarks =
        [
            new() { BookmarkId = "a", Label = "Bookmark" },
            new() { BookmarkId = "b", Label = "Bookmark1" },
        ];

        string result = BookmarkRepositoryService.FindNextAvailableLabel(bookmarks, " ");

        Assert.AreEqual("Bookmark2", result);
    }

    [TestMethod]
    public void FindNextDefaultFolderName_WhenNoFolders_ReturnsFolder1()
    {
        string result = BookmarkRepositoryService.FindNextDefaultFolderName(Array.Empty<string>());

        Assert.AreEqual("Folder1", result);
    }

    [TestMethod]
    public void FindNextDefaultFolderName_WhenGapInNumbering_FillsGap()
    {
        string[] folderPaths =
        [
            "Folder1",
            "Folder3",
            "CustomFolder",
        ];

        string result = BookmarkRepositoryService.FindNextDefaultFolderName(folderPaths);

        Assert.AreEqual("Folder2", result);
    }

    [TestMethod]
    public void FindNextDefaultFolderName_WhenAllSequential_ReturnsNextInSequence()
    {
        string[] folderPaths =
        [
            "Folder1",
            "Folder2",
        ];

        string result = BookmarkRepositoryService.FindNextDefaultFolderName(folderPaths);

        Assert.AreEqual("Folder3", result);
    }

    [TestMethod]
    public void DualBookmarkWorkspaceState_AllBookmarks_CombinesBothHives()
    {
        var personalState = new BookmarkWorkspaceState();
        personalState.Bookmarks.Add(new BookmarkMetadata { BookmarkId = "personal-1", Label = "Bookmark1" });
        personalState.Bookmarks.Add(new BookmarkMetadata { BookmarkId = "personal-2", Label = "Bookmark2" });

        var solutionState = new BookmarkWorkspaceState();
        solutionState.Bookmarks.Add(new BookmarkMetadata { BookmarkId = "solution-1", Label = "Bookmark3" });

        var globalState = new BookmarkWorkspaceState();
        var dualState = new DualBookmarkWorkspaceState(globalState, personalState, solutionState);

        Assert.HasCount(3, dualState.AllBookmarks);
        Assert.IsTrue(dualState.AllBookmarks.Any(b => b.BookmarkId == "personal-1"));
        Assert.IsTrue(dualState.AllBookmarks.Any(b => b.BookmarkId == "personal-2"));
        Assert.IsTrue(dualState.AllBookmarks.Any(b => b.BookmarkId == "solution-1"));
    }

    [TestMethod]
    public void DualBookmarkWorkspaceState_AllFolderPaths_CombinesBothHives()
    {
        var personalState = new BookmarkWorkspaceState();
        personalState.FolderPaths.Add(string.Empty);
        personalState.FolderPaths.Add("PersonalFolder");

        var solutionState = new BookmarkWorkspaceState();
        solutionState.FolderPaths.Add(string.Empty);
        solutionState.FolderPaths.Add("SolutionFolder");

        var globalState = new BookmarkWorkspaceState();
        var dualState = new DualBookmarkWorkspaceState(globalState, personalState, solutionState);

        Assert.HasCount(3, dualState.AllFolderPaths);
        Assert.Contains(string.Empty, dualState.AllFolderPaths);
        Assert.Contains("PersonalFolder", dualState.AllFolderPaths);
        Assert.Contains("SolutionFolder", dualState.AllFolderPaths);
    }

    [TestMethod]
    public void DualBookmarkWorkspaceState_AllFolderPaths_DeduplicatesCommonPaths()
    {
        var personalState = new BookmarkWorkspaceState();
        personalState.FolderPaths.Add(string.Empty);
        personalState.FolderPaths.Add("SharedFolder");

        var solutionState = new BookmarkWorkspaceState();
        solutionState.FolderPaths.Add(string.Empty);
        solutionState.FolderPaths.Add("SharedFolder");

        var globalState = new BookmarkWorkspaceState();
        var dualState = new DualBookmarkWorkspaceState(globalState, personalState, solutionState);

        Assert.HasCount(2, dualState.AllFolderPaths);
    }

    [TestMethod]
    public void FindNextDefaultLabel_WhenBookmarksSplitAcrossBothHives_ChecksAllBookmarks()
    {
        // This tests the bug scenario where Bookmark1 exists in Personal but not in Workspace.
        // When creating a new bookmark (which goes to Workspace), the name generation
        // should check both hives and return Bookmark2 instead of Bookmark1.
        var personalState = new BookmarkWorkspaceState();
        personalState.Bookmarks.Add(new BookmarkMetadata { BookmarkId = "p1", Label = "Bookmark1" });

        var solutionState = new BookmarkWorkspaceState();
        solutionState.Bookmarks.Add(new BookmarkMetadata { BookmarkId = "s1", Label = "Bookmark3" });

        var globalState = new BookmarkWorkspaceState();
        var dualState = new DualBookmarkWorkspaceState(globalState, personalState, solutionState);

        string result = BookmarkRepositoryService.FindNextDefaultLabel(dualState.AllBookmarks);

        Assert.AreEqual("Bookmark2", result, "Should skip Bookmark1 (in Personal) and Bookmark3 (in Solution), returning Bookmark2");
    }

    [TestMethod]
    public void FindNextDefaultFolderName_WithNestedPaths_ExtractsFolderNames()
    {
        string[] folderPaths =
        [
            "Parent/Folder1",
            "Parent/Child/Folder2",
            "Folder3",
        ];

        string result = BookmarkRepositoryService.FindNextDefaultFolderName(folderPaths);

        Assert.AreEqual("Folder4", result);
    }

    [TestMethod]
    public void FindNextDefaultFolderName_IsCaseInsensitive()
    {
        string[] folderPaths =
        [
            "folder1",
            "FOLDER2",
        ];

        string result = BookmarkRepositoryService.FindNextDefaultFolderName(folderPaths);

        Assert.AreEqual("Folder3", result);
    }

    [TestMethod]
    public void BookmarkSnapshot_ExactMatchKey_ReturnsNormalizedKeyFromPathAndLine()
    {
        var snapshot = new BookmarkSnapshot
        {
            DocumentPath = @"C:\Repo\src\file.cs",
            LineNumber = 42,
        };

        string key = snapshot.ExactMatchKey;

        StringAssert.EndsWith(key, "|42");
        StringAssert.Contains(key, "REPO");
        StringAssert.Contains(key, "SRC");
    }

    [TestMethod]
    public void BookmarkSnapshot_ExactMatchKey_WhenPathCasingDiffers_ProducesSameKey()
    {
        var snapshot1 = new BookmarkSnapshot { DocumentPath = @"C:\repo\file.cs", LineNumber = 10 };
        var snapshot2 = new BookmarkSnapshot { DocumentPath = @"C:\REPO\FILE.CS", LineNumber = 10 };

        Assert.AreEqual(snapshot1.ExactMatchKey, snapshot2.ExactMatchKey);
    }

    [TestMethod]
    public void BookmarkMetadata_FolderPath_WhenSet_NormalizesValue()
    {
        var metadata = new BookmarkMetadata();

        metadata.FolderPath = @"Root\Team\\Backlog/Sprint1 ";

        Assert.AreEqual("Team/Backlog/Sprint1", metadata.Group);
        Assert.AreEqual("Team/Backlog/Sprint1", metadata.FolderPath);
    }

    [TestMethod]
    public void BookmarkMetadata_FolderPath_WhenSetToNull_NormalizesToEmptyString()
    {
        var metadata = new BookmarkMetadata { Group = "ExistingGroup" };

        metadata.FolderPath = null;

        Assert.AreEqual(string.Empty, metadata.Group);
        Assert.AreEqual(string.Empty, metadata.FolderPath);
    }

    private static string CreateTestSolutionPath()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "BookmarkStudio.Tests", Guid.NewGuid().ToString("N"));
        return Path.Combine(testRoot, "demo.sln");
    }
}