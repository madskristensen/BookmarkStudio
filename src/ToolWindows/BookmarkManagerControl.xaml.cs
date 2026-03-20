using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
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
            Loaded += BookmarkManagerControl_Loaded;
            PreviewKeyDown += BookmarkManagerControl_PreviewKeyDown;
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

        internal async Task RenameSelectedAsync()
        {
            if (_viewModel.SelectedNode is FolderNodeViewModel)
            {
                await PromptRenameFolderInternalAsync();
            }
            else if (_viewModel.SelectedNode is BookmarkItemNodeViewModel)
            {
                await PromptRenameBookmarkInternalAsync();
            }
        }

        private async void BookmarkManagerControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F2 && _viewModel.SelectedNode is not null)
            {
                e.Handled = true;
                await RenameSelectedAsync();
            }
        }

        private async void BookmarkManagerControl_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= BookmarkManagerControl_Loaded;
            await RunAsync(cancellationToken => _viewModel.InitializeAsync(cancellationToken));
        }

        private void BookmarkTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is BookmarkNodeViewModel node)
            {
                _viewModel.SelectedNode = node;
                return;
            }

            _viewModel.SelectedNode = null;
        }

        private async void BookmarkTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            BookmarkNodeViewModel? node = GetNodeFromVisual(e.OriginalSource as DependencyObject);
            if (node is not BookmarkItemNodeViewModel bookmarkNode)
            {
                return;
            }

            _viewModel.SelectedNode = bookmarkNode;
            e.Handled = true;
            await RunAsync(cancellationToken => _viewModel.NavigateSelectedAsync(cancellationToken));
        }

        private void BookmarkTreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(BookmarkTreeView);
            _dragBookmarkId = (GetNodeFromVisual(e.OriginalSource as DependencyObject) as BookmarkItemNodeViewModel)?.Bookmark.BookmarkId;
        }

        private void BookmarkTreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            BookmarkNodeViewModel? node = GetNodeFromVisual(e.OriginalSource as DependencyObject);
            if (node is null)
            {
                return;
            }

            _viewModel.SelectedNode = node;
        }

        private void BookmarkTreeView_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed
                || string.IsNullOrWhiteSpace(_dragBookmarkId))
            {
                return;
            }

            Point currentPosition = e.GetPosition(BookmarkTreeView);
            if (Math.Abs(currentPosition.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(currentPosition.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            DataObject data = new DataObject();
            data.SetData(BookmarkDragFormat, _dragBookmarkId, false);
            DragDrop.DoDragDrop(BookmarkTreeView, data, DragDropEffects.Move);
            _dragBookmarkId = null;
        }

        private void BookmarkTreeView_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(BookmarkDragFormat, false)
                ? DragDropEffects.Move
                : DragDropEffects.None;
            e.Handled = true;
        }

        private async void BookmarkTreeView_Drop(object sender, DragEventArgs e)
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
        {
            SelectNodeFromContextMenu(sender);
            await RunAsync(cancellationToken => _viewModel.NavigateSelectedAsync(cancellationToken));
        }

        private async void CopyLocationMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SelectNodeFromContextMenu(sender);
            await CopyLocationAsync();
        }

        private async void EditLabelMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SelectNodeFromContextMenu(sender);

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
            SelectNodeFromContextMenu(sender);

            if (sender is not MenuItem menuItem || menuItem.Tag is null || !int.TryParse(menuItem.Tag.ToString(), out int slotNumber))
            {
                return;
            }

            await RunAsync(cancellationToken => _viewModel.AssignSelectedSlotAsync(slotNumber, cancellationToken));
        }

        private async void ClearSlotMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SelectNodeFromContextMenu(sender);
            await RunAsync(cancellationToken => _viewModel.ClearSelectedSlotAsync(cancellationToken));
        }

        private async void SetColorMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SelectNodeFromContextMenu(sender);

            if (sender is not MenuItem menuItem || menuItem.Tag is null || !int.TryParse(menuItem.Tag.ToString(), out int colorValue))
            {
                return;
            }

            BookmarkColor color = (BookmarkColor)colorValue;
            await RunAsync(cancellationToken => _viewModel.SetSelectedColorAsync(color, cancellationToken));
        }

        private async void DeleteBookmarkMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SelectNodeFromContextMenu(sender);
            await RunAsync(cancellationToken => _viewModel.DeleteSelectedAsync(cancellationToken));
        }

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

            if (_viewModel.SelectedNode is FolderNodeViewModel folderNode
                && string.IsNullOrWhiteSpace(folderNode.FolderPath))
            {
                _viewModel.SetStatus("The Root folder cannot be deleted.");
                return;
            }

            await RunAsync(cancellationToken => _viewModel.DeleteSelectedAsync(cancellationToken));
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(BookmarkManagerViewModel.SelectedNode), StringComparison.Ordinal))
            {
                SelectTreeNode(_viewModel.SelectedNode);
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

            if (string.IsNullOrWhiteSpace(folderNode.FolderPath))
            {
                _viewModel.SetStatus("The Root folder cannot be renamed.");
                return;
            }

            string? folderName = TextPromptWindow.Show("Rename Folder", "Enter a new folder name:", folderNode.FolderName, selectTextOnLoad: true);
            if (folderName is null)
            {
                return;
            }

            await RunAsync(cancellationToken => _viewModel.RenameSelectedFolderAsync(folderName, cancellationToken));
        }

        private async Task PromptRenameBookmarkInternalAsync()
        {
            ManagedBookmark? bookmark = _viewModel.SelectedBookmark;
            if (bookmark is null)
            {
                _viewModel.SetStatus("Select a bookmark first.");
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
            catch (Exception ex)
            {
                await ex.LogAsync();
                _viewModel.SetStatus("An unexpected error occurred.");
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
            catch (Exception ex)
            {
                await ex.LogAsync();
                _viewModel.SetStatus("An unexpected error occurred.");
            }
        }

        private void SelectTreeNode(BookmarkNodeViewModel? node)
        {
            if (node is null)
            {
                return;
            }

            TreeViewItem? treeViewItem = FindTreeViewItem(BookmarkTreeView, node);
            if (treeViewItem is null || treeViewItem.IsSelected)
            {
                return;
            }

            treeViewItem.IsSelected = true;
            treeViewItem.BringIntoView();
        }

        private bool SelectFolderFromContextMenu(object sender)
        {
            if (sender is not MenuItem menuItem
                || GetOwningContextMenu(menuItem) is not ContextMenu contextMenu
                || contextMenu.PlacementTarget is not FrameworkElement placementTarget
                || placementTarget.DataContext is not FolderNodeViewModel folderNode)
            {
                return false;
            }

            _viewModel.SelectedNode = folderNode;
            return true;
        }

        private void SelectNodeFromContextMenu(object sender)
        {
            if (sender is not MenuItem menuItem
                || GetOwningContextMenu(menuItem) is not ContextMenu contextMenu
                || contextMenu.PlacementTarget is not FrameworkElement placementTarget
                || placementTarget.DataContext is not BookmarkNodeViewModel node)
            {
                return;
            }

            _viewModel.SelectedNode = node;
        }

        private static ContextMenu? GetOwningContextMenu(DependencyObject? source)
        {
            DependencyObject? current = source;
            while (current is not null)
            {
                if (current is ContextMenu contextMenu)
                {
                    return contextMenu;
                }

                current = LogicalTreeHelper.GetParent(current);
            }

            return null;
        }

        private static TreeViewItem? FindTreeViewItem(ItemsControl parent, object item)
        {
            if (parent.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem directMatch)
            {
                return directMatch;
            }

            foreach (object child in parent.Items)
            {
                if (parent.ItemContainerGenerator.ContainerFromItem(child) is not TreeViewItem childContainer)
                {
                    continue;
                }

                bool wasExpanded = childContainer.IsExpanded;
                childContainer.IsExpanded = true;
                childContainer.UpdateLayout();

                TreeViewItem? match = FindTreeViewItem(childContainer, item);
                if (match is not null)
                {
                    return match;
                }

                childContainer.IsExpanded = wasExpanded;
            }

            return null;
        }

        private static BookmarkNodeViewModel? GetNodeFromVisual(DependencyObject? source)
        {
            DependencyObject? current = source;
            while (current is not null)
            {
                if (current is TreeViewItem item && item.DataContext is BookmarkNodeViewModel node)
                {
                    return node;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private static string GetDropTargetFolderPath(DependencyObject? source)
        {
            BookmarkNodeViewModel? targetNode = GetNodeFromVisual(source);
            if (targetNode is FolderNodeViewModel folderNode)
            {
                return folderNode.FolderPath;
            }

            return (targetNode as BookmarkItemNodeViewModel)?.Bookmark.FolderPath ?? string.Empty;
        }
    }
}
