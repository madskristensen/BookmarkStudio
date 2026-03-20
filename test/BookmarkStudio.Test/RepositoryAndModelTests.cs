using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

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
    public void ToManagedBookmarks_WhenCalled_OrdersBySlotThenPathThenLine()
    {
        BookmarkMetadata[] metadata =
        {
            new BookmarkMetadata { BookmarkId = "3", DocumentPath = @"C:\repo\b.cs", LineNumber = 8, SlotNumber = null },
            new BookmarkMetadata { BookmarkId = "2", DocumentPath = @"C:\repo\a.cs", LineNumber = 20, SlotNumber = 2 },
            new BookmarkMetadata { BookmarkId = "1", DocumentPath = @"C:\repo\a.cs", LineNumber = 10, SlotNumber = 1 },
            new BookmarkMetadata { BookmarkId = "4", DocumentPath = @"C:\repo\a.cs", LineNumber = 2, SlotNumber = null },
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
            ColumnNumber = 7,
            LineText = "  value  ",
        };

        BookmarkMetadata metadata = new BookmarkMetadata();

        metadata.UpdateFromSnapshot(snapshot, seenUtc);

        Assert.AreEqual(snapshot.DocumentPath, metadata.DocumentPath);
        Assert.AreEqual(snapshot.LineNumber, metadata.LineNumber);
        Assert.AreEqual(snapshot.ColumnNumber, metadata.ColumnNumber);
        Assert.AreEqual(snapshot.LineText, metadata.LineText);
        Assert.AreEqual(seenUtc, metadata.CreatedUtc);
        Assert.AreEqual(seenUtc, metadata.LastSeenUtc);
    }

    [TestMethod]
    public void UpdateFromSnapshot_WhenSnapshotNull_Throws()
    {
        BookmarkMetadata metadata = new BookmarkMetadata();

        Assert.ThrowsExactly<ArgumentNullException>(() => metadata.UpdateFromSnapshot(null!, DateTime.UtcNow));
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
            ColumnNumber = 3,
            LineText = "  preview text  ",
            SlotNumber = 4,
            Label = "label",
            Group = "group",
            Color = BookmarkColor.Teal,
            CreatedUtc = createdUtc,
            LastVisitedUtc = visitedUtc,
        };

        ManagedBookmark managed = metadata.ToManagedBookmark();

        Assert.AreEqual("id-1", managed.BookmarkId);
        Assert.AreEqual(@"C:\repo\file.cs", managed.DocumentPath);
        Assert.AreEqual(15, managed.LineNumber);
        Assert.AreEqual(3, managed.ColumnNumber);
        Assert.AreEqual("preview text", managed.LineText);
        Assert.AreEqual(4, managed.SlotNumber);
        Assert.AreEqual("label", managed.Label);
        Assert.AreEqual("group", managed.Group);
        Assert.AreEqual(BookmarkColor.Teal, managed.Color);
        Assert.AreEqual(createdUtc, managed.CreatedUtc);
        Assert.AreEqual(visitedUtc, managed.LastVisitedUtc);
    }

    [TestMethod]
    public void ManagedBookmark_Location_WhenDocumentPathEmpty_ReturnsEmpty()
    {
        ManagedBookmark bookmark = new ManagedBookmark { DocumentPath = string.Empty, LineNumber = 10 };

        Assert.AreEqual(string.Empty, bookmark.Location);
    }

    [TestMethod]
    public void ManagedBookmark_Location_WhenDocumentPathPresent_ReturnsFormattedValue()
    {
        ManagedBookmark bookmark = new ManagedBookmark { DocumentPath = @"C:\repo\file.cs", LineNumber = 10 };

        Assert.AreEqual(@"C:\repo\file.cs(10)", bookmark.Location);
    }

    [TestMethod]
    public void MetadataStore_GetStoragePath_WhenSolutionProvided_UsesVsFolder()
    {
        BookmarkMetadataStore store = new BookmarkMetadataStore();

        string path = store.GetStoragePath(@"C:\repo\demo.sln");

        Assert.AreEqual(@"C:\repo\.vs\bookmarks.json", path);
    }

    [TestMethod]
    public async Task MetadataStore_LoadAsync_WhenStorageMissing_ReturnsEmpty()
    {
        BookmarkMetadataStore store = new BookmarkMetadataStore();
        string solutionPath = CreateTestSolutionPath();

        IReadOnlyList<BookmarkMetadata> loaded = await store.LoadAsync(solutionPath, CancellationToken.None);

        Assert.AreEqual(0, loaded.Count);
    }

    [TestMethod]
    public async Task MetadataStore_SaveAndLoadAsync_WhenRoundTripped_PreservesMetadata()
    {
        BookmarkMetadataStore store = new BookmarkMetadataStore();
        string solutionPath = CreateTestSolutionPath();
        string solutionDirectory = Path.GetDirectoryName(solutionPath)!;
        DateTime now = new DateTime(2025, 02, 03, 04, 05, 06, DateTimeKind.Utc);

        BookmarkMetadata expected = new BookmarkMetadata
        {
            BookmarkId = "id-1",
            DocumentPath = Path.Combine(solutionDirectory, "src", "file.cs"),
            LineNumber = 23,
            ColumnNumber = 5,
            LineText = "preview",
            SlotNumber = 2,
            Label = "label",
            Group = "group",
            Color = BookmarkColor.Green,
            CreatedUtc = now,
            LastVisitedUtc = now.AddMinutes(5),
            LastSeenUtc = now,
        };

        await store.SaveAsync(solutionPath, new[] { expected }, CancellationToken.None);
        IReadOnlyList<BookmarkMetadata> loaded = await store.LoadAsync(solutionPath, CancellationToken.None);

        Assert.AreEqual(1, loaded.Count);
        Assert.AreEqual(expected.BookmarkId, loaded[0].BookmarkId);
        Assert.AreEqual(expected.DocumentPath, loaded[0].DocumentPath);
        Assert.AreEqual(expected.LineNumber, loaded[0].LineNumber);
        Assert.AreEqual(expected.ColumnNumber, loaded[0].ColumnNumber);
        Assert.AreEqual(expected.LineText, loaded[0].LineText);
        Assert.AreEqual(expected.SlotNumber, loaded[0].SlotNumber);
        Assert.AreEqual(expected.Label, loaded[0].Label);
        Assert.AreEqual(expected.Group, loaded[0].Group);
        Assert.AreEqual(expected.Color, loaded[0].Color);
        Assert.AreEqual(expected.CreatedUtc, loaded[0].CreatedUtc);
        Assert.AreEqual(expected.LastVisitedUtc, loaded[0].LastVisitedUtc);
        Assert.AreEqual(expected.LastSeenUtc, loaded[0].LastSeenUtc);
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
            ColumnNumber = 1,
            LineText = "line",
        };

        ManagedBookmark? added = await repository.ToggleAsync(solutionPath, snapshot, CancellationToken.None);

        Assert.IsNotNull(added);
        Assert.AreEqual(3, added.SlotNumber);
    }

    [TestMethod]
    public async Task ToggleAsync_WhenSnapshotExists_RemovesBookmark()
    {
        BookmarkMetadataStore store = new BookmarkMetadataStore();
        BookmarkRepositoryService repository = new BookmarkRepositoryService(store);
        string solutionPath = CreateTestSolutionPath();
        BookmarkSnapshot snapshot = new BookmarkSnapshot
        {
            DocumentPath = @"C:\repo\same.cs",
            LineNumber = 7,
            ColumnNumber = 1,
            LineText = "line",
        };

        ManagedBookmark? added = await repository.ToggleAsync(solutionPath, snapshot, CancellationToken.None);
        ManagedBookmark? removed = await repository.ToggleAsync(solutionPath, snapshot, CancellationToken.None);
        IReadOnlyList<ManagedBookmark> current = await repository.ListAsync(solutionPath, CancellationToken.None);

        Assert.IsNotNull(added);
        Assert.IsNull(removed);
        Assert.AreEqual(0, current.Count);
    }

    [TestMethod]
    public async Task TryUpdateAsync_WhenUpdateReturnsFalse_DoesNotPersistChanges()
    {
        BookmarkMetadataStore store = new BookmarkMetadataStore();
        BookmarkRepositoryService repository = new BookmarkRepositoryService(store);
        string solutionPath = CreateTestSolutionPath();
        DateTime now = new DateTime(2025, 04, 01, 0, 0, 0, DateTimeKind.Utc);

        await store.SaveAsync(solutionPath, new[]
        {
            new BookmarkMetadata { BookmarkId = "id-1", DocumentPath = @"C:\repo\a.cs", LineNumber = 1, Label = "original", LastSeenUtc = now }
        }, CancellationToken.None);

        await repository.TryUpdateAsync(solutionPath, metadata =>
        {
            metadata[0].Label = "changed";
            return false;
        }, CancellationToken.None);

        IReadOnlyList<BookmarkMetadata> loaded = await store.LoadAsync(solutionPath, CancellationToken.None);

        Assert.AreEqual("original", loaded[0].Label);
    }

    [TestMethod]
    public async Task UpdateAsync_WhenUpdateActionIsNull_Throws()
    {
        BookmarkMetadataStore store = new BookmarkMetadataStore();
        BookmarkRepositoryService repository = new BookmarkRepositoryService(store);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() =>
            repository.UpdateAsync(CreateTestSolutionPath(), null!, CancellationToken.None));
    }

    [TestMethod]
    public async Task TryUpdateAsync_WhenUpdateActionIsNull_Throws()
    {
        BookmarkMetadataStore store = new BookmarkMetadataStore();
        BookmarkRepositoryService repository = new BookmarkRepositoryService(store);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() =>
            repository.TryUpdateAsync(CreateTestSolutionPath(), null!, CancellationToken.None));
    }

    [TestMethod]
    public async Task ToggleAsync_WhenSnapshotIsNull_Throws()
    {
        BookmarkMetadataStore store = new BookmarkMetadataStore();
        BookmarkRepositoryService repository = new BookmarkRepositoryService(store);

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() =>
            repository.ToggleAsync(CreateTestSolutionPath(), null!, CancellationToken.None));
    }

    [TestMethod]
    public async Task ToggleAsync_WhenAllSlotsAssigned_AssignsNoSlot()
    {
        BookmarkMetadataStore store = new BookmarkMetadataStore();
        BookmarkRepositoryService repository = new BookmarkRepositoryService(store);
        string solutionPath = CreateTestSolutionPath();
        DateTime now = new DateTime(2025, 03, 04, 12, 00, 00, DateTimeKind.Utc);

        BookmarkMetadata[] existing = Enumerable.Range(1, 9)
            .Select(slot => new BookmarkMetadata
            {
                BookmarkId = string.Concat("id-", slot.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                DocumentPath = string.Concat(@"C:\repo\", slot.ToString(System.Globalization.CultureInfo.InvariantCulture), ".cs"),
                LineNumber = slot,
                SlotNumber = slot,
                LastSeenUtc = now,
            })
            .ToArray();

        await store.SaveAsync(solutionPath, existing, CancellationToken.None);

        ManagedBookmark? added = await repository.ToggleAsync(solutionPath, new BookmarkSnapshot
        {
            DocumentPath = @"C:\repo\new.cs",
            LineNumber = 100,
            ColumnNumber = 1,
            LineText = "line",
        }, CancellationToken.None);

        Assert.IsNotNull(added);
        Assert.IsNull(added.SlotNumber);
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

    [TestMethod]
    public async Task MetadataStore_LoadAsync_WhenBookmarksNodeIsNull_ReturnsEmpty()
    {
        BookmarkMetadataStore store = new BookmarkMetadataStore();
        string solutionPath = CreateTestSolutionPath();
        string storagePath = store.GetStoragePath(solutionPath);
        Directory.CreateDirectory(Path.GetDirectoryName(storagePath)!);
        File.WriteAllText(storagePath, "{\"bookmarks\":null}");

        IReadOnlyList<BookmarkMetadata> loaded = await store.LoadAsync(solutionPath, CancellationToken.None);

        Assert.AreEqual(0, loaded.Count);
    }

    [TestMethod]
    public async Task MetadataStore_LoadAsync_WhenBookmarkFieldsMissing_NormalizesValues()
    {
        BookmarkMetadataStore store = new BookmarkMetadataStore();
        string solutionPath = CreateTestSolutionPath();
        string solutionDirectory = Path.GetDirectoryName(solutionPath)!;
        string storagePath = store.GetStoragePath(solutionPath);
        Directory.CreateDirectory(Path.GetDirectoryName(storagePath)!);
        File.WriteAllText(storagePath,
            "{\"bookmarks\":[{\"id\":\"\",\"documentPath\":\"src\\\\file.cs\",\"lineNumber\":3,\"columnNumber\":1,\"lineText\":null,\"label\":null,\"group\":null,\"color\":\"orange\",\"createdUtc\":\"invalid\",\"lastSeenUtc\":\"2025-01-02 03:04:05\"}]}");

        IReadOnlyList<BookmarkMetadata> loaded = await store.LoadAsync(solutionPath, CancellationToken.None);

        Assert.AreEqual(1, loaded.Count);
        Assert.AreNotEqual(string.Empty, loaded[0].BookmarkId);
        Assert.AreEqual(Path.Combine(solutionDirectory, "src", "file.cs"), loaded[0].DocumentPath);
        Assert.AreEqual(string.Empty, loaded[0].LineText);
        Assert.AreEqual(string.Empty, loaded[0].Label);
        Assert.AreEqual(string.Empty, loaded[0].Group);
        Assert.AreEqual(new DateTime(2025, 01, 02, 03, 04, 05, DateTimeKind.Utc), loaded[0].CreatedUtc);
        Assert.AreEqual(new DateTime(2025, 01, 02, 03, 04, 05, DateTimeKind.Utc), loaded[0].LastSeenUtc);
    }

    [TestMethod]
    public async Task MetadataStore_LoadAsync_WhenDatesMissing_AssignsNonDefaultUtcValues()
    {
        BookmarkMetadataStore store = new BookmarkMetadataStore();
        string solutionPath = CreateTestSolutionPath();
        string storagePath = store.GetStoragePath(solutionPath);
        Directory.CreateDirectory(Path.GetDirectoryName(storagePath)!);
        File.WriteAllText(storagePath,
            "{\"bookmarks\":[{\"id\":\"id-1\",\"documentPath\":\"src\\\\file.cs\",\"lineNumber\":3,\"columnNumber\":1,\"lineText\":\"x\",\"color\":\"orange\"}]}");

        IReadOnlyList<BookmarkMetadata> loaded = await store.LoadAsync(solutionPath, CancellationToken.None);

        Assert.AreEqual(1, loaded.Count);
        Assert.AreNotEqual(default, loaded[0].CreatedUtc);
        Assert.AreNotEqual(default, loaded[0].LastSeenUtc);
    }

    private static string CreateTestSolutionPath()
    {
        string testRoot = Path.Combine(Path.GetTempPath(), "BookmarkStudio.Tests", Guid.NewGuid().ToString("N"));
        return Path.Combine(testRoot, "demo.sln");
    }
}