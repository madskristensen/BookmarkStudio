using System.Windows;

namespace BookmarkStudio.Test;

[TestClass]
public class BookmarkManagerViewModelTests
{
    [TestMethod]
    public void ApplySearchText_WhenMatchingLabelAndColor_FiltersVisibleBookmarks()
    {
        var viewModel = new BookmarkManagerViewModel();
        viewModel.LoadForTests(new[]
        {
            new ManagedBookmark
            {
                BookmarkId = "id-1",
                DocumentPath = @"C:\repo\a.cs",
                LineNumber = 10,
                LineText = "alpha",
                Label = "Important",
                Color = BookmarkColor.Blue,
                ShortcutNumber = 1,
                Group = "FolderA",
            },
            new ManagedBookmark
            {
                BookmarkId = "id-2",
                DocumentPath = @"C:\repo\b.cs",
                LineNumber = 20,
                LineText = "beta",
                Label = "Other",
                Color = BookmarkColor.None,
                ShortcutNumber = 2,
                Group = "FolderB",
            },
        }, new[] { string.Empty, "FolderA", "FolderB" });

        int labelMatches = viewModel.ApplySearchText("important");
        int colorMatches = viewModel.ApplySearchText("blue");

        Assert.AreEqual(1, labelMatches);
        Assert.AreEqual(1, colorMatches);
    }

    [TestMethod]
    public void SelectBookmark_WhenBookmarkIdMissing_ClearsSelection()
    {
        var viewModel = new BookmarkManagerViewModel();
        viewModel.LoadForTests(new[]
        {
            new ManagedBookmark { BookmarkId = "id-1", DocumentPath = @"C:\repo\a.cs", LineNumber = 1, Group = "FolderA" },
        }, new[] { string.Empty, "FolderA" });
        viewModel.SelectBookmark("id-1");

        viewModel.SelectBookmark("missing");

        Assert.IsNull(viewModel.SelectedBookmark);
    }

    [TestMethod]
    public void SelectBookmark_WhenBookmarkExists_SelectsBookmarkNode()
    {
        var viewModel = new BookmarkManagerViewModel();
        viewModel.LoadForTests(new[]
        {
            new ManagedBookmark { BookmarkId = "id-1", DocumentPath = @"C:\repo\a.cs", LineNumber = 1, Group = "FolderA" },
        }, new[] { string.Empty, "FolderA" });

        viewModel.SelectBookmark("id-1");

        Assert.IsNotNull(viewModel.SelectedBookmark);
        Assert.AreEqual("id-1", viewModel.SelectedBookmark.BookmarkId);
        Assert.AreEqual("FolderA", viewModel.SelectedFolderPath);
    }

    [TestMethod]
    public void SelectFolder_WhenFolderExists_SelectsFolderNode()
    {
        var viewModel = new BookmarkManagerViewModel();
        viewModel.LoadForTests(Array.Empty<ManagedBookmark>(), new[] { string.Empty, "FolderA", "FolderA/Sub" });

        viewModel.SelectFolder("FolderA/Sub");

        Assert.IsNull(viewModel.SelectedBookmark);
        Assert.AreEqual("FolderA/Sub", viewModel.SelectedFolderPath);
        Assert.IsTrue(viewModel.HasSelectedFolder);
    }

    [TestMethod]
    public void Clear_WhenCalled_ResetsState()
    {
        var viewModel = new BookmarkManagerViewModel();
        viewModel.LoadForTests(new[]
        {
            new ManagedBookmark { BookmarkId = "id-1", DocumentPath = @"C:\repo\a.cs", LineNumber = 1, Group = "FolderA" },
        }, new[] { string.Empty, "FolderA" });
        viewModel.SelectBookmark("id-1");

        viewModel.Clear();

        Assert.IsEmpty(viewModel.RootNodes);
        Assert.IsNull(viewModel.SelectedBookmark);
        Assert.AreEqual(string.Empty, viewModel.SearchText);
        Assert.AreEqual("No solution loaded.", viewModel.StatusText);
    }

