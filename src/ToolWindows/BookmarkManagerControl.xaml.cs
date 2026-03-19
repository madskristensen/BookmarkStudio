using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace BookmarkStudio
{
    public partial class BookmarkManagerControl : UserControl
    {
        private readonly BookmarkManagerViewModel _viewModel = new BookmarkManagerViewModel();

        public BookmarkManagerControl()
        {
            InitializeComponent();
            DataContext = _viewModel;
            Loaded += BookmarkManagerControl_Loaded;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        internal BookmarkManagerViewModel ViewModel => _viewModel;

        internal string? SelectedBookmarkId => _viewModel.SelectedBookmark?.BookmarkId;

        internal Task RefreshAsync(CancellationToken cancellationToken)
            => _viewModel.RefreshAsync(cancellationToken);

        internal void SelectBookmark(string? bookmarkId)
            => _viewModel.SelectBookmark(bookmarkId);

        private async void BookmarkManagerControl_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= BookmarkManagerControl_Loaded;
            await RunAsync(cancellationToken => _viewModel.InitializeAsync(cancellationToken));
        }

        private async void BookmarkDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => await RunAsync(cancellationToken => _viewModel.NavigateSelectedAsync(cancellationToken));

        private async void NavigateMenuItem_Click(object sender, RoutedEventArgs e)
            => await RunAsync(cancellationToken => _viewModel.NavigateSelectedAsync(cancellationToken));

        private async void CopyLocationMenuItem_Click(object sender, RoutedEventArgs e)
            => await CopyLocationAsync();

        private async void EditLabelMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ManagedBookmark bookmark = _viewModel.SelectedBookmark;
            if (bookmark is null)
            {
                return;
            }

            string newLabel = TextPromptWindow.Show("Edit Label", "Enter a label for the bookmark:", bookmark.Label);
            if (newLabel is null)
            {
                return;
            }

            await RunAsync(async cancellationToken =>
            {
                await BookmarkOperationsService.Current.RenameLabelAsync(bookmark.BookmarkId, newLabel, cancellationToken);
                await _viewModel.RefreshAsync(cancellationToken);
                _viewModel.SelectBookmark(bookmark.BookmarkId);
            });
        }

        private async void EditGroupMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ManagedBookmark bookmark = _viewModel.SelectedBookmark;
            if (bookmark is null)
            {
                return;
            }

            string newGroup = TextPromptWindow.Show("Edit Group", "Enter a group for the bookmark:", bookmark.Group);
            if (newGroup is null)
            {
                return;
            }

            await RunAsync(async cancellationToken =>
            {
                await BookmarkOperationsService.Current.SetGroupAsync(bookmark.BookmarkId, newGroup, cancellationToken);
                await _viewModel.RefreshAsync(cancellationToken);
                _viewModel.SelectBookmark(bookmark.BookmarkId);
            });
        }

        private async void AssignSlotMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menuItem || menuItem.Tag is null || !int.TryParse(menuItem.Tag.ToString(), out int slotNumber))
            {
                return;
            }

            await RunAsync(cancellationToken => _viewModel.AssignSelectedSlotAsync(slotNumber, cancellationToken));
        }

        private async void ClearSlotMenuItem_Click(object sender, RoutedEventArgs e)
            => await RunAsync(cancellationToken => _viewModel.ClearSelectedSlotAsync(cancellationToken));

        private async void ClearGroupMenuItem_Click(object sender, RoutedEventArgs e)
            => await RunAsync(cancellationToken => _viewModel.ClearSelectedGroupAsync(cancellationToken));

        private async void DeleteBookmarkMenuItem_Click(object sender, RoutedEventArgs e)
            => await RunAsync(cancellationToken => _viewModel.DeleteSelectedAsync(cancellationToken));

        private void BookmarkDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;

            string propertyName = e.Column.SortMemberPath;
            if (string.IsNullOrEmpty(propertyName))
            {
                return;
            }

            _viewModel.SortByColumn(propertyName);

            e.Column.SortDirection = _viewModel.ColumnSortDirection;

            foreach (DataGridColumn column in BookmarkDataGrid.Columns)
            {
                if (column != e.Column)
                {
                    column.SortDirection = null;
                }
            }
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(BookmarkManagerViewModel.ColumnSortProperty), StringComparison.Ordinal)
                && _viewModel.ColumnSortProperty is null)
            {
                ClearDataGridSortDirection();
            }
        }

        private void ClearDataGridSortDirection()
        {
            foreach (DataGridColumn column in BookmarkDataGrid.Columns)
            {
                column.SortDirection = null;
            }
        }

        private async Task RunAsync(Func<CancellationToken, Task> action)
        {
            try
            {
                await action(CancellationToken.None);
            }
            catch (OperationCanceledException ex)
            {
                await ex.LogAsync();
                _viewModel.SetStatus("The operation was canceled.");
            }
            catch (ArgumentException ex)
            {
                await ex.LogAsync();
                _viewModel.SetStatus(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                await ex.LogAsync();
                _viewModel.SetStatus(ex.Message);
            }
        }

        private async Task CopyLocationAsync()
        {
            try
            {
                string location = await _viewModel.GetSelectedLocationAsync(CancellationToken.None);
                Clipboard.SetText(location);
                _viewModel.SetStatus("Bookmark location copied to the clipboard.");
            }
            catch (OperationCanceledException ex)
            {
                await ex.LogAsync();
                _viewModel.SetStatus("The operation was canceled.");
            }
            catch (ArgumentException ex)
            {
                await ex.LogAsync();
                _viewModel.SetStatus(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                await ex.LogAsync();
                _viewModel.SetStatus(ex.Message);
            }
        }

            }
        }
