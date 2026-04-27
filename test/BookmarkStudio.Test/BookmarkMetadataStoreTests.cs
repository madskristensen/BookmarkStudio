using System.IO;
using System.Text;

namespace BookmarkStudio.Test;

[TestClass]
public class BookmarkMetadataStoreTests
{
    [TestMethod]
    public async Task LoadWorkspaceAsync_WhenFileDoesNotExist_ReturnsEmptyState()
    {
        var store = new BookmarkMetadataStore();
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
            var store = new BookmarkMetadataStore();
            var original = new BookmarkMetadata
            {
                BookmarkId = "test-id-123",
                DocumentPath = Path.Combine(tempDir, "src", "Program.cs"),
                LineNumber = 42,
                LineText = "Console.WriteLine(\"Hello\");",
                ShortcutNumber = 3,
                Label = "Important entry point",
                Group = "Core/Startup",
                Color = BookmarkColor.Green,
            };

            await store.SaveAsync(solutionPath, new[] { original }, CancellationToken.None);
            string json = File.ReadAllText(Path.Combine(tempDir, ".vs", ".bookmarks.json"), Encoding.UTF8);
            Assert.DoesNotContain(@"""documentPathRoot""", json);
            StringAssert.Contains(json, @"""documentPath"": ""src/Program.cs""");

            IReadOnlyList<BookmarkMetadata> loaded = await store.LoadAsync(solutionPath, CancellationToken.None);

            Assert.HasCount(1, loaded);
            BookmarkMetadata result = loaded[0];
            Assert.AreEqual(original.BookmarkId, result.BookmarkId);
            Assert.AreEqual(original.DocumentPath, result.DocumentPath);
            Assert.AreEqual(original.LineNumber, result.LineNumber);
            Assert.AreEqual(original.LineText, result.LineText);
            Assert.AreEqual(original.ShortcutNumber, result.ShortcutNumber);
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
    public async Task SaveWorkspaceToLocationAsync_WhenDocumentOutsideNestedSolution_SavesPathRelativeToBookmarksFile()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string solutionDir = Path.Combine(tempDir, "src", "App");
        string sharedDir = Path.Combine(tempDir, "shared");
        Directory.CreateDirectory(Path.Combine(tempDir, ".git"));
        Directory.CreateDirectory(solutionDir);
        Directory.CreateDirectory(sharedDir);
        string solutionPath = Path.Combine(solutionDir, "test.sln");
        string documentPath = Path.Combine(sharedDir, "Shared.cs");
        File.WriteAllText(solutionPath, string.Empty);
        File.WriteAllText(documentPath, string.Empty);

        try
        {
            var store = new BookmarkMetadataStore();
            var state = new BookmarkWorkspaceState();
            state.Bookmarks.Add(new BookmarkMetadata
            {
                BookmarkId = "shared-file",
                DocumentPath = documentPath,
                LineNumber = 7,
                LineText = "shared",
                StorageLocation = BookmarkStorageLocation.Workspace,
            });

            await store.SaveWorkspaceToLocationAsync(solutionPath, BookmarkStorageLocation.Workspace, state, CancellationToken.None);

            string bookmarksPath = Path.Combine(tempDir, ".bookmarks.json");
            string json = File.ReadAllText(bookmarksPath, Encoding.UTF8);
            StringAssert.Contains(json, @"""documentPathRoot"": ""bookmarksFile""");
            StringAssert.Contains(json, @"""documentPath"": ""shared/Shared.cs""");

            BookmarkWorkspaceState loaded = await store.LoadWorkspaceFromLocationAsync(solutionPath, BookmarkStorageLocation.Workspace, CancellationToken.None);

            Assert.HasCount(1, loaded.Bookmarks);
            Assert.AreEqual(documentPath, loaded.Bookmarks[0].DocumentPath);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public async Task LoadWorkspaceFromLocationAsync_WhenLegacyWorkspacePathIsRelativeToSolution_KeepsLegacyBaseDirectory()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        string solutionDir = Path.Combine(tempDir, "nested", "Solution");
        Directory.CreateDirectory(Path.Combine(tempDir, ".git"));
        Directory.CreateDirectory(Path.Combine(solutionDir, "src"));
        string solutionPath = Path.Combine(solutionDir, "test.sln");
        string documentPath = Path.Combine(solutionDir, "src", "Program.cs");
        File.WriteAllText(solutionPath, string.Empty);
        File.WriteAllText(documentPath, string.Empty);

        string legacyJson = @"{
            ""root"": {
                ""_bookmarks"": [
                    {
                        ""id"": ""legacy-1"",
                        ""documentPath"": ""src/Program.cs"",
                        ""lineNumber"": 12,
                        ""lineText"": ""legacy""
                    }
                ]
            }
        }";
        File.WriteAllText(Path.Combine(tempDir, ".bookmarks.json"), legacyJson, Encoding.UTF8);

        try
        {
            var store = new BookmarkMetadataStore();

            BookmarkWorkspaceState loaded = await store.LoadWorkspaceFromLocationAsync(solutionPath, BookmarkStorageLocation.Workspace, CancellationToken.None);

            Assert.HasCount(1, loaded.Bookmarks);
            Assert.AreEqual(documentPath, loaded.Bookmarks[0].DocumentPath);
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
            var store = new BookmarkMetadataStore();
            BookmarkMetadata[] bookmarks =
            [
                new() { BookmarkId = "root-1", DocumentPath = Path.Combine(tempDir, "a.cs"), LineNumber = 1, Group = string.Empty },
                new() { BookmarkId = "folder-1", DocumentPath = Path.Combine(tempDir, "b.cs"), LineNumber = 2, Group = "Features" },
                new() { BookmarkId = "nested-1", DocumentPath = Path.Combine(tempDir, "c.cs"), LineNumber = 3, Group = "Features/Auth" },
            ];

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
            var store = new BookmarkMetadataStore();
            var state = new BookmarkWorkspaceState();
            state.Bookmarks.Add(new BookmarkMetadata { BookmarkId = "b1", DocumentPath = Path.Combine(tempDir, "a.cs"), LineNumber = 1, Group = "Folder1" });
            state.FolderPaths.Add("Folder1");
            state.FolderPaths.Add("Folder1/Sub");
            state.ExpandedFolders.Add("Folder1");

            await store.SaveWorkspaceAsync(solutionPath, state, CancellationToken.None);
            BookmarkWorkspaceState loaded = await store.LoadWorkspaceAsync(solutionPath, CancellationToken.None);

            Assert.Contains("Folder1", loaded.ExpandedFolders);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public void GetStorageInfo_WhenNoSolutionPath_ReturnsTransientPersonalLocation()
    {
        var store = new BookmarkMetadataStore();

        BookmarkStorageInfo info = store.GetStorageInfo(string.Empty);

        Assert.AreEqual(BookmarkStorageLocation.Personal, info.Location);
        Assert.Contains("BookmarkStudio", info.AbsolutePath);
        Assert.Contains("Transient", info.AbsolutePath);
    }

    [TestMethod]
    public void GetStorageInfo_WhenSolutionPathProvided_ReturnsPersonalLocationByDefault()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string solutionPath = Path.Combine(tempDir, "test.sln");

        try
        {
            var store = new BookmarkMetadataStore();

            BookmarkStorageInfo info = store.GetStorageInfo(solutionPath);

            Assert.AreEqual(BookmarkStorageLocation.Personal, info.Location);
            Assert.Contains(".vs", info.AbsolutePath);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [TestMethod]
    public void GetSolutionStoragePath_WhenNoSolutionPath_Throws()
    {
        var store = new BookmarkMetadataStore();

        Assert.ThrowsExactly<InvalidOperationException>(() => store.GetSolutionStoragePath(string.Empty));
    }

    [TestMethod]
    public void GetPersonalStoragePath_WhenNoSolutionPath_ReturnsLocalAppDataPath()
    {
        var store = new BookmarkMetadataStore();

        string path = store.GetPersonalStoragePath(string.Empty);

        Assert.Contains(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), path);
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
            var store = new BookmarkMetadataStore();

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
            var store = new BookmarkMetadataStore();

            IReadOnlyList<BookmarkMetadata> loaded = await store.LoadAsync(solutionPath, CancellationToken.None);

            Assert.HasCount(1, loaded);
            Assert.IsNull(loaded[0].ShortcutNumber);
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
            var store = new BookmarkMetadataStore();

            // Create a bookmark in Solution storage at root folder
            var solutionState = new BookmarkWorkspaceState();
            solutionState.Bookmarks.Add(new BookmarkMetadata
            {
                BookmarkId = "bookmark-1",
                DocumentPath = Path.Combine(tempDir, "file.cs"),
                LineNumber = 10,
                Group = string.Empty,
                StorageLocation = BookmarkStorageLocation.Workspace
            });
            solutionState.FolderPaths.Add(string.Empty);
            await store.SaveWorkspaceToLocationAsync(solutionPath, BookmarkStorageLocation.Workspace, solutionState, CancellationToken.None);

            // Create Personal storage with a target folder
            var personalState = new BookmarkWorkspaceState();
            personalState.FolderPaths.Add(string.Empty);
            personalState.FolderPaths.Add("TargetFolder");
            await store.SaveWorkspaceToLocationAsync(solutionPath, BookmarkStorageLocation.Personal, personalState, CancellationToken.None);

            // Move bookmark to Personal storage and TargetFolder
            await store.MoveBookmarkBetweenLocationsAsync(
                solutionPath,
                "bookmark-1",
                "TargetFolder",
                BookmarkStorageLocation.Workspace,
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
            var store = new BookmarkMetadataStore();

            // Create a bookmark in Solution storage
            var solutionState = new BookmarkWorkspaceState();
            solutionState.Bookmarks.Add(new BookmarkMetadata
            {
                BookmarkId = "bookmark-1",
                DocumentPath = Path.Combine(tempDir, "file.cs"),
                LineNumber = 10,
                Group = "OriginalFolder",
                StorageLocation = BookmarkStorageLocation.Workspace
            });
            solutionState.FolderPaths.Add(string.Empty);
            solutionState.FolderPaths.Add("OriginalFolder");
            await store.SaveWorkspaceToLocationAsync(solutionPath, BookmarkStorageLocation.Workspace, solutionState, CancellationToken.None);

            // Create empty Personal storage
            var personalState = new BookmarkWorkspaceState();
            personalState.FolderPaths.Add(string.Empty);
            await store.SaveWorkspaceToLocationAsync(solutionPath, BookmarkStorageLocation.Personal, personalState, CancellationToken.None);

            // Move bookmark to Personal storage with a nested folder path
            await store.MoveBookmarkBetweenLocationsAsync(
                solutionPath,
                "bookmark-1",
                "Parent/Child/Nested",
                BookmarkStorageLocation.Workspace,
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
            var store = new BookmarkMetadataStore();

            // Create Solution storage with a folder containing bookmarks
            var solutionState = new BookmarkWorkspaceState();
            solutionState.Bookmarks.Add(new BookmarkMetadata
            {
                BookmarkId = "bookmark-1",
                DocumentPath = Path.Combine(tempDir, "a.cs"),
                LineNumber = 10,
                Group = "SourceFolder",
                StorageLocation = BookmarkStorageLocation.Workspace
            });
            solutionState.Bookmarks.Add(new BookmarkMetadata
            {
                BookmarkId = "bookmark-2",
                DocumentPath = Path.Combine(tempDir, "b.cs"),
                LineNumber = 20,
                Group = "SourceFolder",
                StorageLocation = BookmarkStorageLocation.Workspace
            });
            solutionState.FolderPaths.Add(string.Empty);
            solutionState.FolderPaths.Add("SourceFolder");
            await store.SaveWorkspaceToLocationAsync(solutionPath, BookmarkStorageLocation.Workspace, solutionState, CancellationToken.None);

            // Create empty Personal storage
            var personalState = new BookmarkWorkspaceState();
            personalState.FolderPaths.Add(string.Empty);
            await store.SaveWorkspaceToLocationAsync(solutionPath, BookmarkStorageLocation.Personal, personalState, CancellationToken.None);

            // Move folder from Solution to Personal
            await store.MoveFolderBetweenLocationsAsync(
                solutionPath,
                "SourceFolder",
                BookmarkStorageLocation.Workspace,
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
            var store = new BookmarkMetadataStore();

            // Create Solution storage with nested folder structure
            var solutionState = new BookmarkWorkspaceState();
            solutionState.Bookmarks.Add(new BookmarkMetadata
            {
                BookmarkId = "root-bookmark",
                DocumentPath = Path.Combine(tempDir, "a.cs"),
                LineNumber = 10,
                Group = "Parent",
                StorageLocation = BookmarkStorageLocation.Workspace
            });
            solutionState.Bookmarks.Add(new BookmarkMetadata
            {
                BookmarkId = "child-bookmark",
                DocumentPath = Path.Combine(tempDir, "b.cs"),
                LineNumber = 20,
                Group = "Parent/Child",
                StorageLocation = BookmarkStorageLocation.Workspace
            });
            solutionState.Bookmarks.Add(new BookmarkMetadata
            {
                BookmarkId = "grandchild-bookmark",
                DocumentPath = Path.Combine(tempDir, "c.cs"),
                LineNumber = 30,
                Group = "Parent/Child/Grandchild",
                StorageLocation = BookmarkStorageLocation.Workspace
            });
            solutionState.FolderPaths.Add(string.Empty);
            solutionState.FolderPaths.Add("Parent");
            solutionState.FolderPaths.Add("Parent/Child");
            solutionState.FolderPaths.Add("Parent/Child/Grandchild");
            await store.SaveWorkspaceToLocationAsync(solutionPath, BookmarkStorageLocation.Workspace, solutionState, CancellationToken.None);

            // Create empty Personal storage
            var personalState = new BookmarkWorkspaceState();
            personalState.FolderPaths.Add(string.Empty);
            await store.SaveWorkspaceToLocationAsync(solutionPath, BookmarkStorageLocation.Personal, personalState, CancellationToken.None);

            // Move Parent folder from Solution to Personal
            await store.MoveFolderBetweenLocationsAsync(
                solutionPath,
                "Parent",
                BookmarkStorageLocation.Workspace,
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
            var store = new BookmarkMetadataStore();

            // Create Solution storage with folder and bookmark
            var solutionState = new BookmarkWorkspaceState();
            solutionState.Bookmarks.Add(new BookmarkMetadata
            {
                BookmarkId = "bookmark-1",
                DocumentPath = Path.Combine(tempDir, "a.cs"),
                LineNumber = 10,
                Group = "OldFolder/Nested",
                StorageLocation = BookmarkStorageLocation.Workspace
            });
            solutionState.FolderPaths.Add(string.Empty);
            solutionState.FolderPaths.Add("OldFolder");
            solutionState.FolderPaths.Add("OldFolder/Nested");
            await store.SaveWorkspaceToLocationAsync(solutionPath, BookmarkStorageLocation.Workspace, solutionState, CancellationToken.None);

            // Create Personal storage with target parent folder
            var personalState = new BookmarkWorkspaceState();
            personalState.FolderPaths.Add(string.Empty);
            personalState.FolderPaths.Add("TargetParent");
            await store.SaveWorkspaceToLocationAsync(solutionPath, BookmarkStorageLocation.Personal, personalState, CancellationToken.None);

            // Move OldFolder to TargetParent/NewFolder in Personal
            await store.MoveFolderBetweenLocationsAsync(
                solutionPath,
                "OldFolder",
                BookmarkStorageLocation.Workspace,
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

    [TestMethod]
    public void GetGlobalStoragePath_ReturnsUserProfilePath()
    {
        var store = new BookmarkMetadataStore();

        string path = store.GetGlobalStoragePath();

        string expectedPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".bookmarks.json");
        Assert.AreEqual(expectedPath, path);
    }

    [TestMethod]
    public void GetStoragePathForLocation_WhenGlobal_ReturnsGlobalPath()
    {
        var store = new BookmarkMetadataStore();
        string solutionPath = Path.Combine(Path.GetTempPath(), "test.sln");

        string path = store.GetStoragePathForLocation(solutionPath, BookmarkStorageLocation.Global);

        Assert.AreEqual(store.GetGlobalStoragePath(), path);
    }

    [TestMethod]
    public async Task SaveAndLoadWorkspaceToLocationAsync_WhenGlobal_RoundTripsBookmarkData()
    {
        var store = new BookmarkMetadataStore();
        string globalPath = store.GetGlobalStoragePath();
        string globalDir = Path.GetDirectoryName(globalPath);
        string backupPath = globalPath + ".backup";

        // Backup existing global file if it exists
        bool hadExistingFile = File.Exists(globalPath);
        if (hadExistingFile)
        {
            File.Copy(globalPath, backupPath, overwrite: true);
        }

        try
        {
            // Delete existing file to start fresh
            if (File.Exists(globalPath))
            {
                File.Delete(globalPath);
            }

            var state = new BookmarkWorkspaceState();
            state.Bookmarks.Add(new BookmarkMetadata
            {
                BookmarkId = "global-test-1",
                DocumentPath = @"C:\Projects\App\Program.cs",
                LineNumber = 42,
                LineText = "Main entry point",
                Label = "Global Bookmark",
                Color = BookmarkColor.Blue,
                Group = "GlobalFolder",
                StorageLocation = BookmarkStorageLocation.Global
            });
            state.FolderPaths.Add(string.Empty);
            state.FolderPaths.Add("GlobalFolder");
            state.ExpandedFolders.Add("GlobalFolder");

            await store.SaveWorkspaceToLocationAsync(string.Empty, BookmarkStorageLocation.Global, state, CancellationToken.None);

            Assert.IsTrue(File.Exists(globalPath), "Global storage file should be created");

            BookmarkWorkspaceState loaded = await store.LoadWorkspaceFromLocationAsync(string.Empty, BookmarkStorageLocation.Global, CancellationToken.None);

            Assert.HasCount(1, loaded.Bookmarks);
            BookmarkMetadata bookmark = loaded.Bookmarks[0];
            Assert.AreEqual("global-test-1", bookmark.BookmarkId);
            Assert.AreEqual(@"C:\Projects\App\Program.cs", bookmark.DocumentPath);
            Assert.AreEqual(42, bookmark.LineNumber);
            Assert.AreEqual("Global Bookmark", bookmark.Label);
            Assert.AreEqual(BookmarkColor.Blue, bookmark.Color);
            Assert.AreEqual("GlobalFolder", bookmark.Group);
            Assert.AreEqual(BookmarkStorageLocation.Global, bookmark.StorageLocation);
            Assert.Contains("GlobalFolder", loaded.FolderPaths);
            Assert.Contains("GlobalFolder", loaded.ExpandedFolders);
        }
        finally
        {
            // Cleanup: delete test file and restore backup if existed
            if (File.Exists(globalPath))
            {
                File.Delete(globalPath);
            }

            if (hadExistingFile)
            {
                File.Move(backupPath, globalPath);
            }
        }
    }

    [TestMethod]
    public async Task LoadWorkspaceFromLocationAsync_WhenGlobalFileDoesNotExist_ReturnsEmptyState()
    {
        var store = new BookmarkMetadataStore();
        string globalPath = store.GetGlobalStoragePath();
        string backupPath = globalPath + ".backup";

        // Backup existing global file if it exists
        bool hadExistingFile = File.Exists(globalPath);
        if (hadExistingFile)
        {
            File.Move(globalPath, backupPath);
        }

        try
        {
            BookmarkWorkspaceState state = await store.LoadWorkspaceFromLocationAsync(string.Empty, BookmarkStorageLocation.Global, CancellationToken.None);

            Assert.IsNotNull(state);
            Assert.IsEmpty(state.Bookmarks);
            Assert.IsEmpty(state.ExpandedFolders);
        }
        finally
        {
            // Restore backup if existed
            if (hadExistingFile)
            {
                File.Move(backupPath, globalPath);
            }
        }
    }

    [TestMethod]
    public async Task SaveWorkspaceToLocationAsync_WhenGlobalWithMultipleBookmarks_PreservesAll()
    {
        var store = new BookmarkMetadataStore();
        string globalPath = store.GetGlobalStoragePath();
        string backupPath = globalPath + ".backup";

        bool hadExistingFile = File.Exists(globalPath);
        if (hadExistingFile)
        {
            File.Copy(globalPath, backupPath, overwrite: true);
        }

        try
        {
            if (File.Exists(globalPath))
            {
                File.Delete(globalPath);
            }

            var state = new BookmarkWorkspaceState();
            state.Bookmarks.Add(new BookmarkMetadata
            {
                BookmarkId = "global-1",
                DocumentPath = @"C:\Code\file1.cs",
                LineNumber = 10,
                Group = string.Empty,
                StorageLocation = BookmarkStorageLocation.Global
            });
            state.Bookmarks.Add(new BookmarkMetadata
            {
                BookmarkId = "global-2",
                DocumentPath = @"C:\Code\file2.cs",
                LineNumber = 20,
                Group = "Utilities",
                StorageLocation = BookmarkStorageLocation.Global
            });
            state.Bookmarks.Add(new BookmarkMetadata
            {
                BookmarkId = "global-3",
                DocumentPath = @"C:\Code\file3.cs",
                LineNumber = 30,
                Group = "Utilities/Helpers",
                StorageLocation = BookmarkStorageLocation.Global
            });
            state.FolderPaths.Add(string.Empty);
            state.FolderPaths.Add("Utilities");
            state.FolderPaths.Add("Utilities/Helpers");

            await store.SaveWorkspaceToLocationAsync(string.Empty, BookmarkStorageLocation.Global, state, CancellationToken.None);

            BookmarkWorkspaceState loaded = await store.LoadWorkspaceFromLocationAsync(string.Empty, BookmarkStorageLocation.Global, CancellationToken.None);

            Assert.HasCount(3, loaded.Bookmarks);
            Assert.AreEqual(string.Empty, loaded.Bookmarks.Single(b => b.BookmarkId == "global-1").Group);
            Assert.AreEqual("Utilities", loaded.Bookmarks.Single(b => b.BookmarkId == "global-2").Group);
            Assert.AreEqual("Utilities/Helpers", loaded.Bookmarks.Single(b => b.BookmarkId == "global-3").Group);
        }
        finally
        {
            if (File.Exists(globalPath))
            {
                File.Delete(globalPath);
            }

            if (hadExistingFile)
            {
                File.Move(backupPath, globalPath);
            }
        }
    }

    [TestMethod]
    public async Task GlobalStorage_WhenBookmarkHasAbsolutePath_PreservesAbsolutePath()
    {
        var store = new BookmarkMetadataStore();
        string globalPath = store.GetGlobalStoragePath();
        string backupPath = globalPath + ".backup";

        bool hadExistingFile = File.Exists(globalPath);
        if (hadExistingFile)
        {
            File.Copy(globalPath, backupPath, overwrite: true);
        }

        try
        {
            if (File.Exists(globalPath))
            {
                File.Delete(globalPath);
            }

            string absoluteDocPath = @"D:\OtherProjects\SomeApp\Controllers\HomeController.cs";

            var state = new BookmarkWorkspaceState();
            state.Bookmarks.Add(new BookmarkMetadata
            {
                BookmarkId = "absolute-path-test",
                DocumentPath = absoluteDocPath,
                LineNumber = 100,
                Group = string.Empty,
                StorageLocation = BookmarkStorageLocation.Global
            });
            state.FolderPaths.Add(string.Empty);

            await store.SaveWorkspaceToLocationAsync(string.Empty, BookmarkStorageLocation.Global, state, CancellationToken.None);

            BookmarkWorkspaceState loaded = await store.LoadWorkspaceFromLocationAsync(string.Empty, BookmarkStorageLocation.Global, CancellationToken.None);

            Assert.HasCount(1, loaded.Bookmarks);
            Assert.AreEqual(absoluteDocPath, loaded.Bookmarks[0].DocumentPath, "Absolute path should be preserved for global bookmarks");
        }
        finally
        {
            if (File.Exists(globalPath))
            {
                File.Delete(globalPath);
            }

            if (hadExistingFile)
            {
                File.Move(backupPath, globalPath);
            }
        }
    }
}