    [TestMethod]
    public void LoadForTests_WhenBookmarksAndFoldersUnordered_BuildsDeterministicTreeOrdering()
    {
        var viewModel = new BookmarkManagerViewModel();
        viewModel.LoadForTests(new[]
        {
            new ManagedBookmark { BookmarkId = "root-2", DocumentPath = @"C:\repo\z.cs", LineNumber = 20, Group = string.Empty },
            new ManagedBookmark { BookmarkId = "root-1", DocumentPath = @"C:\repo\a.cs", LineNumber = 5, Group = string.Empty },
            new ManagedBookmark { BookmarkId = "a-2", DocumentPath = @"C:\repo\a2.cs", LineNumber = 12, Group = "Team/Backlog" },
            new ManagedBookmark { BookmarkId = "a-1", DocumentPath = @"C:\repo\a1.cs", LineNumber = 4, Group = "Team/Backlog" },
        }, new[] { string.Empty, "Zeta", "Team", "Team/Backlog" });

        Assert.HasCount(1, viewModel.RootNodes);
        Assert.AreEqual("Root", viewModel.RootNodes[0].DisplayText);

        var rootFolder = (FolderNodeViewModel)viewModel.RootNodes[0];
        Assert.AreEqual("Team", rootFolder.Children[0].DisplayText);
        Assert.AreEqual("Zeta", rootFolder.Children[1].DisplayText);
        Assert.AreEqual("a.cs", rootFolder.Children[2].DisplayText);
        Assert.AreEqual("z.cs", rootFolder.Children[3].DisplayText);

        var teamFolder = (FolderNodeViewModel)rootFolder.Children[0];
        var backlogFolder = (FolderNodeViewModel)teamFolder.Children[0];

        CollectionAssert.AreEqual(
            new[] { "a-1", "a-2" },
            backlogFolder.Children
                .OfType<BookmarkItemNodeViewModel>()
                .Select(node => node.Bookmark.BookmarkId)
                .ToArray());
    }

    [TestMethod]
    public void ApplySearchText_WhenSelectedBookmarkStillVisible_PreservesSelection()
    {
        var viewModel = new BookmarkManagerViewModel();
        viewModel.LoadForTests(new[]
        {
            new ManagedBookmark { BookmarkId = "id-1", DocumentPath = @"C:\repo\a.cs", LineNumber = 1, Label = "alpha", Group = "FolderA" },
            new ManagedBookmark { BookmarkId = "id-2", DocumentPath = @"C:\repo\b.cs", LineNumber = 2, Label = "beta important", Group = "FolderA" },
        }, new[] { string.Empty, "FolderA" });
        viewModel.SelectBookmark("id-2");

        int visible = viewModel.ApplySearchText("beta");

        Assert.AreEqual(1, visible);
        Assert.IsNotNull(viewModel.SelectedBookmark);
        Assert.AreEqual("id-2", viewModel.SelectedBookmark.BookmarkId);
        Assert.AreEqual("FolderA", viewModel.SelectedFolderPath);
    }

    [TestMethod]
    public void ApplySearchText_WhenSelectedBookmarkFilteredOut_SelectsFirstVisibleNode()
    {
        var viewModel = new BookmarkManagerViewModel();
        viewModel.LoadForTests(new[]
        {
            new ManagedBookmark { BookmarkId = "id-1", DocumentPath = @"C:\repo\a.cs", LineNumber = 1, Label = "alpha", Group = string.Empty },
            new ManagedBookmark { BookmarkId = "id-2", DocumentPath = @"C:\repo\b.cs", LineNumber = 2, Label = "beta", Group = string.Empty },
        }, new[] { string.Empty });
        viewModel.SelectBookmark("id-2");

        int visible = viewModel.ApplySearchText("alpha");

        Assert.AreEqual(1, visible);
        Assert.IsNotNull(viewModel.SelectedBookmark);
        Assert.AreEqual("id-1", viewModel.SelectedBookmark.BookmarkId);
    }

