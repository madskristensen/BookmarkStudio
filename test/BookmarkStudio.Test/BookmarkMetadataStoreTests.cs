using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BookmarkStudio.Test;

[TestClass]
public class BookmarkMetadataStoreTests
{
    [TestMethod]
    public async Task LoadWorkspaceAsync_WhenFileDoesNotExist_ReturnsEmptyState()
    {
        BookmarkMetadataStore store = new BookmarkMetadataStore();
        string fakeSolutionPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "fake.sln");

        BookmarkWorkspaceState state = await store.LoadWorkspaceAsync(fakeSolutionPath, CancellationToken.None);

        Assert.IsNotNull(state);
        Assert.IsEmpty(state.Bookmarks);
        Assert.IsEmpty(state.ExpandedFolders);
    }

    [TestMethod]
    public async Task SaveAndLoadAsync_WhenRoundTripped_PreservesBookmarkData()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string solutionPath = Path.Combine(tempDir, "test.sln");
        File.WriteAllText(solutionPath, string.Empty);

        try
        {
            BookmarkMetadataStore store = new BookmarkMetadataStore();
            BookmarkMetadata original = new BookmarkMetadata
            {
                BookmarkId = "test-id-123",
                DocumentPath = Path.Combine(tempDir, "src", "Program.cs"),
                LineNumber = 42,
                LineText = "Console.WriteLine(\"Hello\");",
                SlotNumber = 3,
                Label = "Important entry point",
                Group = "Core/Startup",
                Color = BookmarkColor.Green,
            };

            await store.SaveAsync(solutionPath, new[] { original }, CancellationToken.None);
            IReadOnlyList<BookmarkMetadata> loaded = await store.LoadAsync(solutionPath, CancellationToken.None);

            Assert.HasCount(1, loaded);
            BookmarkMetadata result = loaded[0];
            Assert.AreEqual(original.BookmarkId, result.BookmarkId);
            Assert.AreEqual(original.DocumentPath, result.DocumentPath);
            Assert.AreEqual(original.LineNumber, result.LineNumber);
            Assert.AreEqual(original.LineText, result.LineText);
            Assert.AreEqual(original.SlotNumber, result.SlotNumber);
            Assert.AreEqual(original.Label, result.Label);
            Assert.AreEqual(original.Color, result.Color);
            Assert.AreEqual("Core/Startup", result.Group);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task SaveAndLoadAsync_WhenMultipleBookmarksInFolders_PreservesFolderStructure()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string solutionPath = Path.Combine(tempDir, "test.sln");
        File.WriteAllText(solutionPath, string.Empty);

        try
        {
            BookmarkMetadataStore store = new BookmarkMetadataStore();
            BookmarkMetadata[] bookmarks =
            {
                new BookmarkMetadata { BookmarkId = "root-1", DocumentPath = Path.Combine(tempDir, "a.cs"), LineNumber = 1, Group = string.Empty },
                new BookmarkMetadata { BookmarkId = "folder-1", DocumentPath = Path.Combine(tempDir, "b.cs"), LineNumber = 2, Group = "Features" },
                new BookmarkMetadata { BookmarkId = "nested-1", DocumentPath = Path.Combine(tempDir, "c.cs"), LineNumber = 3, Group = "Features/Auth" },
            };

            await store.SaveAsync(solutionPath, bookmarks, CancellationToken.None);
            IReadOnlyList<BookmarkMetadata> loaded = await store.LoadAsync(solutionPath, CancellationToken.None);

            Assert.HasCount(3, loaded);
            Assert.AreEqual(string.Empty, loaded.First(b => b.BookmarkId == "root-1").Group);
            Assert.AreEqual("Features", loaded.First(b => b.BookmarkId == "folder-1").Group);
            Assert.AreEqual("Features/Auth", loaded.First(b => b.BookmarkId == "nested-1").Group);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task SaveWorkspaceAsync_WhenExpandedFoldersSet_PreservesExpandedState()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string solutionPath = Path.Combine(tempDir, "test.sln");
        File.WriteAllText(solutionPath, string.Empty);

        try
        {
            BookmarkMetadataStore store = new BookmarkMetadataStore();
            BookmarkWorkspaceState state = new BookmarkWorkspaceState();
            state.Bookmarks.Add(new BookmarkMetadata { BookmarkId = "b1", DocumentPath = Path.Combine(tempDir, "a.cs"), LineNumber = 1, Group = "Folder1" });
            state.FolderPaths.Add("Folder1");
            state.FolderPaths.Add("Folder1/Sub");
            state.ExpandedFolders.Add("Folder1");

            await store.SaveWorkspaceAsync(solutionPath, state, CancellationToken.None);
            BookmarkWorkspaceState loaded = await store.LoadWorkspaceAsync(solutionPath, CancellationToken.None);

            Assert.IsTrue(loaded.ExpandedFolders.Contains("Folder1"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public void GetStorageInfo_WhenNoSolutionPath_ReturnsTransientPersonalLocation()
    {
        BookmarkMetadataStore store = new BookmarkMetadataStore();

        BookmarkStorageInfo info = store.GetStorageInfo(string.Empty);

        Assert.AreEqual(BookmarkStorageLocation.Personal, info.Location);
        Assert.IsTrue(info.AbsolutePath.Contains("BookmarkStudio"));
        Assert.IsTrue(info.AbsolutePath.Contains("Transient"));
    }

    [TestMethod]
    public void GetStorageInfo_WhenSolutionPathProvided_ReturnsPersonalLocationByDefault()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string solutionPath = Path.Combine(tempDir, "test.sln");

        try
        {
            BookmarkMetadataStore store = new BookmarkMetadataStore();

            BookmarkStorageInfo info = store.GetStorageInfo(solutionPath);

            Assert.AreEqual(BookmarkStorageLocation.Personal, info.Location);
            Assert.IsTrue(info.AbsolutePath.Contains(".vs"));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public void GetSolutionStoragePath_WhenNoSolutionPath_Throws()
    {
        BookmarkMetadataStore store = new BookmarkMetadataStore();

        Assert.ThrowsExactly<InvalidOperationException>(() => store.GetSolutionStoragePath(string.Empty));
    }

    [TestMethod]
    public void GetPersonalStoragePath_WhenNoSolutionPath_ReturnsLocalAppDataPath()
    {
        BookmarkMetadataStore store = new BookmarkMetadataStore();

        string path = store.GetPersonalStoragePath(string.Empty);

        Assert.IsTrue(path.Contains(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)));
        Assert.IsTrue(path.EndsWith(".bookmarks.json", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task LoadAsync_WhenJsonHasUnknownProperties_IgnoresThem()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempDir, ".vs"));
        string solutionPath = Path.Combine(tempDir, "test.sln");
        File.WriteAllText(solutionPath, string.Empty);

        string jsonWithExtraProps = @"{
            ""root"": {
                ""_bookmarks"": [
                    {
                        ""id"": ""test-1"",
                        ""documentPath"": ""src/file.cs"",
                        ""lineNumber"": 10,
                        ""lineText"": ""test"",
                        ""unknownProp"": ""should be ignored"",
                        ""anotherUnknown"": 123
                    }
                ]
            },
            ""expandedFolders"": [],
            ""futureFeature"": { ""nested"": true }
        }";
        string bookmarksPath = Path.Combine(tempDir, ".vs", ".bookmarks.json");
        File.WriteAllText(bookmarksPath, jsonWithExtraProps, Encoding.UTF8);

        try
        {
            BookmarkMetadataStore store = new BookmarkMetadataStore();

            IReadOnlyList<BookmarkMetadata> loaded = await store.LoadAsync(solutionPath, CancellationToken.None);

            Assert.HasCount(1, loaded);
            Assert.AreEqual("test-1", loaded[0].BookmarkId);
            Assert.AreEqual(10, loaded[0].LineNumber);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task LoadAsync_WhenBookmarkHasNoSlotOrColor_UsesDefaults()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(tempDir, ".vs"));
        string solutionPath = Path.Combine(tempDir, "test.sln");
        File.WriteAllText(solutionPath, string.Empty);

        string minimalJson = @"{
            ""root"": {
                ""_bookmarks"": [
                    {
                        ""id"": ""minimal-1"",
                        ""documentPath"": ""file.cs"",
                        ""lineNumber"": 5,
                        ""lineText"": ""code""
                    }
                ]
            },
            ""expandedFolders"": []
        }";
        string bookmarksPath = Path.Combine(tempDir, ".vs", ".bookmarks.json");
        File.WriteAllText(bookmarksPath, minimalJson, Encoding.UTF8);

        try
        {
            BookmarkMetadataStore store = new BookmarkMetadataStore();

            IReadOnlyList<BookmarkMetadata> loaded = await store.LoadAsync(solutionPath, CancellationToken.None);

            Assert.HasCount(1, loaded);
            Assert.IsNull(loaded[0].SlotNumber);
            Assert.AreEqual(BookmarkColor.None, loaded[0].Color);
            Assert.AreEqual(string.Empty, loaded[0].Label);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task MoveBookmarkBetweenLocationsAsync_WhenMovedToTargetFolder_UpdatesFolderAndStorage()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string solutionPath = Path.Combine(tempDir, "test.sln");
        File.WriteAllText(solutionPath, string.Empty);

        try
        {
            BookmarkMetadataStore store = new BookmarkMetadataStore();

            // Create a bookmark in Solution storage at root folder
            BookmarkWorkspaceState solutionState = new BookmarkWorkspaceState();
            solutionState.Bookmarks.Add(new BookmarkMetadata
            {
                BookmarkId = "bookmark-1",
                DocumentPath = Path.Combine(tempDir, "file.cs"),
                LineNumber = 10,
                Group = string.Empty,
                StorageLocation = BookmarkStorageLocation.Solution
            });
            solutionState.FolderPaths.Add(string.Empty);
            await store.SaveWorkspaceToLocationAsync(solutionPath, BookmarkStorageLocation.Solution, solutionState, CancellationToken.None);

            // Create Personal storage with a target folder
            BookmarkWorkspaceState personalState = new BookmarkWorkspaceState();
            personalState.FolderPaths.Add(string.Empty);
            personalState.FolderPaths.Add("TargetFolder");
            await store.SaveWorkspaceToLocationAsync(solutionPath, BookmarkStorageLocation.Personal, personalState, CancellationToken.None);

            // Move bookmark to Personal storage and TargetFolder
            await store.MoveBookmarkBetweenLocationsAsync(
                solutionPath,
                "bookmark-1",
                "TargetFolder",
                BookmarkStorageLocation.Personal,
                CancellationToken.None);

            // Load and verify
            DualBookmarkWorkspaceState dualState = await store.LoadDualWorkspaceAsync(solutionPath, CancellationToken.None);

            Assert.IsEmpty(dualState.SolutionState.Bookmarks, "Bookmark should be removed from Solution storage");
            Assert.HasCount(1, dualState.PersonalState.Bookmarks);

            BookmarkMetadata movedBookmark = dualState.PersonalState.Bookmarks[0];
            Assert.AreEqual("bookmark-1", movedBookmark.BookmarkId);
            Assert.AreEqual("TargetFolder", movedBookmark.Group, "Bookmark should be in TargetFolder");
            Assert.AreEqual(BookmarkStorageLocation.Personal, movedBookmark.StorageLocation);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task MoveBookmarkBetweenLocationsAsync_WhenMovedToNestedFolder_UpdatesFolderAndRegistersPath()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string solutionPath = Path.Combine(tempDir, "test.sln");
        File.WriteAllText(solutionPath, string.Empty);

        try
        {
            BookmarkMetadataStore store = new BookmarkMetadataStore();

            // Create a bookmark in Solution storage
            BookmarkWorkspaceState solutionState = new BookmarkWorkspaceState();
            solutionState.Bookmarks.Add(new BookmarkMetadata
            {
                BookmarkId = "bookmark-1",
                DocumentPath = Path.Combine(tempDir, "file.cs"),
                LineNumber = 10,
                Group = "OriginalFolder",
                StorageLocation = BookmarkStorageLocation.Solution
            });
            solutionState.FolderPaths.Add(string.Empty);
            solutionState.FolderPaths.Add("OriginalFolder");
            await store.SaveWorkspaceToLocationAsync(solutionPath, BookmarkStorageLocation.Solution, solutionState, CancellationToken.None);

            // Create empty Personal storage
            BookmarkWorkspaceState personalState = new BookmarkWorkspaceState();
            personalState.FolderPaths.Add(string.Empty);
            await store.SaveWorkspaceToLocationAsync(solutionPath, BookmarkStorageLocation.Personal, personalState, CancellationToken.None);

            // Move bookmark to Personal storage with a nested folder path
            await store.MoveBookmarkBetweenLocationsAsync(
                solutionPath,
                "bookmark-1",
                "Parent/Child/Nested",
                BookmarkStorageLocation.Personal,
                CancellationToken.None);

            // Load and verify
            DualBookmarkWorkspaceState dualState = await store.LoadDualWorkspaceAsync(solutionPath, CancellationToken.None);

            Assert.IsEmpty(dualState.SolutionState.Bookmarks);
            Assert.HasCount(1, dualState.PersonalState.Bookmarks);

            BookmarkMetadata movedBookmark = dualState.PersonalState.Bookmarks[0];
            Assert.AreEqual("Parent/Child/Nested", movedBookmark.Group, "Bookmark should be in nested folder");
            Assert.Contains("Parent/Child/Nested", dualState.PersonalState.FolderPaths, "Nested folder path should be registered");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task MoveFolderBetweenLocationsAsync_WhenFolderHasBookmarks_MovesAllBookmarks()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string solutionPath = Path.Combine(tempDir, "test.sln");
        File.WriteAllText(solutionPath, string.Empty);

        try
        {
            BookmarkMetadataStore store = new BookmarkMetadataStore();

            // Create Solution storage with a folder containing bookmarks
            BookmarkWorkspaceState solutionState = new BookmarkWorkspaceState();
            solutionState.Bookmarks.Add(new BookmarkMetadata
            {
                BookmarkId = "bookmark-1",
                DocumentPath = Path.Combine(tempDir, "a.cs"),
                LineNumber = 10,
                Group = "SourceFolder",
                StorageLocation = BookmarkStorageLocation.Solution
            });
            solutionState.Bookmarks.Add(new BookmarkMetadata
            {
                BookmarkId = "bookmark-2",
                DocumentPath = Path.Combine(tempDir, "b.cs"),
                LineNumber = 20,
                Group = "SourceFolder",
                StorageLocation = BookmarkStorageLocation.Solution
            });
            solutionState.FolderPaths.Add(string.Empty);
            solutionState.FolderPaths.Add("SourceFolder");
            await store.SaveWorkspaceToLocationAsync(solutionPath, BookmarkStorageLocation.Solution, solutionState, CancellationToken.None);

            // Create empty Personal storage
            BookmarkWorkspaceState personalState = new BookmarkWorkspaceState();
            personalState.FolderPaths.Add(string.Empty);
            await store.SaveWorkspaceToLocationAsync(solutionPath, BookmarkStorageLocation.Personal, personalState, CancellationToken.None);

            // Move folder from Solution to Personal
            await store.MoveFolderBetweenLocationsAsync(
                solutionPath,
                "SourceFolder",
                BookmarkStorageLocation.Solution,
                "SourceFolder",
                BookmarkStorageLocation.Personal,
                CancellationToken.None);

            // Load and verify
            DualBookmarkWorkspaceState dualState = await store.LoadDualWorkspaceAsync(solutionPath, CancellationToken.None);

            Assert.IsEmpty(dualState.SolutionState.Bookmarks, "All bookmarks should be removed from Solution");
            Assert.DoesNotContain("SourceFolder", dualState.SolutionState.FolderPaths, "Folder should be removed from Solution");

            Assert.HasCount(2, dualState.PersonalState.Bookmarks, "All bookmarks should be in Personal");
            Assert.Contains("SourceFolder", dualState.PersonalState.FolderPaths, "Folder should be in Personal");

            foreach (BookmarkMetadata bookmark in dualState.PersonalState.Bookmarks)
            {
                Assert.AreEqual("SourceFolder", bookmark.Group);
                Assert.AreEqual(BookmarkStorageLocation.Personal, bookmark.StorageLocation);
            }
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task MoveFolderBetweenLocationsAsync_WhenFolderHasSubfolders_MovesEntireHierarchy()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string solutionPath = Path.Combine(tempDir, "test.sln");
        File.WriteAllText(solutionPath, string.Empty);

        try
        {
            BookmarkMetadataStore store = new BookmarkMetadataStore();

            // Create Solution storage with nested folder structure
            BookmarkWorkspaceState solutionState = new BookmarkWorkspaceState();
            solutionState.Bookmarks.Add(new BookmarkMetadata
            {
                BookmarkId = "root-bookmark",
                DocumentPath = Path.Combine(tempDir, "a.cs"),
                LineNumber = 10,
                Group = "Parent",
                StorageLocation = BookmarkStorageLocation.Solution
            });
            solutionState.Bookmarks.Add(new BookmarkMetadata
            {
                BookmarkId = "child-bookmark",
                DocumentPath = Path.Combine(tempDir, "b.cs"),
                LineNumber = 20,
                Group = "Parent/Child",
                StorageLocation = BookmarkStorageLocation.Solution
            });
            solutionState.Bookmarks.Add(new BookmarkMetadata
            {
                BookmarkId = "grandchild-bookmark",
                DocumentPath = Path.Combine(tempDir, "c.cs"),
                LineNumber = 30,
                Group = "Parent/Child/Grandchild",
                StorageLocation = BookmarkStorageLocation.Solution
            });
            solutionState.FolderPaths.Add(string.Empty);
            solutionState.FolderPaths.Add("Parent");
            solutionState.FolderPaths.Add("Parent/Child");
            solutionState.FolderPaths.Add("Parent/Child/Grandchild");
            await store.SaveWorkspaceToLocationAsync(solutionPath, BookmarkStorageLocation.Solution, solutionState, CancellationToken.None);

            // Create empty Personal storage
            BookmarkWorkspaceState personalState = new BookmarkWorkspaceState();
            personalState.FolderPaths.Add(string.Empty);
            await store.SaveWorkspaceToLocationAsync(solutionPath, BookmarkStorageLocation.Personal, personalState, CancellationToken.None);

            // Move Parent folder from Solution to Personal
            await store.MoveFolderBetweenLocationsAsync(
                solutionPath,
                "Parent",
                BookmarkStorageLocation.Solution,
                "Parent",
                BookmarkStorageLocation.Personal,
                CancellationToken.None);

            // Load and verify
            DualBookmarkWorkspaceState dualState = await store.LoadDualWorkspaceAsync(solutionPath, CancellationToken.None);

            Assert.IsEmpty(dualState.SolutionState.Bookmarks, "All bookmarks should be removed from Solution");
            Assert.HasCount(3, dualState.PersonalState.Bookmarks, "All 3 bookmarks should be in Personal");

            Assert.AreEqual("Parent", dualState.PersonalState.Bookmarks.Single(b => b.BookmarkId == "root-bookmark").Group);
            Assert.AreEqual("Parent/Child", dualState.PersonalState.Bookmarks.Single(b => b.BookmarkId == "child-bookmark").Group);
            Assert.AreEqual("Parent/Child/Grandchild", dualState.PersonalState.Bookmarks.Single(b => b.BookmarkId == "grandchild-bookmark").Group);

            Assert.Contains("Parent", dualState.PersonalState.FolderPaths);
            Assert.Contains("Parent/Child", dualState.PersonalState.FolderPaths);
            Assert.Contains("Parent/Child/Grandchild", dualState.PersonalState.FolderPaths);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task MoveFolderBetweenLocationsAsync_WhenMovingToNewPath_TransformsFolderPaths()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string solutionPath = Path.Combine(tempDir, "test.sln");
        File.WriteAllText(solutionPath, string.Empty);

        try
        {
            BookmarkMetadataStore store = new BookmarkMetadataStore();

            // Create Solution storage with folder and bookmark
            BookmarkWorkspaceState solutionState = new BookmarkWorkspaceState();
            solutionState.Bookmarks.Add(new BookmarkMetadata
            {
                BookmarkId = "bookmark-1",
                DocumentPath = Path.Combine(tempDir, "a.cs"),
                LineNumber = 10,
                Group = "OldFolder/Nested",
                StorageLocation = BookmarkStorageLocation.Solution
            });
            solutionState.FolderPaths.Add(string.Empty);
            solutionState.FolderPaths.Add("OldFolder");
            solutionState.FolderPaths.Add("OldFolder/Nested");
            await store.SaveWorkspaceToLocationAsync(solutionPath, BookmarkStorageLocation.Solution, solutionState, CancellationToken.None);

            // Create Personal storage with target parent folder
            BookmarkWorkspaceState personalState = new BookmarkWorkspaceState();
            personalState.FolderPaths.Add(string.Empty);
            personalState.FolderPaths.Add("TargetParent");
            await store.SaveWorkspaceToLocationAsync(solutionPath, BookmarkStorageLocation.Personal, personalState, CancellationToken.None);

            // Move OldFolder to TargetParent/NewFolder in Personal
            await store.MoveFolderBetweenLocationsAsync(
                solutionPath,
                "OldFolder",
                BookmarkStorageLocation.Solution,
                "TargetParent/NewFolder",
                BookmarkStorageLocation.Personal,
                CancellationToken.None);

            // Load and verify
            DualBookmarkWorkspaceState dualState = await store.LoadDualWorkspaceAsync(solutionPath, CancellationToken.None);

            Assert.IsEmpty(dualState.SolutionState.Bookmarks);
            Assert.HasCount(1, dualState.PersonalState.Bookmarks);

            BookmarkMetadata movedBookmark = dualState.PersonalState.Bookmarks[0];
            Assert.AreEqual("TargetParent/NewFolder/Nested", movedBookmark.Group, "Nested path should be transformed");

            Assert.Contains("TargetParent/NewFolder", dualState.PersonalState.FolderPaths);
            Assert.Contains("TargetParent/NewFolder/Nested", dualState.PersonalState.FolderPaths);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }
}
