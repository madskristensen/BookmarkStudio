using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.ComponentModel;

namespace BookmarkStudio.Test;

[TestClass]
public class BookmarkManagerViewModelTests
{
    [TestMethod]
    public void ApplySearchText_WhenMatchingLabelAndColor_FiltersVisibleBookmarks()
    {
        BookmarkManagerViewModel viewModel = new BookmarkManagerViewModel();
        viewModel.Bookmarks.Add(new ManagedBookmark
        {
            BookmarkId = "id-1",
            DocumentPath = @"C:\repo\a.cs",
            LineNumber = 10,
            LineText = "alpha",
            Label = "Important",
            Color = BookmarkColor.Blue,
            SlotNumber = 1,
        });
        viewModel.Bookmarks.Add(new ManagedBookmark
        {
            BookmarkId = "id-2",
            DocumentPath = @"C:\repo\b.cs",
            LineNumber = 20,
            LineText = "beta",
            Label = "Other",
            Color = BookmarkColor.None,
            SlotNumber = 2,
        });

        int labelMatches = viewModel.ApplySearchText("important");
        int colorMatches = viewModel.ApplySearchText("blue");

        Assert.AreEqual(1, labelMatches);
        Assert.AreEqual(1, colorMatches);
    }

    [TestMethod]
    public void SortByColumn_WhenSameColumnCalledTwice_TogglesSortDirection()
    {
        BookmarkManagerViewModel viewModel = new BookmarkManagerViewModel();

        viewModel.SortByColumn(nameof(ManagedBookmark.Label));
        ListSortDirection firstDirection = viewModel.ColumnSortDirection;
        viewModel.SortByColumn(nameof(ManagedBookmark.Label));

        Assert.AreEqual(ListSortDirection.Ascending, firstDirection);
        Assert.AreEqual(ListSortDirection.Descending, viewModel.ColumnSortDirection);
    }

    [TestMethod]
    public void SortByColumn_WhenSwitchingColumn_ResetsToAscending()
    {
        BookmarkManagerViewModel viewModel = new BookmarkManagerViewModel();

        viewModel.SortByColumn(nameof(ManagedBookmark.Label));
        viewModel.SortByColumn(nameof(ManagedBookmark.Label));
        viewModel.SortByColumn(nameof(ManagedBookmark.LineNumber));

        Assert.AreEqual(nameof(ManagedBookmark.LineNumber), viewModel.ColumnSortProperty);
        Assert.AreEqual(ListSortDirection.Ascending, viewModel.ColumnSortDirection);
    }

    [TestMethod]
    public void SelectBookmark_WhenBookmarkIdMissing_ClearsSelection()
    {
        BookmarkManagerViewModel viewModel = new BookmarkManagerViewModel();
        viewModel.Bookmarks.Add(new ManagedBookmark { BookmarkId = "id-1", DocumentPath = @"C:\repo\a.cs", LineNumber = 1 });
        viewModel.SelectedBookmark = viewModel.Bookmarks[0];

        viewModel.SelectBookmark("missing");

        Assert.IsNull(viewModel.SelectedBookmark);
    }

    [TestMethod]
    public void Clear_WhenCalled_ResetsState()
    {
        BookmarkManagerViewModel viewModel = new BookmarkManagerViewModel();
        viewModel.Bookmarks.Add(new ManagedBookmark { BookmarkId = "id-1", DocumentPath = @"C:\repo\a.cs", LineNumber = 1 });
        viewModel.SelectedBookmark = viewModel.Bookmarks[0];
        viewModel.ApplySearchText("a.cs");
        viewModel.SortByColumn(nameof(ManagedBookmark.Label));

        viewModel.Clear();

        Assert.AreEqual(0, viewModel.Bookmarks.Count);
        Assert.IsNull(viewModel.SelectedBookmark);
        Assert.AreEqual(string.Empty, viewModel.SearchText);
        Assert.IsNull(viewModel.ColumnSortProperty);
        Assert.AreEqual(ListSortDirection.Ascending, viewModel.ColumnSortDirection);
        Assert.AreEqual("No solution loaded.", viewModel.StatusText);
    }
}