    [TestMethod]
    public void LoadForTests_WhenFolderHasNoBookmarks_AddsFolderPlaceholderRow()
    {
        var viewModel = new BookmarkManagerViewModel();
        viewModel.LoadForTests(Array.Empty<ManagedBookmark>(), new[] { string.Empty, "FolderA", "FolderA/Sub" });

        Assert.HasCount(2, viewModel.BookmarkRows);
        Assert.IsTrue(viewModel.BookmarkRows.All(row => row.IsFolderPlaceholder));
        CollectionAssert.AreEquivalent(
            new[] { "FolderA", "FolderA/Sub" },
            viewModel.BookmarkRows.Select(row => row.FolderPath).ToArray());
    }

    [TestMethod]
    public void LoadForTests_WhenFolderContainsBookmarks_DoesNotCreatePlaceholderForThatFolder()
    {
        var viewModel = new BookmarkManagerViewModel();
        viewModel.LoadForTests(new[]
        {
            new ManagedBookmark { BookmarkId = "id-1", DocumentPath = @"C:\repo\a.cs", LineNumber = 1, Group = "FolderA" },
        }, new[] { string.Empty, "FolderA", "FolderB" });

        BookmarkGridRowViewModel folderA = viewModel.BookmarkRows.Single(row => string.Equals(row.FolderPath, "FolderA", StringComparison.Ordinal));
        BookmarkGridRowViewModel folderB = viewModel.BookmarkRows.Single(row => string.Equals(row.FolderPath, "FolderB", StringComparison.Ordinal));

        Assert.IsFalse(folderA.IsFolderPlaceholder);
        Assert.IsTrue(folderB.IsFolderPlaceholder);
    }

    [TestMethod]
    public void BookmarkItemNodeViewModel_WhenTreeDepthIsOne_DoesNotOffsetNonNameColumns()
    {
        var node = new BookmarkItemNodeViewModel(
            new ManagedBookmark { BookmarkId = "id-1", DocumentPath = @"C:\repo\a.cs", LineNumber = 10 },
            treeDepth: 1);

        Assert.AreEqual(new Thickness(0, 0, 0, 0), node.NonNameColumnMargin);
        Assert.AreEqual(new Thickness(0, 0, 4, 0), node.NonNameRightAlignedMargin);
    }

    [TestMethod]
    public void BookmarkItemNodeViewModel_WhenTreeDepthIsNested_OffsetsNonNameColumnsByNestedLevels()
    {
        var node = new BookmarkItemNodeViewModel(
            new ManagedBookmark { BookmarkId = "id-1", DocumentPath = @"C:\repo\a.cs", LineNumber = 10 },
            treeDepth: 3);

        Assert.AreEqual(new Thickness(-38, 0, 0, 0), node.NonNameColumnMargin);
        Assert.AreEqual(new Thickness(-38, 0, 4, 0), node.NonNameRightAlignedMargin);
    }

    [TestMethod]
    public void FilterColor_WhenSet_FiltersBookmarksByColor()
    {
        var viewModel = new BookmarkManagerViewModel();
        viewModel.LoadForTests(new[]
        {
            new ManagedBookmark { BookmarkId = "id-1", DocumentPath = @"C:\repo\a.cs", LineNumber = 1, Color = BookmarkColor.Blue, Group = string.Empty },
            new ManagedBookmark { BookmarkId = "id-2", DocumentPath = @"C:\repo\b.cs", LineNumber = 2, Color = BookmarkColor.Green, Group = string.Empty },
            new ManagedBookmark { BookmarkId = "id-3", DocumentPath = @"C:\repo\c.cs", LineNumber = 3, Color = BookmarkColor.Blue, Group = string.Empty },
        }, new[] { string.Empty });

        viewModel.FilterColor = BookmarkColor.Blue;
        int visibleCount = CountVisibleBookmarks(viewModel.RootNodes);

        Assert.AreEqual(2, visibleCount);
    }

