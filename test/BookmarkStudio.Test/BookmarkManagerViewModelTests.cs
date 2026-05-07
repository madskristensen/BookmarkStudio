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
    public void SelectBookmark_WhenBookmarkExists_SetsIsSelectedOnNode()
    {
        var viewModel = new BookmarkManagerViewModel();
        viewModel.LoadForTests(new[]
        {
            new ManagedBookmark { BookmarkId = "id-1", DocumentPath = @"C:\repo\a.cs", LineNumber = 1, Group = "FolderA" },
            new ManagedBookmark { BookmarkId = "id-2", DocumentPath = @"C:\repo\b.cs", LineNumber = 2, Group = "FolderA" },
        }, new[] { string.Empty, "FolderA" });

        viewModel.SelectBookmark("id-1");

        Assert.IsTrue(viewModel.SelectedNode.IsSelected);
    }

    [TestMethod]
    public void SelectBookmark_WhenSwitchingBookmarks_ClearsIsSelectedOnPreviousNode()
    {
        var viewModel = new BookmarkManagerViewModel();
        viewModel.LoadForTests(new[]
        {
            new ManagedBookmark { BookmarkId = "id-1", DocumentPath = @"C:\repo\a.cs", LineNumber = 1, Group = string.Empty },
            new ManagedBookmark { BookmarkId = "id-2", DocumentPath = @"C:\repo\b.cs", LineNumber = 2, Group = string.Empty },
        }, new[] { string.Empty });
        viewModel.SelectBookmark("id-1");
        var firstNode = viewModel.SelectedNode;

        viewModel.SelectBookmark("id-2");

        Assert.IsFalse(firstNode.IsSelected);
        Assert.IsTrue(viewModel.SelectedNode.IsSelected);
        Assert.AreEqual("id-2", viewModel.SelectedBookmark.BookmarkId);
    }

    [TestMethod]
    public void SelectBookmark_WhenBookmarkInNestedFolder_ExpandsAncestorFolders()
    {
        var viewModel = new BookmarkManagerViewModel();
        viewModel.LoadForTests(new[]
        {
            new ManagedBookmark { BookmarkId = "id-1", DocumentPath = @"C:\repo\a.cs", LineNumber = 1, Group = "Team/Backlog" },
        }, new[] { string.Empty, "Team", "Team/Backlog" });

        // Collapse all folders first
        var root = (FolderNodeViewModel)viewModel.RootNodes[0];
        var teamFolder = (FolderNodeViewModel)root.Children[0];
        var backlogFolder = (FolderNodeViewModel)teamFolder.Children[0];
        teamFolder.IsExpanded = false;
        backlogFolder.IsExpanded = false;

        viewModel.SelectBookmark("id-1");

        Assert.IsTrue(teamFolder.IsExpanded);
        Assert.IsTrue(backlogFolder.IsExpanded);
        Assert.IsNotNull(viewModel.SelectedBookmark);
        Assert.AreEqual("id-1", viewModel.SelectedBookmark.BookmarkId);
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

    private static List<string> CollectBookmarkIdsInOrder(IEnumerable<BookmarkNodeViewModel> nodes)
    {
        var result = new List<string>();
        foreach (BookmarkNodeViewModel node in nodes)
        {
            if (node is BookmarkItemNodeViewModel bookmarkNode)
            {
                result.Add(bookmarkNode.Bookmark.BookmarkId);
            }
            else if (node is FolderNodeViewModel folder)
            {
                result.AddRange(CollectBookmarkIdsInOrder(folder.Children));
            }
        }

        return result;
    }

    [TestMethod]
    public void SortMode_WhenAlphabetical_OrdersBookmarksByLabel()
    {
        var viewModel = new BookmarkManagerViewModel();
        viewModel.LoadForTests(new[]
        {
            new ManagedBookmark { BookmarkId = "id-c", DocumentPath = @"C:\repo\c.cs", LineNumber = 1, Label = "Charlie", Group = string.Empty },
            new ManagedBookmark { BookmarkId = "id-a", DocumentPath = @"C:\repo\a.cs", LineNumber = 2, Label = "Alpha", Group = string.Empty },
            new ManagedBookmark { BookmarkId = "id-b", DocumentPath = @"C:\repo\b.cs", LineNumber = 3, Label = "Bravo", Group = string.Empty },
        }, new[] { string.Empty });

        viewModel.SortMode = BookmarkSortMode.Alphabetical;
        List<string> order = CollectBookmarkIdsInOrder(viewModel.RootNodes);

        CollectionAssert.AreEqual(new[] { "id-a", "id-b", "id-c" }, order);
    }

    [TestMethod]
    public void SortMode_WhenAlphabeticalAndLabelEmpty_FallsBackToFileName()
    {
        var viewModel = new BookmarkManagerViewModel();
        viewModel.LoadForTests(new[]
        {
            new ManagedBookmark { BookmarkId = "id-z", DocumentPath = @"C:\repo\zeta.cs", LineNumber = 1, Label = "", Group = string.Empty },
            new ManagedBookmark { BookmarkId = "id-a", DocumentPath = @"C:\repo\alpha.cs", LineNumber = 2, Label = "", Group = string.Empty },
        }, new[] { string.Empty });

        viewModel.SortMode = BookmarkSortMode.Alphabetical;
        List<string> order = CollectBookmarkIdsInOrder(viewModel.RootNodes);

        CollectionAssert.AreEqual(new[] { "id-a", "id-z" }, order);
    }

    [TestMethod]
    public void SortMode_WhenSlot_AssignedShortcutsFirstThenUnassigned()
    {
        var viewModel = new BookmarkManagerViewModel();
        viewModel.LoadForTests(new[]
        {
            new ManagedBookmark { BookmarkId = "no-slot", DocumentPath = @"C:\repo\a.cs", LineNumber = 1, ShortcutNumber = null, Group = string.Empty },
            new ManagedBookmark { BookmarkId = "slot-3", DocumentPath = @"C:\repo\b.cs", LineNumber = 2, ShortcutNumber = 3, Group = string.Empty },
            new ManagedBookmark { BookmarkId = "slot-1", DocumentPath = @"C:\repo\c.cs", LineNumber = 3, ShortcutNumber = 1, Group = string.Empty },
        }, new[] { string.Empty });

        viewModel.SortMode = BookmarkSortMode.Slot;
        List<string> order = CollectBookmarkIdsInOrder(viewModel.RootNodes);

        CollectionAssert.AreEqual(new[] { "slot-1", "slot-3", "no-slot" }, order);
    }

    [TestMethod]
    public void SortMode_WhenCreated_NewestFirst()
    {
        var now = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var bookmarks = new[]
        {
            new ManagedBookmark { BookmarkId = "old", DocumentPath = @"C:\repo\a.cs", LineNumber = 1, Group = string.Empty, CreatedUtc = now.AddDays(-10) },
            new ManagedBookmark { BookmarkId = "newest", DocumentPath = @"C:\repo\b.cs", LineNumber = 2, Group = string.Empty, CreatedUtc = now },
            new ManagedBookmark { BookmarkId = "middle", DocumentPath = @"C:\repo\c.cs", LineNumber = 3, Group = string.Empty, CreatedUtc = now.AddDays(-5) },
        };

        var viewModel = new BookmarkManagerViewModel();
        viewModel.LoadForTests(bookmarks, new[] { string.Empty });
        viewModel.SortMode = BookmarkSortMode.Created;
        List<string> order = CollectBookmarkIdsInOrder(viewModel.RootNodes);

        CollectionAssert.AreEqual(new[] { "newest", "middle", "old" }, order);
    }

    [TestMethod]
    public void GroupMode_WhenColor_ProducesFlatListOrderedByColor()
    {
        var viewModel = new BookmarkManagerViewModel();
        viewModel.LoadForTests(new[]
        {
            new ManagedBookmark { BookmarkId = "g1", DocumentPath = @"C:\repo\b.cs", LineNumber = 2, Color = BookmarkColor.Green, Group = "FolderB" },
            new ManagedBookmark { BookmarkId = "b1", DocumentPath = @"C:\repo\a.cs", LineNumber = 1, Color = BookmarkColor.Blue, Group = "FolderA" },
            new ManagedBookmark { BookmarkId = "b2", DocumentPath = @"C:\repo\c.cs", LineNumber = 3, Color = BookmarkColor.Blue, Group = "FolderA" },
        }, new[] { string.Empty, "FolderA", "FolderB" });

        viewModel.GroupMode = BookmarkGroupMode.Color;

        var rootFolder = (FolderNodeViewModel)viewModel.RootNodes[0];
        Assert.IsTrue(rootFolder.Children.All(n => n is BookmarkItemNodeViewModel),
            "Grouped view should be a flat list of bookmark items - no folder nodes.");

        var order = rootFolder.Children.OfType<BookmarkItemNodeViewModel>().Select(n => n.Bookmark.BookmarkId).ToArray();
        // Blue comes before Green by enum value (Red=1 < Blue=5 < Green=4)... actually enum is Red=1,Orange=2,Yellow=3,Green=4,Blue=5.
        // So Green(4) before Blue(5).
        CollectionAssert.AreEqual(new[] { "g1", "b1", "b2" }, order);
    }

    [TestMethod]
    public void GroupMode_WhenFile_ProducesFlatListOrderedByFileName()
    {
        var viewModel = new BookmarkManagerViewModel();
        viewModel.LoadForTests(new[]
        {
            new ManagedBookmark { BookmarkId = "b1", DocumentPath = @"C:\repo\b.cs", LineNumber = 2, Group = "Y" },
            new ManagedBookmark { BookmarkId = "a2", DocumentPath = @"C:\repo\sub\a.cs", LineNumber = 5, Group = "Z" },
            new ManagedBookmark { BookmarkId = "a1", DocumentPath = @"C:\repo\a.cs", LineNumber = 1, Group = "X" },
        }, new[] { string.Empty, "X", "Y", "Z" });

        viewModel.GroupMode = BookmarkGroupMode.File;

        var rootFolder = (FolderNodeViewModel)viewModel.RootNodes[0];
        Assert.IsTrue(rootFolder.Children.All(n => n is BookmarkItemNodeViewModel),
            "Grouped view should be a flat list of bookmark items - no folder nodes.");

        var order = rootFolder.Children.OfType<BookmarkItemNodeViewModel>().Select(n => n.Bookmark.BookmarkId).ToArray();
        // a.cs bookmarks come first (both files named "a.cs" group together), then b.cs.
        // Within same file name, sort by LineNumber (default). a1 is line 1, a2 is line 5.
        CollectionAssert.AreEqual(new[] { "a1", "a2", "b1" }, order);
    }

    [TestMethod]
    public void GroupModeAndSortMode_WhenCombined_AppliesSortInsideEachGroup()
    {
        var viewModel = new BookmarkManagerViewModel();
        viewModel.LoadForTests(new[]
        {
            new ManagedBookmark { BookmarkId = "blue-z", DocumentPath = @"C:\repo\z.cs", LineNumber = 10, Color = BookmarkColor.Blue, Label = "Zeta", Group = string.Empty },
            new ManagedBookmark { BookmarkId = "blue-a", DocumentPath = @"C:\repo\a.cs", LineNumber = 20, Color = BookmarkColor.Blue, Label = "Alpha", Group = string.Empty },
            new ManagedBookmark { BookmarkId = "green-m", DocumentPath = @"C:\repo\m.cs", LineNumber = 5, Color = BookmarkColor.Green, Label = "Mike", Group = string.Empty },
        }, new[] { string.Empty });

        viewModel.GroupMode = BookmarkGroupMode.Color;
        viewModel.SortMode = BookmarkSortMode.Alphabetical;

        var rootFolder = (FolderNodeViewModel)viewModel.RootNodes[0];
        var order = rootFolder.Children.OfType<BookmarkItemNodeViewModel>().Select(n => n.Bookmark.BookmarkId).ToArray();
        // Green(4) group first, then Blue(5) group. Alpha < Zeta inside Blue.
        CollectionAssert.AreEqual(new[] { "green-m", "blue-a", "blue-z" }, order);
    }

    [TestMethod]
    public void GroupMode_WhenSwitchingBackToFolders_RestoresFolderHierarchy()
    {
        var viewModel = new BookmarkManagerViewModel();
        viewModel.LoadForTests(new[]
        {
            new ManagedBookmark { BookmarkId = "id-1", DocumentPath = @"C:\repo\a.cs", LineNumber = 1, Color = BookmarkColor.Blue, Group = "FolderA" },
        }, new[] { string.Empty, "FolderA" });

        viewModel.GroupMode = BookmarkGroupMode.Color;
        viewModel.GroupMode = BookmarkGroupMode.Folders;

        var rootFolder = (FolderNodeViewModel)viewModel.RootNodes[0];
        Assert.IsTrue(rootFolder.Children.OfType<FolderNodeViewModel>().Any(n => n.FolderName == "FolderA"));
    }
}
