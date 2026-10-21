namespace BookmarkStudio.Test;

[TestClass]
public class BookmarkStudioSessionTests
{
    [TestMethod]
    [DataRow(BookmarkStorageLocation.Global)]
    [DataRow(BookmarkStorageLocation.Personal)]
    [DataRow(BookmarkStorageLocation.Workspace)]
    public void ApplyWorkspaceUpdate_WhenAddingFilesSequentially_PreservesExistingBookmarks(BookmarkStorageLocation location)
    {
        DualBookmarkWorkspaceState state = CreateState();
        BookmarkWorkspaceState target = state.GetStateForLocation(location);
        string[] originalIds = target.Bookmarks.Select(bookmark => bookmark.BookmarkId).ToArray();

        foreach (var id in new[] { "file-1", "file-2" })
        {
            BookmarkWorkspaceState updated = BookmarkStudioSession.ApplyWorkspaceUpdate(state, location, workspace =>
                workspace.Bookmarks.Add(new BookmarkMetadata
                {
                    BookmarkId = id,
                    DocumentPath = @"C:\files\" + id + ".txt",
                    LineNumber = 0,
                    StorageLocation = location,
                }));

            Assert.AreSame(target, updated, "The state saved must be the selected storage location's state.");
        }

        CollectionAssert.AreEquivalent(originalIds.Concat(new[] { "file-1", "file-2" }).ToArray(),
            target.Bookmarks.Select(bookmark => bookmark.BookmarkId).ToArray());
        Assert.IsTrue(target.Bookmarks.All(bookmark => bookmark.StorageLocation == location));
        AssertOtherLocationsUnchanged(state, location);
    }

    [TestMethod]
    [DataRow(BookmarkStorageLocation.Global, true)]
    [DataRow(BookmarkStorageLocation.Global, false)]
    [DataRow(BookmarkStorageLocation.Personal, true)]
    [DataRow(BookmarkStorageLocation.Personal, false)]
    [DataRow(BookmarkStorageLocation.Workspace, true)]
    [DataRow(BookmarkStorageLocation.Workspace, false)]
    public void ApplyWorkspaceUpdate_WhenReordering_UpdatesOnlySelectedLocation(BookmarkStorageLocation location, bool useDragDrop)
    {
        DualBookmarkWorkspaceState state = CreateState();
        BookmarkWorkspaceState target = state.GetStateForLocation(location);
        var firstId = location + "-1";
        var secondId = location + "-2";

        BookmarkWorkspaceState updated = BookmarkStudioSession.ApplyWorkspaceUpdate(state, location, workspace =>
        {
            if (useDragDrop)
            {
                BookmarkRepositoryService.ReorderBookmark(workspace, secondId, string.Empty, firstId, placeBefore: true);
            }
            else
            {
                BookmarkRepositoryService.MoveBookmarkWithinFolder(workspace, secondId, -1);
            }
        });

        Assert.AreSame(target, updated);
        CollectionAssert.AreEqual(new[] { secondId, firstId },
            updated.Bookmarks.OrderBy(bookmark => bookmark.SortIndex).Select(bookmark => bookmark.BookmarkId).ToArray());
        Assert.AreEqual(0, updated.Bookmarks.Single(bookmark => bookmark.BookmarkId == secondId).SortIndex);
        Assert.AreEqual(1, updated.Bookmarks.Single(bookmark => bookmark.BookmarkId == firstId).SortIndex);
        AssertOtherLocationsUnchanged(state, location);
    }

    [TestMethod]
    [DataRow(BookmarkStorageLocation.Global)]
    [DataRow(BookmarkStorageLocation.Personal)]
    [DataRow(BookmarkStorageLocation.Workspace)]
    public void ApplyWorkspaceUpdate_WhenClearing_RemovesOnlySelectedLocationsBookmarksAndFolders(BookmarkStorageLocation location)
    {
        DualBookmarkWorkspaceState state = CreateState();
        BookmarkWorkspaceState target = state.GetStateForLocation(location);

        BookmarkWorkspaceState updated = BookmarkStudioSession.ApplyWorkspaceUpdate(state, location, workspace =>
        {
            workspace.Bookmarks.Clear();
            workspace.FolderPaths.Clear();
            workspace.FolderPaths.Add(string.Empty);
        });

        Assert.AreSame(target, updated);
        Assert.IsEmpty(target.Bookmarks);
        CollectionAssert.AreEquivalent(new[] { string.Empty }, target.FolderPaths.ToArray());
        AssertOtherLocationsUnchanged(state, location);
    }

    private static DualBookmarkWorkspaceState CreateState()
    {
        return new DualBookmarkWorkspaceState(
            CreateWorkspace(BookmarkStorageLocation.Global),
            CreateWorkspace(BookmarkStorageLocation.Personal),
            CreateWorkspace(BookmarkStorageLocation.Workspace));
    }

    private static BookmarkWorkspaceState CreateWorkspace(BookmarkStorageLocation location)
    {
        var workspace = new BookmarkWorkspaceState();
        workspace.FolderPaths.Add(string.Empty);
        workspace.FolderPaths.Add("EmptyFolder");
        for (var index = 0; index < 2; index++)
        {
            workspace.Bookmarks.Add(new BookmarkMetadata
            {
                BookmarkId = location + "-" + (index + 1),
                DocumentPath = @"C:\files\" + location + index + ".txt",
                LineNumber = 0,
                SortIndex = index,
                StorageLocation = location,
            });
        }

        return workspace;
    }

    private static void AssertOtherLocationsUnchanged(DualBookmarkWorkspaceState state, BookmarkStorageLocation updatedLocation)
    {
        foreach (BookmarkStorageLocation location in Enum.GetValues(typeof(BookmarkStorageLocation)))
        {
            if (location == updatedLocation)
            {
                continue;
            }

            BookmarkWorkspaceState workspace = state.GetStateForLocation(location);
            CollectionAssert.AreEqual(new[] { location + "-1", location + "-2" },
                workspace.Bookmarks.Select(bookmark => bookmark.BookmarkId).ToArray());
            CollectionAssert.AreEqual(new[] { 0, 1 }, workspace.Bookmarks.Select(bookmark => bookmark.SortIndex).ToArray());
            CollectionAssert.AreEquivalent(new[] { string.Empty, "EmptyFolder" }, workspace.FolderPaths.ToArray());
        }
    }
}