    [TestMethod]
    public void FilterColor_WhenCleared_ShowsAllBookmarks()
    {
        var viewModel = new BookmarkManagerViewModel();
        viewModel.LoadForTests(new[]
        {
            new ManagedBookmark { BookmarkId = "id-1", DocumentPath = @"C:\repo\a.cs", LineNumber = 1, Color = BookmarkColor.Blue, Group = string.Empty },
            new ManagedBookmark { BookmarkId = "id-2", DocumentPath = @"C:\repo\b.cs", LineNumber = 2, Color = BookmarkColor.Green, Group = string.Empty },
        }, new[] { string.Empty });
        viewModel.FilterColor = BookmarkColor.Blue;

        viewModel.FilterColor = null;
        int visibleCount = CountVisibleBookmarks(viewModel.RootNodes);

        Assert.AreEqual(2, visibleCount);
    }

    [TestMethod]
    public void GetShortcutAssignments_WhenBookmarksHaveShortcuts_ReturnsDictionary()
    {
        var viewModel = new BookmarkManagerViewModel();
        viewModel.LoadForTests(new[]
        {
            new ManagedBookmark { BookmarkId = "id-1", DocumentPath = @"C:\repo\a.cs", LineNumber = 1, ShortcutNumber = 1, Label = "First", Group = string.Empty },
            new ManagedBookmark { BookmarkId = "id-2", DocumentPath = @"C:\repo\b.cs", LineNumber = 2, ShortcutNumber = 3, Label = "Third", Group = string.Empty },
            new ManagedBookmark { BookmarkId = "id-3", DocumentPath = @"C:\repo\c.cs", LineNumber = 3, ShortcutNumber = null, Label = "NoShortcut", Group = string.Empty },
        }, new[] { string.Empty });

        Dictionary<int, string> assignments = viewModel.GetShortcutAssignments();

        Assert.HasCount(2, assignments);
        Assert.AreEqual("First", assignments[1]);
        Assert.AreEqual("Third", assignments[3]);
    }

    [TestMethod]
    public void GetShortcutAssignments_WhenLabelIsEmpty_UsesFileName()
    {
        var viewModel = new BookmarkManagerViewModel();
        viewModel.LoadForTests(new[]
        {
            new ManagedBookmark { BookmarkId = "id-1", DocumentPath = @"C:\repo\MyFile.cs", LineNumber = 1, ShortcutNumber = 5, Label = "", Group = string.Empty },
        }, new[] { string.Empty });

        Dictionary<int, string> assignments = viewModel.GetShortcutAssignments();

        Assert.HasCount(1, assignments);
        Assert.AreEqual("MyFile.cs", assignments[5]);
    }

    [TestMethod]
    public void GetShortcutAssignments_WhenNoShortcutsAssigned_ReturnsEmptyDictionary()
    {
        var viewModel = new BookmarkManagerViewModel();
        viewModel.LoadForTests(new[]
        {
            new ManagedBookmark { BookmarkId = "id-1", DocumentPath = @"C:\repo\a.cs", LineNumber = 1, ShortcutNumber = null, Group = string.Empty },
        }, new[] { string.Empty });

        Dictionary<int, string> assignments = viewModel.GetShortcutAssignments();

        Assert.IsEmpty(assignments);
    }

    private static int CountVisibleBookmarks(IEnumerable<BookmarkNodeViewModel> nodes)
    {
        int count = 0;
        foreach (BookmarkNodeViewModel node in nodes)
        {
            if (node is BookmarkItemNodeViewModel)
            {
                count++;
            }
            else if (node is FolderNodeViewModel folder)
            {
                count += CountVisibleBookmarks(folder.Children);
            }
        }

        return count;
    }
}
