using System.ComponentModel;
using System.Runtime.InteropServices;
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

        public BookmarkManagerControl()
        {
            InitializeComponent();
            DataContext = _viewModel;
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

        private void BookmarkTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
            => _viewModel.SelectedNode = e.NewValue as BookmarkNodeViewModel;

        private void BookmarkTreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
            => _dragStartPoint = e.GetPosition(BookmarkTreeView);

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

        private async void AddFolderMenuItem_Click(object sender, RoutedEventArgs e)
            => await PromptCreateFolderInternalAsync();

        private async void RenameFolderMenuItem_Click(object sender, RoutedEventArgs e)
            => await PromptRenameFolderInternalAsync();

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

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(BookmarkManagerViewModel.SelectedNode), StringComparison.Ordinal)
                && BookmarkTreeView.SelectedItem != _viewModel.SelectedNode)
            {
                SelectNodeInTree(BookmarkTreeView, _viewModel.SelectedNode);
            }
        }

        private void BookmarkTreeView_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            if ((_dragStartPoint - e.GetPosition(BookmarkTreeView)).Length <= SystemParameters.MinimumHorizontalDragDistance)
            {
                return;
            }

            if (_viewModel.SelectedNode is not BookmarkItemNodeViewModel bookmarkNode)
            {
                return;
            }

            DataObject data = new DataObject();
            data.SetData(BookmarkDragFormat, bookmarkNode.Bookmark.BookmarkId, false);

            try
            {
                DragDrop.DoDragDrop(BookmarkTreeView, data, DragDropEffects.Move);
            }
            catch (COMException ex)
            {
                ex.Log();
                _viewModel.SetStatus("Drag and drop completed with a shell warning.");
            }
        }

        private void BookmarkTreeView_DragOver(object sender, DragEventArgs e)
        {
            try
            {
                e.Effects = e.Data.GetDataPresent(BookmarkDragFormat, false)
                    ? DragDropEffects.Move
                    : DragDropEffects.None;
            }
            catch (COMException ex)
            {
                ex.Log();
                e.Effects = DragDropEffects.None;
            }

            e.Handled = true;
        }

        private async void BookmarkTreeView_Drop(object sender, DragEventArgs e)
        {
            string? bookmarkId = null;

            try
            {
                if (!e.Data.GetDataPresent(BookmarkDragFormat, false))
                {
                    e.Handled = true;
                    return;
                }

                bookmarkId = e.Data.GetData(BookmarkDragFormat, false) as string;
            }
            catch (COMException ex)
            {
                ex.Log();
                _viewModel.SetStatus("Drag/drop failed due to incompatible data format.");
                e.Handled = true;
                return;
            }

            if (string.IsNullOrWhiteSpace(bookmarkId))
            {
                e.Handled = true;
                return;
            }

            BookmarkNodeViewModel? targetNode = GetNodeAtPosition(e.GetPosition(BookmarkTreeView));
            string targetFolderPath = targetNode switch
            {
                FolderNodeViewModel folderNode => folderNode.FolderPath,
                BookmarkItemNodeViewModel bookmarkNode => bookmarkNode.Bookmark.FolderPath,
                _ => string.Empty,
            };

            ManagedBookmark? selectedBookmark = _viewModel.SelectedBookmark;
            if (selectedBookmark is null
                || !string.Equals(selectedBookmark.BookmarkId, bookmarkId, StringComparison.Ordinal))
            {
                _viewModel.SelectBookmark(bookmarkId);
                selectedBookmark = _viewModel.SelectedBookmark;
            }

            if (selectedBookmark is null
                || string.Equals(selectedBookmark.FolderPath, targetFolderPath, StringComparison.OrdinalIgnoreCase))
            {
                e.Handled = true;
                return;
            }

            _viewModel.MoveSelectedBookmarkToFolderAsync(targetFolderPath, CancellationToken.None).FireAndForget();
            e.Handled = true;
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

        private BookmarkNodeViewModel? GetNodeAtPosition(Point position)
        {
            if (BookmarkTreeView.InputHitTest(position) is not DependencyObject current)
            {
                return null;
            }

            while (current is not null)
            {
                if (current is TreeViewItem item)
                {
                    return item.DataContext as BookmarkNodeViewModel;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private static bool SelectNodeInTree(ItemsControl parent, object? targetNode)
        {
            if (targetNode is null)
            {
                return false;
            }

            for (int i = 0; i < parent.Items.Count; i++)
            {
                if (parent.ItemContainerGenerator.ContainerFromIndex(i) is not TreeViewItem item)
                {
                    continue;
                }

                if (ReferenceEquals(item.DataContext, targetNode))
                {
                    item.IsSelected = true;
                    item.BringIntoView();
                    return true;
                }

                if (item.DataContext is FolderNodeViewModel)
                {
                    item.IsExpanded = true;
                }

                if (SelectNodeInTree(item, targetNode))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
