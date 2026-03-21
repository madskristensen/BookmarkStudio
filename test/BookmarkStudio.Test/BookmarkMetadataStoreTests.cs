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
                CreatedUtc = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
                LastVisitedUtc = new DateTime(2024, 1, 16, 14, 0, 0, DateTimeKind.Utc),
                LastSeenUtc = new DateTime(2024, 1, 16, 14, 0, 0, DateTimeKind.Utc),
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
}
