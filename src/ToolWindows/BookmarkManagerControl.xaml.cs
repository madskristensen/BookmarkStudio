using System.Collections.Generic;
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
        private static readonly string BookmarkDragFormat = "BookmarkStudio.Bookmark";
        private static readonly string FolderDragFormat = "BookmarkStudio.Folder";
        private readonly BookmarkManagerViewModel _viewModel = new();
        private readonly ToolWindowInitData? _initData;
        private Point _dragStartPoint;
        private string? _dragBookmarkId;
        private string? _dragFolderPath;
        private BookmarkStorageLocation? _dragSourceStorage;

        public BookmarkManagerControl()
            : this(null)
        {
        }

        internal BookmarkManagerControl(ToolWindowInitData? initData)
        {
            _initData = initData;
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

        internal Task RefreshWithoutCleanupAsync(CancellationToken cancellationToken)
            => _viewModel.RefreshWithoutCleanupAsync(cancellationToken);

        internal void RefreshFromCache()
            => _viewModel.RefreshFromCache();

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
            if (_viewModel.SelectedNode is null)
            {
                return;
            }

            switch (e.Key)
            {
                case Key.F2:
                    e.Handled = true;
                    await RenameSelectedAsync();
                    break;

                case Key.Delete:
                    e.Handled = true;

                    // Check if it's a root node - require confirmation for clearing all bookmarks
                    if (_viewModel.SelectedNode is FolderNodeViewModel folderNode && folderNode.IsRoot)
                    {
                        var storageName = folderNode.StorageLocation switch
                        {
                            BookmarkStorageLocation.Personal => "User",
                            BookmarkStorageLocation.Workspace => "Workspace",
                            _ => "this storage"
                        };

                        System.Windows.MessageBoxResult result = System.Windows.MessageBox.Show(
                            $"Are you sure you want to clear all bookmarks from {storageName}? This action cannot be undone.",
                            "Clear Bookmarks",
                            System.Windows.MessageBoxButton.YesNo,
                            System.Windows.MessageBoxImage.Warning,
                            System.Windows.MessageBoxResult.No);

                        if (result != System.Windows.MessageBoxResult.Yes)
                        {
                            return;
                        }

                        await RunAsync(cancellationToken => _viewModel.ClearBookmarksByStorageAsync(cancellationToken));
                    }
                    else
                    {
                        // Normal delete for non-root nodes
                        await RunAsync(cancellationToken => _viewModel.DeleteSelectedAsync(cancellationToken));
                    }
                    break;

                case Key.Enter:
                    if (_viewModel.SelectedNode is BookmarkItemNodeViewModel)
                    {
                        e.Handled = true;
                        await RunAsync(cancellationToken => _viewModel.NavigateSelectedAsync(cancellationToken));
                    }
                    break;

                case Key.C:
                    if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control
                        && _viewModel.SelectedNode is BookmarkItemNodeViewModel)
                    {
                        e.Handled = true;
                        await CopyLocationAsync();
                    }
                    break;
            }
        }

        private async void BookmarkManagerControl_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= BookmarkManagerControl_Loaded;

            if (_initData is not null)
            {
                _viewModel.InitializeWithData(_initData.Bookmarks, _initData.FolderPaths, _initData.ExpandedFolders);
            }
            else
            {
                await RunAsync(cancellationToken => _viewModel.InitializeAsync(cancellationToken));
            }
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
            _dragBookmarkId = null;
            _dragFolderPath = null;
            _dragSourceStorage = null;

            BookmarkNodeViewModel? node = GetNodeFromVisual(e.OriginalSource as DependencyObject);
            if (node is BookmarkItemNodeViewModel bookmarkNode)
            {
                _dragBookmarkId = bookmarkNode.Bookmark.BookmarkId;
                _dragSourceStorage = bookmarkNode.Bookmark.StorageLocation;
            }
            else if (node is FolderNodeViewModel folderNode && !folderNode.IsRoot)
            {
                _dragFolderPath = folderNode.FolderPath;
                _dragSourceStorage = folderNode.StorageLocation;
            }
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
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            var hasDragItem = !string.IsNullOrWhiteSpace(_dragBookmarkId) || !string.IsNullOrWhiteSpace(_dragFolderPath);
            if (!hasDragItem)
            {
                return;
            }

            Point currentPosition = e.GetPosition(BookmarkTreeView);
            if (Math.Abs(currentPosition.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(currentPosition.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            var data = new DataObject();
            if (!string.IsNullOrWhiteSpace(_dragBookmarkId))
            {
                data.SetData(BookmarkDragFormat, _dragBookmarkId, false);
            }
            else if (!string.IsNullOrWhiteSpace(_dragFolderPath))
            {
                data.SetData(FolderDragFormat, _dragFolderPath, false);
            }

            DragDrop.DoDragDrop(BookmarkTreeView, data, DragDropEffects.Move);
            _dragBookmarkId = null;
            _dragFolderPath = null;
            _dragSourceStorage = null;
        }

        private void BookmarkTreeView_DragOver(object sender, DragEventArgs e)
        {
            var canDrop = e.Data.GetDataPresent(BookmarkDragFormat, false)
                || e.Data.GetDataPresent(FolderDragFormat, false);

            if (canDrop && e.Data.GetDataPresent(FolderDragFormat, false))
            {
                // Check if dropping folder onto itself or a descendant
                var draggedFolder = e.Data.GetData(FolderDragFormat, false) as string;
                var targetFolder = GetDropTargetFolderPath(e.OriginalSource as DependencyObject);

                if (!string.IsNullOrWhiteSpace(draggedFolder))
                {
                    // Cannot drop folder onto itself
                    if (string.Equals(draggedFolder, targetFolder, StringComparison.OrdinalIgnoreCase))
                    {
                        canDrop = false;
                    }
                    // Cannot drop folder onto a descendant
                    else if (targetFolder.StartsWith(draggedFolder + "/", StringComparison.OrdinalIgnoreCase))
                    {
                        canDrop = false;
                    }
                }
            }

            e.Effects = canDrop ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }

        private async void BookmarkTreeView_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;

            // Handle bookmark drop
            if (e.Data.GetDataPresent(BookmarkDragFormat, false))
            {
                await HandleBookmarkDropAsync(e);
                return;
            }

            // Handle folder drop
            if (e.Data.GetDataPresent(FolderDragFormat, false))
            {
                await HandleFolderDropAsync(e);
                return;
            }
        }

        private async Task HandleBookmarkDropAsync(DragEventArgs e)
        {
            var bookmarkId = e.Data.GetData(BookmarkDragFormat, false) as string;
            if (string.IsNullOrWhiteSpace(bookmarkId))
            {
                return;
            }

            _viewModel.SelectBookmark(bookmarkId);
            ManagedBookmark? selectedBookmark = _viewModel.SelectedBookmark;
            if (selectedBookmark is null)
            {
                return;
            }

            // Determine target folder and storage location
            BookmarkStorageLocation? targetStorage = GetDropTargetStorageLocation(e.OriginalSource as DependencyObject);
            var targetFolderPath = GetDropTargetFolderPath(e.OriginalSource as DependencyObject);

            // Check if moving between storage locations
            if (targetStorage.HasValue && _dragSourceStorage.HasValue && targetStorage.Value != _dragSourceStorage.Value)
            {
                // Move bookmark to different storage location and target folder
                await RunAsync(async cancellationToken =>
                {
                    await BookmarkOperationsService.Current.MoveBookmarkToStorageAsync(bookmarkId!, targetFolderPath, targetStorage.Value, cancellationToken);
                    await _viewModel.RefreshAsync(cancellationToken);
                });
            }
            else if (!string.Equals(selectedBookmark.FolderPath, targetFolderPath, StringComparison.OrdinalIgnoreCase))
            {
                // Move within same storage to different folder
                await RunAsync(cancellationToken => _viewModel.MoveSelectedBookmarkToFolderAsync(targetFolderPath, cancellationToken));
            }
        }

        private async Task HandleFolderDropAsync(DragEventArgs e)
        {
            var sourceFolderPath = e.Data.GetData(FolderDragFormat, false) as string;
            if (string.IsNullOrWhiteSpace(sourceFolderPath))
            {
                return;
            }

            var targetFolderPath = GetDropTargetFolderPath(e.OriginalSource as DependencyObject);

            // Cannot drop folder onto itself or a descendant
            if (string.Equals(sourceFolderPath, targetFolderPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (targetFolderPath.StartsWith(sourceFolderPath + "/", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Get storage locations
            BookmarkStorageLocation? sourceStorage = _dragSourceStorage;
            BookmarkStorageLocation? targetStorage = GetDropTargetStorageLocation(e.OriginalSource as DependencyObject);

            if (!sourceStorage.HasValue || !targetStorage.HasValue)
            {
                return;
            }

            // Get the folder name from the source path
            var folderName = sourceFolderPath.Contains("/")
                ? sourceFolderPath.Substring(sourceFolderPath.LastIndexOf('/') + 1)
                : sourceFolderPath;

            // Build the new path
            var newFolderPath = string.IsNullOrEmpty(targetFolderPath)
                ? folderName
                : targetFolderPath + "/" + folderName;

            // Cannot move if destination would be the same path in the same storage
            if (string.Equals(sourceFolderPath, newFolderPath, StringComparison.OrdinalIgnoreCase)
                && sourceStorage.Value == targetStorage.Value)
            {
                return;
            }

            // Check if this is a cross-storage move
            if (sourceStorage.Value != targetStorage.Value)
            {
                await RunAsync(cancellationToken => _viewModel.MoveFolderAcrossStorageAsync(
                    sourceFolderPath!, sourceStorage.Value,
                    newFolderPath, targetStorage.Value,
                    cancellationToken));
            }
            else
            {
                await RunAsync(cancellationToken => _viewModel.MoveFolderAsync(sourceFolderPath!, newFolderPath, targetStorage.Value, cancellationToken));
            }
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

            var newLabel = TextPromptWindow.Show("Edit Label", "Enter a label for the bookmark:", bookmark.Label, selectTextOnLoad: true);
            if (newLabel is null)
            {
                return;
            }

            _viewModel.SelectedLabelText = newLabel;
            await RunAsync(cancellationToken => _viewModel.SaveSelectionAsync(cancellationToken));
        }

        private async void AssignShortcutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SelectNodeFromContextMenu(sender);

            if (sender is not MenuItem menuItem || menuItem.Tag is null || !int.TryParse(menuItem.Tag.ToString(), out var shortcutNumber))
            {
                return;
            }

            await RunAsync(cancellationToken => _viewModel.AssignSelectedShortcutAsync(shortcutNumber, cancellationToken));
        }

        private void AssignShortcutSubmenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem submenu)
            {
                return;
            }

            Dictionary<int, string> shortcutAssignments = _viewModel.GetShortcutAssignments();

            foreach (var item in submenu.Items)
            {
                if (item is MenuItem shortcutMenuItem && shortcutMenuItem.Tag is not null && int.TryParse(shortcutMenuItem.Tag.ToString(), out var shortcutNumber))
                {
                    if (shortcutAssignments.TryGetValue(shortcutNumber, out var bookmarkLabel))
                    {
                        shortcutMenuItem.Header = string.Concat(shortcutNumber.ToString(System.Globalization.CultureInfo.InvariantCulture), " - ", bookmarkLabel);
                    }
                    else
                    {
                        shortcutMenuItem.Header = string.Concat(shortcutNumber.ToString(System.Globalization.CultureInfo.InvariantCulture), " - Unassigned");
                    }
                }
            }
        }

        private async void ClearShortcutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SelectNodeFromContextMenu(sender);
            await RunAsync(cancellationToken => _viewModel.ClearSelectedShortcutAsync(cancellationToken));
        }

        private async void SetColorMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SelectNodeFromContextMenu(sender);

            if (sender is not MenuItem menuItem || menuItem.Tag is null || !int.TryParse(menuItem.Tag.ToString(), out var colorValue))
            {
                return;
            }

            var color = (BookmarkColor)colorValue;
            await RunAsync(cancellationToken => _viewModel.SetSelectedColorAsync(color, cancellationToken));
        }

        private async void DeleteBookmarkMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SelectNodeFromContextMenu(sender);
            await RunAsync(cancellationToken => _viewModel.DeleteSelectedAsync(cancellationToken));
        }

        private async void MoveToSolutionMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await RunAsync(cancellationToken => _viewModel.MoveToSolutionAsync(cancellationToken));
        }

        private async void MoveToPersonalMenuItem_Click(object sender, RoutedEventArgs e)
        {
            await RunAsync(cancellationToken => _viewModel.MoveToPersonalAsync(cancellationToken));
        }

        private async void AddFolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!SelectFolderFromContextMenu(sender))
            {
                return;
            }

            await PromptCreateFolderInternalAsync();
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

        private async void ClearBookmarksMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!SelectFolderFromContextMenu(sender))
            {
                return;
            }

            if (_viewModel.SelectedNode is not FolderNodeViewModel folderNode || !folderNode.IsRoot)
            {
                _viewModel.SetStatus("Select a root folder to clear bookmarks.");
                return;
            }

            var storageName = folderNode.StorageLocation switch
            {
                BookmarkStorageLocation.Personal => "User",
                BookmarkStorageLocation.Workspace => "Workspace",
                _ => "this storage"
            };

            System.Windows.MessageBoxResult result = System.Windows.MessageBox.Show(
                $"Are you sure you want to clear all bookmarks from {storageName}? This action cannot be undone.",
                "Clear Bookmarks",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning,
                System.Windows.MessageBoxResult.No);

            if (result != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }

            await RunAsync(cancellationToken => _viewModel.ClearBookmarksByStorageAsync(cancellationToken));
        }

        private void OpenBackingFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!SelectFolderFromContextMenu(sender))
            {
                return;
            }

            if (_viewModel.SelectedNode is not FolderNodeViewModel folderNode || !folderNode.IsRoot)
            {
                _viewModel.SetStatus("Select a root folder to open the backing file.");
                return;
            }

            if (!folderNode.StorageLocation.HasValue)
            {
                _viewModel.SetStatus("Cannot determine storage location.");
                return;
            }

            var storagePath = BookmarkOperationsService.Current.GetStoragePathForLocation(folderNode.StorageLocation.Value);

            if (!System.IO.File.Exists(storagePath))
            {
                var storageName = folderNode.StorageLocation.Value == BookmarkStorageLocation.Personal ? "User" : "Workspace";
                System.Windows.MessageBox.Show(
                    $"No bookmark file exists for {storageName} yet. Add a bookmark to create the file.",
                    "File Not Found",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await VS.Documents.OpenAsync(storagePath);
            }).FireAndForget();
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
            var folderName = TextPromptWindow.Show("Add Folder", "Enter a folder name:", string.Empty);
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

            var folderName = TextPromptWindow.Show("Rename Folder", "Enter a new folder name:", folderNode.FolderName, selectTextOnLoad: true);
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

            var newLabel = TextPromptWindow.Show("Edit Label", "Enter a label for the bookmark:", bookmark.Label, selectTextOnLoad: true);
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
                var location = await _viewModel.GetSelectedLocationAsync(CancellationToken.None);
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

            foreach (var child in parent.Items)
            {
                if (parent.ItemContainerGenerator.ContainerFromItem(child) is not TreeViewItem childContainer)
                {
                    continue;
                }

                var wasExpanded = childContainer.IsExpanded;
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

        private static BookmarkStorageLocation? GetDropTargetStorageLocation(DependencyObject? source)
        {
            BookmarkNodeViewModel? targetNode = GetNodeFromVisual(source);

            // Walk up to find the root storage node
            while (targetNode is not null)
            {
                if (targetNode is FolderNodeViewModel folderNode && folderNode.IsRoot)
                {
                    return folderNode.StorageLocation;
                }

                if (targetNode is BookmarkItemNodeViewModel bookmarkNode)
                {
                    return bookmarkNode.Bookmark.StorageLocation;
                }

                // Try to find parent through visual tree
                DependencyObject? visual = source;
                while (visual is not null)
                {
                    if (visual is TreeViewItem tvi && tvi.DataContext is FolderNodeViewModel parentFolder && parentFolder.IsRoot)
                    {
                        return parentFolder.StorageLocation;
                    }

                    visual = VisualTreeHelper.GetParent(visual);
                }

                break;
            }

            return null;
        }
    }
}
