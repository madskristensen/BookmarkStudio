using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace BookmarkStudio.Test;

[TestClass]
public class BookmarkManagerViewModelTests
{
    [TestMethod]
    public void ApplySearchText_WhenMatchingLabelAndColor_FiltersVisibleBookmarks()
    {
        BookmarkManagerViewModel viewModel = new BookmarkManagerViewModel();
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
                SlotNumber = 1,
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
                SlotNumber = 2,
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
        BookmarkManagerViewModel viewModel = new BookmarkManagerViewModel();
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
        BookmarkManagerViewModel viewModel = new BookmarkManagerViewModel();
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
        BookmarkManagerViewModel viewModel = new BookmarkManagerViewModel();
        viewModel.LoadForTests(Array.Empty<ManagedBookmark>(), new[] { string.Empty, "FolderA", "FolderA/Sub" });

        viewModel.SelectFolder("FolderA/Sub");

        Assert.IsNull(viewModel.SelectedBookmark);
        Assert.AreEqual("FolderA/Sub", viewModel.SelectedFolderPath);
        Assert.IsTrue(viewModel.HasSelectedFolder);
    }

    [TestMethod]
    public void Clear_WhenCalled_ResetsState()
    {
        BookmarkManagerViewModel viewModel = new BookmarkManagerViewModel();
        viewModel.LoadForTests(new[]
        {
            new ManagedBookmark { BookmarkId = "id-1", DocumentPath = @"C:\repo\a.cs", LineNumber = 1, Group = "FolderA" },
        }, new[] { string.Empty, "FolderA" });
        viewModel.SelectBookmark("id-1");

        viewModel.Clear();

        Assert.AreEqual(0, viewModel.RootNodes.Count);
        Assert.IsNull(viewModel.SelectedBookmark);
        Assert.AreEqual(string.Empty, viewModel.SearchText);
        Assert.AreEqual("No solution loaded.", viewModel.StatusText);
    }

    [TestMethod]
    public void LoadForTests_WhenBookmarksAndFoldersUnordered_BuildsDeterministicTreeOrdering()
    {
        BookmarkManagerViewModel viewModel = new BookmarkManagerViewModel();
        viewModel.LoadForTests(new[]
        {
            new ManagedBookmark { BookmarkId = "root-2", DocumentPath = @"C:\repo\z.cs", LineNumber = 20, Group = string.Empty },
            new ManagedBookmark { BookmarkId = "root-1", DocumentPath = @"C:\repo\a.cs", LineNumber = 5, Group = string.Empty },
            new ManagedBookmark { BookmarkId = "a-2", DocumentPath = @"C:\repo\a2.cs", LineNumber = 12, Group = "Team/Backlog" },
            new ManagedBookmark { BookmarkId = "a-1", DocumentPath = @"C:\repo\a1.cs", LineNumber = 4, Group = "Team/Backlog" },
        }, new[] { string.Empty, "Zeta", "Team", "Team/Backlog" });

        Assert.AreEqual(1, viewModel.RootNodes.Count);
        Assert.AreEqual("Root", viewModel.RootNodes[0].DisplayText);

        FolderNodeViewModel rootFolder = (FolderNodeViewModel)viewModel.RootNodes[0];
        Assert.AreEqual("Team", rootFolder.Children[0].DisplayText);
        Assert.AreEqual("Zeta", rootFolder.Children[1].DisplayText);
        Assert.AreEqual("a.cs", rootFolder.Children[2].DisplayText);
        Assert.AreEqual("z.cs", rootFolder.Children[3].DisplayText);

        FolderNodeViewModel teamFolder = (FolderNodeViewModel)rootFolder.Children[0];
        FolderNodeViewModel backlogFolder = (FolderNodeViewModel)teamFolder.Children[0];

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
        BookmarkManagerViewModel viewModel = new BookmarkManagerViewModel();
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
        BookmarkManagerViewModel viewModel = new BookmarkManagerViewModel();
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
        BookmarkManagerViewModel viewModel = new BookmarkManagerViewModel();
        viewModel.LoadForTests(Array.Empty<ManagedBookmark>(), new[] { string.Empty, "FolderA", "FolderA/Sub" });

        Assert.AreEqual(2, viewModel.BookmarkRows.Count);
        Assert.IsTrue(viewModel.BookmarkRows.All(row => row.IsFolderPlaceholder));
        CollectionAssert.AreEquivalent(
            new[] { "FolderA", "FolderA/Sub" },
            viewModel.BookmarkRows.Select(row => row.FolderPath).ToArray());
    }

    [TestMethod]
    public void LoadForTests_WhenFolderContainsBookmarks_DoesNotCreatePlaceholderForThatFolder()
    {
        BookmarkManagerViewModel viewModel = new BookmarkManagerViewModel();
        viewModel.LoadForTests(new[]
        {
            new ManagedBookmark { BookmarkId = "id-1", DocumentPath = @"C:\repo\a.cs", LineNumber = 1, Group = "FolderA" },
        }, new[] { string.Empty, "FolderA", "FolderB" });

        BookmarkGridRowViewModel folderA = viewModel.BookmarkRows.Single(row => string.Equals(row.FolderPath, "FolderA", StringComparison.Ordinal));
        BookmarkGridRowViewModel folderB = viewModel.BookmarkRows.Single(row => string.Equals(row.FolderPath, "FolderB", StringComparison.Ordinal));

        Assert.IsFalse(folderA.IsFolderPlaceholder);
        Assert.IsTrue(folderB.IsFolderPlaceholder);
    }
}
