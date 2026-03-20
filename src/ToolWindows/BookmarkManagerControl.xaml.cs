using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace BookmarkStudio
{
    public partial class BookmarkManagerControl : UserControl
    {
        private static readonly string BookmarkDragFormat = DataFormats.UnicodeText;
        private readonly BookmarkManagerViewModel _viewModel = new BookmarkManagerViewModel();
        private Point _dragStartPoint;
        private string? _dragBookmarkId;

        public BookmarkManagerControl()
        {
            InitializeComponent();
            DataContext = _viewModel;
            ConfigureGrouping();
            Loaded += BookmarkManagerControl_Loaded;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        internal BookmarkManagerViewModel ViewModel => _viewModel;

        internal string? SelectedBookmarkId => _viewModel.SelectedBookmark?.BookmarkId;

        internal string? SelectedFolderPath => _viewModel.SelectedNode is FolderNodeViewModel folderNode
            ? folderNode.FolderPath
            : null;

        internal Task RefreshAsync(CancellationToken cancellationToken)
            => _viewModel.RefreshAsync(cancellationToken);

        internal void SelectBookmark(string? bookmarkId)
            => _viewModel.SelectBookmark(bookmarkId);

        internal Task PromptCreateFolderAsync()
            => PromptCreateFolderInternalAsync();

        internal Task DeleteSelectedAsync(CancellationToken cancellationToken)
            => _viewModel.DeleteSelectedAsync(cancellationToken);

        private async void BookmarkManagerControl_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= BookmarkManagerControl_Loaded;
            await RunAsync(cancellationToken => _viewModel.InitializeAsync(cancellationToken));
        }

        private void BookmarkDataGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (BookmarkDataGrid.SelectedItem is BookmarkGridRowViewModel row)
            {
                _viewModel.SelectBookmark(row.BookmarkId);
                return;
            }

            _viewModel.SelectedNode = null;
        }

        private async void BookmarkDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (BookmarkDataGrid.SelectedItem is not BookmarkGridRowViewModel row
                || row.IsFolderPlaceholder
                || string.IsNullOrWhiteSpace(row.BookmarkId))
            {
                return;
            }

            _viewModel.SelectBookmark(row.BookmarkId);
            e.Handled = true;
            await RunAsync(cancellationToken => _viewModel.NavigateSelectedAsync(cancellationToken));
        }

        private void BookmarkDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(BookmarkDataGrid);
            _dragBookmarkId = GetRowFromVisual(e.OriginalSource as DependencyObject)?.BookmarkId;
        }

        private void BookmarkDataGrid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed
                || string.IsNullOrWhiteSpace(_dragBookmarkId))
            {
                return;
            }

            Point currentPosition = e.GetPosition(BookmarkDataGrid);
            if (Math.Abs(currentPosition.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(currentPosition.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            DataObject data = new DataObject();
            data.SetData(BookmarkDragFormat, _dragBookmarkId, false);
            DragDrop.DoDragDrop(BookmarkDataGrid, data, DragDropEffects.Move);
            _dragBookmarkId = null;
        }

        private void BookmarkDataGrid_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(BookmarkDragFormat, false)
                ? DragDropEffects.Move
                : DragDropEffects.None;
            e.Handled = true;
        }

        private async void BookmarkDataGrid_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(BookmarkDragFormat, false))
            {
                e.Handled = true;
                return;
            }

            string? bookmarkId = e.Data.GetData(BookmarkDragFormat, false) as string;
            if (string.IsNullOrWhiteSpace(bookmarkId))
            {
                e.Handled = true;
                return;
            }

            _viewModel.SelectBookmark(bookmarkId);
            ManagedBookmark? selectedBookmark = _viewModel.SelectedBookmark;
            if (selectedBookmark is null)
            {
                e.Handled = true;
                return;
            }

            string targetFolderPath = GetDropTargetFolderPath(e.OriginalSource as DependencyObject);
            if (string.Equals(selectedBookmark.FolderPath, targetFolderPath, StringComparison.OrdinalIgnoreCase))
            {
                e.Handled = true;
                return;
            }

            _viewModel.MoveSelectedBookmarkToFolderAsync(targetFolderPath, CancellationToken.None).FireAndForget();
            e.Handled = true;
        }

        private async void NavigateMenuItem_Click(object sender, RoutedEventArgs e)
            => await RunAsync(cancellationToken => _viewModel.NavigateSelectedAsync(cancellationToken));

        private async void CopyLocationMenuItem_Click(object sender, RoutedEventArgs e)
            => await CopyLocationAsync();

        private async void EditLabelMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ManagedBookmark? bookmark = _viewModel.SelectedBookmark;
            if (bookmark is null)
            {
                return;
            }

            string? newLabel = TextPromptWindow.Show("Edit Label", "Enter a label for the bookmark:", bookmark.Label, selectTextOnLoad: true);
            if (newLabel is null)
            {
                return;
            }

            _viewModel.SelectedLabelText = newLabel;
            await RunAsync(cancellationToken => _viewModel.SaveSelectionAsync(cancellationToken));
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

        private async void SetColorMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menuItem || menuItem.Tag is null || !int.TryParse(menuItem.Tag.ToString(), out int colorValue))
            {
                return;
            }

            BookmarkColor color = (BookmarkColor)colorValue;
            await RunAsync(cancellationToken => _viewModel.SetSelectedColorAsync(color, cancellationToken));
        }

        private async void DeleteBookmarkMenuItem_Click(object sender, RoutedEventArgs e)
            => await RunAsync(cancellationToken => _viewModel.DeleteSelectedAsync(cancellationToken));

        private void FolderHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            SelectFolderFromHeader(sender);
            e.Handled = true;
        }

        private void FolderHeader_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
            => SelectFolderFromHeader(sender);

        private async void RenameFolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!SelectFolderFromContextMenu(sender))
            {
                return;
            }

            await PromptRenameFolderInternalAsync();
        }

        private async void DeleteFolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!SelectFolderFromContextMenu(sender))
            {
                return;
            }

            await RunAsync(cancellationToken => _viewModel.DeleteSelectedAsync(cancellationToken));
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(BookmarkManagerViewModel.SelectedNode), StringComparison.Ordinal)
                || string.Equals(e.PropertyName, nameof(BookmarkManagerViewModel.SelectedBookmark), StringComparison.Ordinal))
            {
                SelectRowByBookmarkId(_viewModel.SelectedBookmark?.BookmarkId);
            }
        }

        private async Task PromptCreateFolderInternalAsync()
        {
            string? folderName = TextPromptWindow.Show("Add Folder", "Enter a folder name:", string.Empty);
            if (folderName is null)
            {
                return;
            }

            await RunAsync(cancellationToken => _viewModel.CreateFolderAsync(folderName, cancellationToken));
        }

        private async Task PromptRenameFolderInternalAsync()
        {
            if (_viewModel.SelectedNode is not FolderNodeViewModel folderNode)
            {
                _viewModel.SetStatus("Select a folder first.");
                return;
            }

            string? folderName = TextPromptWindow.Show("Rename Folder", "Enter a new folder name:", folderNode.FolderName, selectTextOnLoad: true);
            if (folderName is null)
            {
                return;
            }

            await RunAsync(cancellationToken => _viewModel.RenameSelectedFolderAsync(folderName, cancellationToken));
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

        private void SelectRowByBookmarkId(string? bookmarkId)
        {
            if (string.IsNullOrWhiteSpace(bookmarkId))
            {
                if (BookmarkDataGrid.SelectedItem is not null)
                {
                    BookmarkDataGrid.SelectedItem = null;
                }

                return;
            }

            BookmarkGridRowViewModel? row = _viewModel.BookmarkRows.FirstOrDefault(item => string.Equals(item.BookmarkId, bookmarkId, StringComparison.Ordinal));
            if (ReferenceEquals(BookmarkDataGrid.SelectedItem, row))
            {
                return;
            }

            BookmarkDataGrid.SelectedItem = row;
            if (row is not null)
            {
                BookmarkDataGrid.ScrollIntoView(row);
            }
        }

        private void ConfigureGrouping()
        {
            if (Resources["BookmarksGroupedView"] is not CollectionViewSource collectionViewSource)
            {
                return;
            }

            collectionViewSource.GroupDescriptions.Clear();
            collectionViewSource.GroupDescriptions.Add(new PropertyGroupDescription(nameof(BookmarkGridRowViewModel.FolderDisplayName)));
        }

        private void SelectFolderFromHeader(object sender)
        {
            if (sender is not FrameworkElement element || element.DataContext is not CollectionViewGroup group)
            {
                return;
            }

            _viewModel.SelectFolder(GetFolderPathFromGroupName(group.Name as string));
        }

        private bool SelectFolderFromContextMenu(object sender)
        {
            if (sender is not MenuItem menuItem
                || menuItem.Parent is not ContextMenu contextMenu
                || contextMenu.PlacementTarget is not FrameworkElement placementTarget
                || placementTarget.DataContext is not CollectionViewGroup group)
            {
                return false;
            }

            _viewModel.SelectFolder(GetFolderPathFromGroupName(group.Name as string));
            return _viewModel.SelectedNode is FolderNodeViewModel;
        }

        private static string GetFolderPathFromGroupName(string? groupName)
            => string.Equals(groupName, "Root", StringComparison.Ordinal)
                ? string.Empty
                : BookmarkIdentity.NormalizeFolderPath(groupName);

        private static BookmarkGridRowViewModel? GetRowFromVisual(DependencyObject? source)
        {
            DependencyObject? current = source;
            while (current is not null)
            {
                if (current is DataGridRow row && row.Item is BookmarkGridRowViewModel bookmarkRow)
                {
                    return bookmarkRow;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private static string GetDropTargetFolderPath(DependencyObject? source)
        {
            DependencyObject? current = source;
            while (current is not null)
            {
                if (current is DataGridRow row && row.Item is BookmarkGridRowViewModel bookmarkRow)
                {
                    return bookmarkRow.FolderPath;
                }

                if (current is GroupItem groupItem && groupItem.DataContext is CollectionViewGroup group)
                {
                    return GetFolderPathFromGroupName(group.Name as string);
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return string.Empty;
        }
    }
}
