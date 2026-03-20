using Microsoft.VisualStudio.TestTools.UnitTesting;

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
}
