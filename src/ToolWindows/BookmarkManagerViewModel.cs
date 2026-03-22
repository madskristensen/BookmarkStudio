using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;

namespace BookmarkStudio
{
    internal sealed class BookmarkManagerViewModel : INotifyPropertyChanged
    {
        private readonly BookmarkOperationsService _operations = BookmarkOperationsService.Current;
        private readonly List<ManagedBookmark> _bookmarks = new List<ManagedBookmark>();
        private readonly List<ManagedBookmark> _personalBookmarks = new List<ManagedBookmark>();
        private readonly List<ManagedBookmark> _solutionBookmarks = new List<ManagedBookmark>();
        private readonly HashSet<string> _folderPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { string.Empty };
        private readonly HashSet<string> _personalFolderPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { string.Empty };
        private readonly HashSet<string> _solutionFolderPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { string.Empty };
        private readonly HashSet<string> _expandedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool _isTestMode;
        private BookmarkNodeViewModel? _selectedNode;
        private string _searchText = string.Empty;
        private BookmarkColor? _filterColor;
        private int? _selectedSlotNumber;
        private string _selectedLabelText = string.Empty;
        private string _statusText = "Loading bookmarks...";
        private BookmarkStorageInfo? _storageInfo;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<BookmarkNodeViewModel> RootNodes { get; } = new ObservableCollection<BookmarkNodeViewModel>();

        public ObservableCollection<BookmarkGridRowViewModel> BookmarkRows { get; } = new ObservableCollection<BookmarkGridRowViewModel>();

        public IReadOnlyList<int> SlotOptions { get; } = Enumerable.Range(1, 9).ToArray();

        public BookmarkNodeViewModel? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (ReferenceEquals(_selectedNode, value))
                {
                    return;
                }

                _selectedNode = value;

                if (_selectedNode is BookmarkItemNodeViewModel bookmarkNode)
                {
                    SelectedLabelText = bookmarkNode.Bookmark.Label;
                    SelectedSlotNumber = bookmarkNode.Bookmark.SlotNumber;
                }
                else
                {
                    SelectedLabelText = string.Empty;
                    SelectedSlotNumber = null;
                }

                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedBookmark));
                OnPropertyChanged(nameof(SelectedFolderPath));
                OnPropertyChanged(nameof(HasSelectedBookmark));
                OnPropertyChanged(nameof(HasSelectedFolder));
                OnPropertyChanged(nameof(SelectedLocation));
                OnPropertyChanged(nameof(SelectedPreview));
            }
        }

        public ManagedBookmark? SelectedBookmark => (SelectedNode as BookmarkItemNodeViewModel)?.Bookmark;

        public string? SelectedFolderPath => SelectedNode switch
        {
            FolderNodeViewModel folderNode => folderNode.FolderPath,
            BookmarkItemNodeViewModel bookmarkNode => bookmarkNode.Bookmark.FolderPath,
            _ => null,
        };

        public bool HasSelectedBookmark => SelectedBookmark is not null;

        public bool HasSelectedFolder => SelectedNode is FolderNodeViewModel;

        public string SelectedLocation => SelectedBookmark?.Location ?? string.Empty;

        public string SelectedPreview => SelectedBookmark?.LineText ?? string.Empty;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (string.Equals(_searchText, value, StringComparison.Ordinal))
                {
                    return;
                }

                _searchText = value ?? string.Empty;
                OnPropertyChanged();
                RebuildTreeInternal();
            }
        }

        public BookmarkColor? FilterColor
        {
            get => _filterColor;
            set
            {
                if (_filterColor == value)
                {
                    return;
                }

                _filterColor = value;
                OnPropertyChanged();
                RebuildTreeInternal();
            }
        }

        public string SelectedLabelText
        {
            get => _selectedLabelText;
            set
            {
                if (string.Equals(_selectedLabelText, value, StringComparison.Ordinal))
                {
                    return;
                }

                _selectedLabelText = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public int? SelectedSlotNumber
        {
            get => _selectedSlotNumber;
            set
            {
                if (_selectedSlotNumber == value)
                {
                    return;
                }

                _selectedSlotNumber = value;
                OnPropertyChanged();
            }
        }

        public string StatusText
        {
            get => _statusText;
            private set
            {
                if (string.Equals(_statusText, value, StringComparison.Ordinal))
                {
                    return;
                }

                _statusText = value;
                OnPropertyChanged();
            }
        }

        public Task InitializeAsync(CancellationToken cancellationToken)
            => RefreshAsync(performCleanup: false, cancellationToken);

        public void InitializeWithData(
            IReadOnlyList<ManagedBookmark> bookmarks,
            IReadOnlyList<string> folderPaths,
            IEnumerable<string> expandedFolders)
        {
            ReloadData(bookmarks, folderPaths, expandedFolders);
            RebuildTree();
            SetStatus(string.Concat(_bookmarks.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), " bookmarks loaded."));
        }

        /// <summary>
        /// Rebuilds the tree view from the session's cached data without reloading from disk.
        /// Use this when the session cache has already been updated and you just need to refresh the UI.
        /// </summary>
        public void RefreshFromCache()
        {
            string? selectedBookmarkId = SelectedBookmark?.BookmarkId;
            string? selectedFolderPath = SelectedNode is FolderNodeViewModel folderNode ? folderNode.FolderPath : null;

            IReadOnlyList<ManagedBookmark> cachedBookmarks = BookmarkStudioSession.Current.CachedBookmarks;
            IReadOnlyList<string> cachedFolderPaths = BookmarkStudioSession.Current.CachedFolderPaths;

            ReloadData(cachedBookmarks, cachedFolderPaths, _expandedFolders);
            RebuildTree();
            RestoreSelection(selectedBookmarkId, selectedFolderPath);

            int totalBookmarks = _bookmarks.Count;
            SetStatus(string.Concat(totalBookmarks.ToString(System.Globalization.CultureInfo.InvariantCulture), " bookmarks loaded."));
        }

        public Task RefreshAsync(CancellationToken cancellationToken)
            => RefreshAsync(performCleanup: true, cancellationToken);

        public Task RefreshWithoutCleanupAsync(CancellationToken cancellationToken)
            => RefreshAsync(performCleanup: false, cancellationToken);

        private async Task RefreshAsync(bool performCleanup, CancellationToken cancellationToken)
        {
            int staleCount = 0;
            DualBookmarkWorkspaceState dualState;

            if (performCleanup)
            {
                (dualState, staleCount) = await _operations.RefreshDualAndCleanupAsync(cancellationToken);
            }
            else
            {
                dualState = await _operations.RefreshDualAsync(cancellationToken);
            }

            string? selectedBookmarkId = SelectedBookmark?.BookmarkId;
            string? selectedFolderPath = SelectedNode is FolderNodeViewModel folderNode ? folderNode.FolderPath : null;

            ReloadDualData(dualState);
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            RebuildTree();
            RestoreSelection(selectedBookmarkId, selectedFolderPath);

            int totalBookmarks = _personalBookmarks.Count + _solutionBookmarks.Count;
            string statusMessage = staleCount > 0
                ? string.Concat(totalBookmarks.ToString(System.Globalization.CultureInfo.InvariantCulture), " bookmarks loaded. Removed ", staleCount.ToString(System.Globalization.CultureInfo.InvariantCulture), " stale bookmark(s).")
                : string.Concat(totalBookmarks.ToString(System.Globalization.CultureInfo.InvariantCulture), " bookmarks loaded.");
            SetStatus(statusMessage);
        }

        public async Task SaveSelectionAsync(CancellationToken cancellationToken)
        {
            ManagedBookmark selectedBookmark = GetRequiredSelection();
            IReadOnlyList<ManagedBookmark> bookmarks = await _operations.RenameLabelAsync(selectedBookmark.BookmarkId, SelectedLabelText, cancellationToken);
            ReloadDualBookmarks(bookmarks);
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            RebuildTree();
            SelectBookmark(selectedBookmark.BookmarkId);
            SetStatus("Bookmark metadata updated.");
        }

        public async Task AssignSelectedSlotAsync(CancellationToken cancellationToken)
        {
            ManagedBookmark selectedBookmark = GetRequiredSelection();
            if (!SelectedSlotNumber.HasValue)
            {
                throw new InvalidOperationException("Select a slot before assigning it.");
            }

            IReadOnlyList<ManagedBookmark> bookmarks = await _operations.AssignSlotAsync(SelectedSlotNumber.Value, selectedBookmark.BookmarkId, cancellationToken);
            ReloadDualBookmarks(bookmarks);
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            RebuildTree();
            SelectBookmark(selectedBookmark.BookmarkId);
            SetStatus(string.Concat("Slot ", SelectedSlotNumber.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), " assigned."));
        }

        public async Task AssignSelectedSlotAsync(int slotNumber, CancellationToken cancellationToken)
        {
            SelectedSlotNumber = slotNumber;
            await AssignSelectedSlotAsync(cancellationToken);
        }

        public async Task ClearSelectedSlotAsync(CancellationToken cancellationToken)
        {
            ManagedBookmark selectedBookmark = GetRequiredSelection();
            IReadOnlyList<ManagedBookmark> bookmarks = await _operations.ClearSlotAsync(selectedBookmark.BookmarkId, cancellationToken);
            ReloadDualBookmarks(bookmarks);
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            RebuildTree();
            SelectBookmark(selectedBookmark.BookmarkId);
            SetStatus("Bookmark slot cleared.");
        }

        public Dictionary<int, string> GetSlotAssignments()
        {
            Dictionary<int, string> assignments = new Dictionary<int, string>();

            foreach (ManagedBookmark bookmark in _bookmarks)
            {
                if (bookmark.SlotNumber.HasValue && bookmark.SlotNumber.Value >= 1 && bookmark.SlotNumber.Value <= 9)
                {
                    string label = string.IsNullOrWhiteSpace(bookmark.Label) 
                        ? bookmark.FileName 
                        : bookmark.Label;
                    assignments[bookmark.SlotNumber.Value] = label;
                }
            }

            return assignments;
        }

        public async Task SetSelectedColorAsync(BookmarkColor color, CancellationToken cancellationToken)
        {
            ManagedBookmark selectedBookmark = GetRequiredSelection();
            IReadOnlyList<ManagedBookmark> bookmarks = await _operations.SetColorAsync(selectedBookmark.BookmarkId, color, cancellationToken);
            ReloadDualBookmarks(bookmarks);
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            RebuildTree();
            SelectBookmark(selectedBookmark.BookmarkId);
            SetStatus(string.Concat("Bookmark color set to ", color.ToString(), "."));
        }

        public async Task DeleteSelectedAsync(CancellationToken cancellationToken)
        {
            if (SelectedNode is FolderNodeViewModel folderNode)
            {
                if (!folderNode.StorageLocation.HasValue)
                {
                    throw new InvalidOperationException("Cannot determine storage location for the selected folder.");
                }

                IReadOnlyList<ManagedBookmark> bookmarks = await _operations.DeleteFolderRecursiveAsync(folderNode.FolderPath, folderNode.StorageLocation.Value, cancellationToken);
                ReloadDualBookmarks(bookmarks);
                RemoveFolderPathsForStorage(folderNode.FolderPath, folderNode.StorageLocation.Value);
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
                RebuildTree();
                SetStatus("Folder deleted recursively.");
                return;
            }

            ManagedBookmark selectedBookmark = GetRequiredSelection();
            IReadOnlyList<ManagedBookmark> updated = await _operations.RemoveBookmarkAsync(selectedBookmark.BookmarkId, cancellationToken);
            ReloadDualBookmarks(updated);
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            RebuildTree();
            SetStatus("Bookmark removed.");
        }

        public async Task ClearBookmarksByStorageAsync(CancellationToken cancellationToken)
        {
            if (SelectedNode is not FolderNodeViewModel folderNode || !folderNode.IsRoot)
            {
                throw new InvalidOperationException("Select a root folder to clear bookmarks.");
            }

            if (!folderNode.StorageLocation.HasValue)
            {
                throw new InvalidOperationException("Cannot determine storage location for the selected folder.");
            }

            BookmarkStorageLocation storageLocation = folderNode.StorageLocation.Value;
            IReadOnlyList<ManagedBookmark> bookmarks = await _operations.ClearBookmarksByStorageLocationAsync(storageLocation, cancellationToken);
            ReloadDualBookmarks(bookmarks);
            ClearFolderPathsForStorage(storageLocation);
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            RebuildTree();
            string storageName = storageLocation == BookmarkStorageLocation.Personal ? "User" : "Workspace";
            SetStatus($"All bookmarks cleared from {storageName}.");
        }

        public async Task MoveToSolutionAsync(CancellationToken cancellationToken)
        {
            BookmarkStorageInfo newInfo = await _operations.MoveToLocationAsync(BookmarkStorageLocation.Solution, cancellationToken);
            _storageInfo = newInfo;
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            UpdateRootNodeStorageLocation();
            string message = string.Concat("Bookmarks moved to ", newInfo.RelativePath);
            SetStatus(string.Concat(_bookmarks.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), " bookmarks in ", newInfo.RelativePath));
            await VS.StatusBar.ShowMessageAsync(message);
        }

        public async Task MoveToPersonalAsync(CancellationToken cancellationToken)
        {
            BookmarkStorageInfo newInfo = await _operations.MoveToLocationAsync(BookmarkStorageLocation.Personal, cancellationToken);
            _storageInfo = newInfo;
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            UpdateRootNodeStorageLocation();
            string message = string.Concat("Bookmarks moved to ", newInfo.RelativePath);
            SetStatus(string.Concat(_bookmarks.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), " bookmarks in ", newInfo.RelativePath));
            await VS.StatusBar.ShowMessageAsync(message);
        }

        public bool CanMoveToSolution => _storageInfo?.Location == BookmarkStorageLocation.Personal;

        public bool CanMoveToPersonal => _storageInfo?.Location == BookmarkStorageLocation.Solution;

        private void UpdateRootNodeStorageLocation()
        {
            if (RootNodes.Count > 0 && RootNodes[0] is FolderNodeViewModel rootNode && rootNode.IsRoot)
            {
                rootNode.StorageLocation = _storageInfo?.Location;
            }

            OnPropertyChanged(nameof(CanMoveToSolution));
            OnPropertyChanged(nameof(CanMoveToPersonal));
        }

        public async Task CreateFolderAsync(string folderName, CancellationToken cancellationToken)
        {
            string parentPath;
            BookmarkStorageLocation storageLocation;

            switch (SelectedNode)
            {
                case FolderNodeViewModel folderNode:
                    parentPath = folderNode.FolderPath;
                    storageLocation = folderNode.StorageLocation ?? BookmarkStorageLocation.Solution;
                    break;
                case BookmarkItemNodeViewModel bookmarkNode:
                    parentPath = bookmarkNode.Bookmark.FolderPath;
                    storageLocation = bookmarkNode.Bookmark.StorageLocation;
                    break;
                default:
                    parentPath = string.Empty;
                    storageLocation = BookmarkStorageLocation.Solution;
                    break;
            }

            IReadOnlyList<ManagedBookmark> bookmarks = await _operations.CreateFolderAsync(parentPath, folderName, storageLocation, cancellationToken);
            ReloadDualBookmarks(bookmarks);

            string createdPath = string.IsNullOrEmpty(parentPath)
                ? BookmarkIdentity.NormalizeFolderPath(folderName)
                : string.Concat(parentPath, "/", BookmarkIdentity.NormalizeFolderPath(folderName));

            AddFolderPathForStorage(createdPath, storageLocation);
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            RebuildTree();
            SelectFolder(createdPath);
            SetStatus("Folder created.");
        }

        public async Task RenameSelectedFolderAsync(string folderName, CancellationToken cancellationToken)
        {
            FolderNodeViewModel folderNode = GetRequiredSelectedFolder();
            BookmarkStorageLocation storageLocation = folderNode.StorageLocation ?? BookmarkStorageLocation.Solution;

            IReadOnlyList<ManagedBookmark> bookmarks = await _operations.RenameFolderAsync(folderNode.FolderPath, folderName, storageLocation, cancellationToken);
            ReloadDualBookmarks(bookmarks);

            string parentPath = GetParentFolderPath(folderNode.FolderPath);
            string renamedPath = string.IsNullOrEmpty(parentPath)
                ? BookmarkIdentity.NormalizeFolderPath(folderName)
                : string.Concat(parentPath, "/", BookmarkIdentity.NormalizeFolderPath(folderName));

            // Update folder paths in both global and storage-specific collections
            UpdateFolderPathsAfterMove(folderNode.FolderPath, renamedPath, storageLocation);

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            RebuildTree();
            SelectFolder(renamedPath);
            SetStatus("Folder renamed.");
        }

        public async Task MoveSelectedBookmarkToFolderAsync(string targetFolderPath, CancellationToken cancellationToken)
        {
            ManagedBookmark selectedBookmark = GetRequiredSelection();
            IReadOnlyList<ManagedBookmark> bookmarks = await _operations.MoveBookmarkToFolderAsync(
                selectedBookmark.BookmarkId,
                targetFolderPath,
                selectedBookmark.StorageLocation,
                cancellationToken);
            ReloadDualBookmarks(bookmarks);
            AddFolderPathForStorage(targetFolderPath, selectedBookmark.StorageLocation);
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            RebuildTree();
            SelectBookmark(selectedBookmark.BookmarkId);
            SetStatus("Bookmark moved.");
        }

        public async Task MoveFolderAsync(string sourceFolderPath, string targetFolderPath, BookmarkStorageLocation storageLocation, CancellationToken cancellationToken)
        {
            string normalizedSource = BookmarkIdentity.NormalizeFolderPath(sourceFolderPath);
            string normalizedTarget = BookmarkIdentity.NormalizeFolderPath(targetFolderPath);

            if (string.IsNullOrWhiteSpace(normalizedSource))
            {
                throw new ArgumentException("Cannot move the root folder.", nameof(sourceFolderPath));
            }

            if (string.Equals(normalizedSource, normalizedTarget, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Cannot move a folder into itself or a descendant
            if (normalizedTarget.StartsWith(normalizedSource + "/", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Cannot move a folder into itself or a descendant.", nameof(targetFolderPath));
            }

            IReadOnlyList<ManagedBookmark> bookmarks = await _operations.MoveFolderAsync(normalizedSource, normalizedTarget, storageLocation, cancellationToken);
            ReloadDualBookmarks(bookmarks);

            // Update folder paths for the specific storage
            UpdateFolderPathsAfterMove(normalizedSource, normalizedTarget, storageLocation);

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            RebuildTree();
            SelectFolder(normalizedTarget);
            SetStatus("Folder moved.");
        }

        public async Task MoveFolderAcrossStorageAsync(
            string sourceFolderPath,
            BookmarkStorageLocation sourceStorage,
            string targetFolderPath,
            BookmarkStorageLocation targetStorage,
            CancellationToken cancellationToken)
        {
            string normalizedSource = BookmarkIdentity.NormalizeFolderPath(sourceFolderPath);
            string normalizedTarget = BookmarkIdentity.NormalizeFolderPath(targetFolderPath);

            if (string.IsNullOrWhiteSpace(normalizedSource))
            {
                throw new ArgumentException("Cannot move the root folder.", nameof(sourceFolderPath));
            }

            // Move folder and all its contents (bookmarks and subfolders) to the target storage
            IReadOnlyList<ManagedBookmark> bookmarks = await _operations.MoveFolderBetweenStorageAsync(
                normalizedSource, sourceStorage,
                normalizedTarget, targetStorage,
                cancellationToken);
            ReloadDualBookmarks(bookmarks);

            // Update folder paths tracking
            RemoveFolderPathsForStorage(normalizedSource, sourceStorage);
            AddFolderPathForStorage(normalizedTarget, targetStorage);

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            RebuildTree();
            SelectFolder(normalizedTarget);
            SetStatus("Folder moved to " + (targetStorage == BookmarkStorageLocation.Personal ? "User" : "Workspace") + " storage.");
        }

        public Task NavigateSelectedAsync(CancellationToken cancellationToken)
            => _operations.NavigateToBookmarkAsync(GetRequiredSelection().BookmarkId, cancellationToken);

        public Task<string> GetSelectedLocationAsync(CancellationToken cancellationToken)
            => _operations.GetBookmarkLocationAsync(GetRequiredSelection().BookmarkId, cancellationToken);

        internal void SetStatus(string statusText)
            => StatusText = statusText ?? string.Empty;

        internal int ApplySearchText(string searchText)
        {
            if (!_isTestMode)
            {
                ThrowIfNotOnUIThread();
            }

            SearchText = searchText;
            return CountVisibleBookmarks(RootNodes);
        }

        internal void Clear()
        {
            if (!_isTestMode)
            {
                ThrowIfNotOnUIThread();
            }

            _bookmarks.Clear();
            RootNodes.Clear();
            BookmarkRows.Clear();
            _folderPaths.Clear();
            _folderPaths.Add(string.Empty);
            SelectedNode = null;
            SearchText = string.Empty;
            SetStatus("No solution loaded.");
        }

        internal void ClearSearch()
        {
            if (!_isTestMode)
            {
                ThrowIfNotOnUIThread();
            }

            SearchText = string.Empty;
        }

        internal void SelectBookmark(string? bookmarkId)
        {
            if (string.IsNullOrWhiteSpace(bookmarkId))
            {
                SelectedNode = null;
                return;
            }

            BookmarkItemNodeViewModel? node = FindBookmarkNode(RootNodes, bookmarkId!);
            SelectedNode = node;
        }

        internal void SelectFolder(string? folderPath)
        {
            string normalizedPath = BookmarkIdentity.NormalizeFolderPath(folderPath);
            FolderNodeViewModel? node = FindFolderNode(RootNodes, normalizedPath);
            SelectedNode = node;
        }

        internal void LoadForTests(IEnumerable<ManagedBookmark> bookmarks, IEnumerable<string>? folderPaths = null)
        {
            _isTestMode = true;
            ReloadData(
                (bookmarks ?? Enumerable.Empty<ManagedBookmark>()).ToArray(),
                (folderPaths ?? Enumerable.Empty<string>()).ToArray(),
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            RebuildTreeForTests();
        }

        private void RebuildTreeForTests()
        {
            string? selectedBookmarkId = SelectedBookmark?.BookmarkId;
            string? selectedFolderPath = SelectedNode is FolderNodeViewModel folderNode ? folderNode.FolderPath : null;

            RootNodes.Clear();
            BookmarkRows.Clear();

            FolderBuilderNode root = new FolderBuilderNode(string.Empty, null);
            foreach (string folderPath in _folderPaths)
            {
                EnsureFolderBuilderNode(root, folderPath);
            }

            IEnumerable<ManagedBookmark> sourceBookmarks = _bookmarks;
            string search = SearchText.Trim();
            if (!string.IsNullOrWhiteSpace(search))
            {
                sourceBookmarks = sourceBookmarks.Where(bookmark => MatchesSearch(bookmark, search));
            }

            if (_filterColor.HasValue)
            {
                BookmarkColor filterValue = _filterColor.Value;
                sourceBookmarks = sourceBookmarks.Where(bookmark => bookmark.Color == filterValue);
            }

            List<ManagedBookmark> visibleBookmarks = sourceBookmarks.ToList();
            RebuildBookmarkRows(root, visibleBookmarks);

            foreach (ManagedBookmark bookmark in visibleBookmarks)
            {
                FolderBuilderNode folder = EnsureFolderBuilderNode(root, bookmark.FolderPath);
                folder.Bookmarks.Add(bookmark);
            }

            FolderNodeViewModel rootNode = new FolderNodeViewModel(string.Empty, 0, _storageInfo?.Location);
            rootNode.IsExpanded = true;
            rootNode.IsExpandedChanged += OnFolderExpandedChanged;

            foreach (FolderBuilderNode childFolder in root.Children.Values.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
            {
                FolderNodeViewModel node = BuildFolderNode(childFolder, 1, _storageInfo?.Location ?? BookmarkStorageLocation.Personal);
                rootNode.Children.Add(node);
            }

            foreach (ManagedBookmark rootBookmark in root.Bookmarks.OrderBy(item => item.LineNumber).ThenBy(item => item.FileName, StringComparer.OrdinalIgnoreCase))
            {
                rootNode.Children.Add(new BookmarkItemNodeViewModel(rootBookmark, 1));
            }

            RootNodes.Add(rootNode);

            RestoreSelection(selectedBookmarkId, selectedFolderPath);
        }

        private void ReloadData(IReadOnlyList<ManagedBookmark> bookmarks, IReadOnlyList<string> folderPaths, IEnumerable<string> expandedFolders)
        {
            ReloadBookmarks(bookmarks);

            _folderPaths.Clear();
            _folderPaths.Add(string.Empty);
            foreach (string folderPath in folderPaths)
            {
                _folderPaths.Add(BookmarkIdentity.NormalizeFolderPath(folderPath));
            }

            _expandedFolders.Clear();
            foreach (string folder in expandedFolders)
            {
                _expandedFolders.Add(BookmarkIdentity.NormalizeFolderPath(folder));
            }
        }

        private void ReloadDualData(DualBookmarkWorkspaceState dualState)
        {
            _bookmarks.Clear();
            _personalBookmarks.Clear();
            _solutionBookmarks.Clear();
            _folderPaths.Clear();
            _personalFolderPaths.Clear();
            _solutionFolderPaths.Clear();

            _folderPaths.Add(string.Empty);
            _personalFolderPaths.Add(string.Empty);
            _solutionFolderPaths.Add(string.Empty);

            foreach (ManagedBookmark bookmark in dualState.PersonalState.Bookmarks.Select(b => b.ToManagedBookmark()))
            {
                _bookmarks.Add(bookmark);
                _personalBookmarks.Add(bookmark);
                _personalFolderPaths.Add(bookmark.FolderPath);
                _folderPaths.Add(bookmark.FolderPath);
            }

            foreach (ManagedBookmark bookmark in dualState.SolutionState.Bookmarks.Select(b => b.ToManagedBookmark()))
            {
                _bookmarks.Add(bookmark);
                _solutionBookmarks.Add(bookmark);
                _solutionFolderPaths.Add(bookmark.FolderPath);
                _folderPaths.Add(bookmark.FolderPath);
            }

            foreach (string folderPath in dualState.PersonalState.FolderPaths)
            {
                _personalFolderPaths.Add(BookmarkIdentity.NormalizeFolderPath(folderPath));
                _folderPaths.Add(BookmarkIdentity.NormalizeFolderPath(folderPath));
            }

            foreach (string folderPath in dualState.SolutionState.FolderPaths)
            {
                _solutionFolderPaths.Add(BookmarkIdentity.NormalizeFolderPath(folderPath));
                _folderPaths.Add(BookmarkIdentity.NormalizeFolderPath(folderPath));
            }

            _expandedFolders.Clear();
            foreach (string folder in dualState.PersonalState.ExpandedFolders)
            {
                _expandedFolders.Add(BookmarkIdentity.NormalizeFolderPath(folder));
            }

            foreach (string folder in dualState.SolutionState.ExpandedFolders)
            {
                _expandedFolders.Add(BookmarkIdentity.NormalizeFolderPath(folder));
            }
        }

        private void ReloadBookmarks(IReadOnlyList<ManagedBookmark> bookmarks)
        {
            _bookmarks.Clear();
            foreach (ManagedBookmark bookmark in bookmarks)
            {
                _bookmarks.Add(bookmark);
                _folderPaths.Add(bookmark.FolderPath);
            }
        }

        private void ReloadDualBookmarks(IReadOnlyList<ManagedBookmark> bookmarks)
        {
            _bookmarks.Clear();
            _personalBookmarks.Clear();
            _solutionBookmarks.Clear();
            _folderPaths.Clear();
            _personalFolderPaths.Clear();
            _solutionFolderPaths.Clear();

            _folderPaths.Add(string.Empty);
            _personalFolderPaths.Add(string.Empty);
            _solutionFolderPaths.Add(string.Empty);

            foreach (ManagedBookmark bookmark in bookmarks)
            {
                _bookmarks.Add(bookmark);
                _folderPaths.Add(bookmark.FolderPath);

                if (bookmark.StorageLocation == BookmarkStorageLocation.Personal)
                {
                    _personalBookmarks.Add(bookmark);
                    _personalFolderPaths.Add(bookmark.FolderPath);
                }
                else if (bookmark.StorageLocation == BookmarkStorageLocation.Solution)
                {
                    _solutionBookmarks.Add(bookmark);
                    _solutionFolderPaths.Add(bookmark.FolderPath);
                }
            }
        }

        private void RemoveFolderPathsForStorage(string folderPath, BookmarkStorageLocation storageLocation)
        {
            string prefix = folderPath + "/";

            _folderPaths.RemoveWhere(path =>
                string.Equals(path, folderPath, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

            HashSet<string> targetSet = storageLocation == BookmarkStorageLocation.Personal
                ? _personalFolderPaths
                : _solutionFolderPaths;

            targetSet.RemoveWhere(path =>
                string.Equals(path, folderPath, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        private void ClearFolderPathsForStorage(BookmarkStorageLocation storageLocation)
        {
            HashSet<string> targetSet = storageLocation == BookmarkStorageLocation.Personal
                ? _personalFolderPaths
                : _solutionFolderPaths;

            // Remove all paths from this storage from the global set
            _folderPaths.RemoveWhere(path => targetSet.Contains(path));
            
            // Clear the storage-specific set and add back the root
            targetSet.Clear();
            targetSet.Add(string.Empty);
        }

        private void AddFolderPathForStorage(string folderPath, BookmarkStorageLocation storageLocation)
        {
            string normalized = BookmarkIdentity.NormalizeFolderPath(folderPath);
            _folderPaths.Add(normalized);

            HashSet<string> targetSet = storageLocation == BookmarkStorageLocation.Personal
                ? _personalFolderPaths
                : _solutionFolderPaths;

            targetSet.Add(normalized);
        }

        private void UpdateFolderPathsAfterMove(string sourceFolder, string targetFolder, BookmarkStorageLocation storageLocation)
        {
            string sourcePrefix = sourceFolder + "/";

            HashSet<string> targetSet = storageLocation == BookmarkStorageLocation.Personal
                ? _personalFolderPaths
                : _solutionFolderPaths;

            // Find affected paths in both the global and storage-specific sets
            List<string> affectedGlobal = _folderPaths
                .Where(path => string.Equals(path, sourceFolder, StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase))
                .ToList();

            List<string> affectedStorage = targetSet
                .Where(path => string.Equals(path, sourceFolder, StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Remove old paths
            foreach (string path in affectedGlobal)
            {
                _folderPaths.Remove(path);
            }

            foreach (string path in affectedStorage)
            {
                targetSet.Remove(path);
            }

            // Add new paths
            foreach (string path in affectedGlobal)
            {
                string newPath = string.Equals(path, sourceFolder, StringComparison.OrdinalIgnoreCase)
                    ? targetFolder
                    : targetFolder + path.Substring(sourceFolder.Length);
                _folderPaths.Add(newPath);
            }

            foreach (string path in affectedStorage)
            {
                string newPath = string.Equals(path, sourceFolder, StringComparison.OrdinalIgnoreCase)
                    ? targetFolder
                    : targetFolder + path.Substring(sourceFolder.Length);
                targetSet.Add(newPath);
            }
        }

        private void RestoreSelection(string? bookmarkId, string? folderPath)
        {
            if (!string.IsNullOrWhiteSpace(bookmarkId))
            {
                SelectBookmark(bookmarkId);
                if (SelectedNode is not null)
                {
                    return;
                }
            }

            if (!string.IsNullOrWhiteSpace(folderPath))
            {
                SelectFolder(folderPath);
                if (SelectedNode is not null)
                {
                    return;
                }
            }

            SelectedNode = GetDefaultSelectedNode();
        }

        private BookmarkNodeViewModel? GetDefaultSelectedNode()
        {
            BookmarkNodeViewModel? firstNode = RootNodes.FirstOrDefault();
            if (firstNode is not FolderNodeViewModel rootNode
                || !string.IsNullOrWhiteSpace(rootNode.FolderPath)
                || rootNode.Children.Count == 0)
            {
                return firstNode;
            }

            return rootNode.Children[0];
        }

        private void RebuildTreeInternal()
        {
            if (_isTestMode)
            {
                RebuildTreeForTests();
            }
            else
            {
                RebuildTree();
            }
        }

        private void RebuildTree()
        {
            if (!_isTestMode)
            {
                ThrowIfNotOnUIThread();
            }

            string? selectedBookmarkId = SelectedBookmark?.BookmarkId;
            string? selectedFolderPath = SelectedNode is FolderNodeViewModel folderNode ? folderNode.FolderPath : null;

            RootNodes.Clear();

            if (!_isTestMode && !_operations.IsSolutionOrFolderOpen())
            {
                RestoreSelection(selectedBookmarkId, selectedFolderPath);
                return;
            }

            string search = SearchText.Trim();

            // Build Personal storage root node
            FolderNodeViewModel personalRoot = BuildStorageRootNode(
                BookmarkStorageLocation.Personal,
                _personalBookmarks,
                _personalFolderPaths,
                search);
            RootNodes.Add(personalRoot);

            // Build Solution storage root node
            FolderNodeViewModel solutionRoot = BuildStorageRootNode(
                BookmarkStorageLocation.Solution,
                _solutionBookmarks,
                _solutionFolderPaths,
                search);
            RootNodes.Add(solutionRoot);

            RestoreSelection(selectedBookmarkId, selectedFolderPath);
        }

        private FolderNodeViewModel BuildStorageRootNode(
            BookmarkStorageLocation storageLocation,
            IReadOnlyList<ManagedBookmark> bookmarks,
            IEnumerable<string> folderPaths,
            string search)
        {
            FolderBuilderNode root = new FolderBuilderNode(string.Empty, null);
            foreach (string folderPath in folderPaths)
            {
                EnsureFolderBuilderNode(root, folderPath);
            }

            IEnumerable<ManagedBookmark> sourceBookmarks = bookmarks;
            if (!string.IsNullOrWhiteSpace(search))
            {
                sourceBookmarks = sourceBookmarks.Where(bookmark => MatchesSearch(bookmark, search));
            }

            if (_filterColor.HasValue)
            {
                BookmarkColor filterValue = _filterColor.Value;
                sourceBookmarks = sourceBookmarks.Where(bookmark => bookmark.Color == filterValue);
            }

            List<ManagedBookmark> visibleBookmarks = sourceBookmarks.ToList();

            foreach (ManagedBookmark bookmark in visibleBookmarks)
            {
                FolderBuilderNode folder = EnsureFolderBuilderNode(root, bookmark.FolderPath);
                folder.Bookmarks.Add(bookmark);
            }

            FolderNodeViewModel rootNode = new FolderNodeViewModel(string.Empty, 0, storageLocation);
            rootNode.IsExpanded = true;
            rootNode.IsExpandedChanged += OnFolderExpandedChanged;

            foreach (FolderBuilderNode childFolder in root.Children.Values.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
            {
                FolderNodeViewModel node = BuildFolderNode(childFolder, 1, storageLocation);
                rootNode.Children.Add(node);
            }

            foreach (ManagedBookmark rootBookmark in root.Bookmarks.OrderBy(item => item.LineNumber).ThenBy(item => item.FileName, StringComparer.OrdinalIgnoreCase))
            {
                rootNode.Children.Add(new BookmarkItemNodeViewModel(rootBookmark, 1));
            }

            return rootNode;
        }

        private void OnFolderExpandedChanged(object? sender, EventArgs e)
        {
            if (sender is not FolderNodeViewModel folderNode)
            {
                return;
            }

            if (folderNode.IsExpanded)
            {
                _expandedFolders.Add(folderNode.FolderPath);
            }
            else
            {
                _expandedFolders.Remove(folderNode.FolderPath);
            }

            _ = SaveExpandedFoldersAsync();
        }

        private async Task SaveExpandedFoldersAsync()
        {
            try
            {
                await _operations.SetExpandedFoldersAsync(_expandedFolders, CancellationToken.None);
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
            }
        }

        private void RebuildBookmarkRows(FolderBuilderNode root, IReadOnlyCollection<ManagedBookmark> bookmarks)
        {
            BookmarkRows.Clear();

            HashSet<string> foldersWithBookmarks = new HashSet<string>(
                bookmarks.Select(bookmark => BookmarkIdentity.NormalizeFolderPath(bookmark.FolderPath)),
                StringComparer.OrdinalIgnoreCase);

            foreach (string folderPath in _folderPaths
                .Select(BookmarkIdentity.NormalizeFolderPath)
                .Where(path => !string.IsNullOrWhiteSpace(path) && !foldersWithBookmarks.Contains(path))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                EnsureFolderBuilderNode(root, folderPath);
                BookmarkRows.Add(BookmarkGridRowViewModel.CreateFolderPlaceholder(folderPath));
            }

            foreach (ManagedBookmark bookmark in bookmarks
                .OrderBy(item => item.FileName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.LineNumber)
                .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase))
            {
                BookmarkRows.Add(new BookmarkGridRowViewModel(bookmark));
            }
        }

        private static bool MatchesSearch(ManagedBookmark bookmark, string search)
        {
            return Contains(bookmark.Label, search)
                || Contains(bookmark.FileName, search)
                || Contains(bookmark.DocumentPath, search)
                || Contains(bookmark.LineText, search)
                || Contains(bookmark.Location, search)
                || Contains(bookmark.SlotNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture), search)
                || (bookmark.Color != BookmarkColor.None && Contains(bookmark.Color.ToString(), search));
        }

        private static FolderBuilderNode EnsureFolderBuilderNode(FolderBuilderNode root, string? folderPath)
        {
            string normalizedPath = BookmarkIdentity.NormalizeFolderPath(folderPath);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                return root;
            }

            FolderBuilderNode current = root;
            foreach (string segment in normalizedPath.Split('/'))
            {
                if (!current.Children.TryGetValue(segment, out FolderBuilderNode? child))
                {
                    child = new FolderBuilderNode(segment, current);
                    current.Children[segment] = child;
                }

                current = child;
            }

            return current;
        }

        private FolderNodeViewModel BuildFolderNode(FolderBuilderNode folderNode, int treeDepth, BookmarkStorageLocation storageLocation)
        {
            FolderNodeViewModel node = new FolderNodeViewModel(folderNode.Path, treeDepth, storageLocation);
            node.IsExpanded = _expandedFolders.Contains(folderNode.Path);
            node.IsExpandedChanged += OnFolderExpandedChanged;

            foreach (FolderBuilderNode child in folderNode.Children.Values.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
            {
                FolderNodeViewModel childNode = BuildFolderNode(child, treeDepth + 1, storageLocation);
                node.Children.Add(childNode);
            }

            foreach (ManagedBookmark bookmark in folderNode.Bookmarks.OrderBy(item => item.LineNumber).ThenBy(item => item.FileName, StringComparer.OrdinalIgnoreCase))
            {
                node.Children.Add(new BookmarkItemNodeViewModel(bookmark, treeDepth + 1));
            }

            return node;
        }

        private FolderNodeViewModel GetRequiredSelectedFolder()
            => SelectedNode as FolderNodeViewModel ?? throw new InvalidOperationException("Select a folder first.");

        private ManagedBookmark GetRequiredSelection()
            => SelectedBookmark ?? throw new InvalidOperationException("Select a bookmark first.");

        private static FolderNodeViewModel? FindFolderNode(IEnumerable<BookmarkNodeViewModel> nodes, string folderPath)
        {
            foreach (BookmarkNodeViewModel node in nodes)
            {
                if (node is FolderNodeViewModel folderNode)
                {
                    if (string.Equals(folderNode.FolderPath, folderPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return folderNode;
                    }

                    FolderNodeViewModel? found = FindFolderNode(folderNode.Children, folderPath);
                    if (found is not null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }

        private static BookmarkItemNodeViewModel? FindBookmarkNode(IEnumerable<BookmarkNodeViewModel> nodes, string bookmarkId)
        {
            foreach (BookmarkNodeViewModel node in nodes)
            {
                if (node is BookmarkItemNodeViewModel bookmarkNode)
                {
                    if (string.Equals(bookmarkNode.Bookmark.BookmarkId, bookmarkId, StringComparison.Ordinal))
                    {
                        return bookmarkNode;
                    }

                    continue;
                }

                if (node is FolderNodeViewModel folderNode)
                {
                    BookmarkItemNodeViewModel? found = FindBookmarkNode(folderNode.Children, bookmarkId);
                    if (found is not null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }

        private static string GetParentFolderPath(string folderPath)
        {
            string normalized = BookmarkIdentity.NormalizeFolderPath(folderPath);
            int separator = normalized.LastIndexOf('/');
            return separator <= 0
                ? string.Empty
                : normalized.Substring(0, separator);
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
                else if (node is FolderNodeViewModel folderNode)
                {
                    count += CountVisibleBookmarks(folderNode.Children);
                }
            }

            return count;
        }

        private static bool Contains(string? value, string search)
            => !string.IsNullOrWhiteSpace(value)
                && value!.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void ThrowIfNotOnUIThread()
            => ThreadHelper.ThrowIfNotOnUIThread();

        private sealed class FolderBuilderNode
        {
            public FolderBuilderNode(string name, FolderBuilderNode? parent)
            {
                Name = name;
                Parent = parent;
            }

            public string Name { get; }

            public FolderBuilderNode? Parent { get; }

            public string Path => string.IsNullOrEmpty(Parent?.Path)
                ? Name
                : string.Concat(Parent!.Path, "/", Name);

            public Dictionary<string, FolderBuilderNode> Children { get; } = new Dictionary<string, FolderBuilderNode>(StringComparer.OrdinalIgnoreCase);

            public List<ManagedBookmark> Bookmarks { get; } = new List<ManagedBookmark>();
        }
    }

    public abstract class BookmarkNodeViewModel
    {
        public abstract bool IsFolder { get; }

        public abstract string DisplayText { get; }
    }

    public sealed class FolderNodeViewModel : BookmarkNodeViewModel, INotifyPropertyChanged
    {
        private bool _isExpanded;
        private BookmarkStorageLocation? _storageLocation;

        public FolderNodeViewModel(string folderPath, int treeDepth)
            : this(folderPath, treeDepth, storageLocation: null)
        {
        }

        internal FolderNodeViewModel(string folderPath, int treeDepth, BookmarkStorageLocation? storageLocation)
        {
            FolderPath = BookmarkIdentity.NormalizeFolderPath(folderPath);
            TreeDepth = Math.Max(0, treeDepth);
            _storageLocation = storageLocation;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public event EventHandler? IsExpandedChanged;

        public string FolderPath { get; }

        public bool IsRoot => string.IsNullOrWhiteSpace(FolderPath);

        internal BookmarkStorageLocation? StorageLocation
        {
            get => _storageLocation;
            set
            {
                if (_storageLocation != value)
                {
                    _storageLocation = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StorageLocation)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FolderName)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayText)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPersonalRoot)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSolutionRoot)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanMoveToSolution)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanMoveToPersonal)));
                }
            }
        }

        public bool IsPersonalRoot => IsRoot && _storageLocation == BookmarkStorageLocation.Personal;

        public bool IsSolutionRoot => IsRoot && _storageLocation == BookmarkStorageLocation.Solution;

        public bool CanMoveToSolution => IsRoot && _storageLocation == BookmarkStorageLocation.Personal;

        public bool CanMoveToPersonal => IsRoot && _storageLocation == BookmarkStorageLocation.Solution;

        public string FolderName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(FolderPath))
                {
                    return FolderPath.Split('/').Last();
                }

                return _storageLocation switch
                {
                    BookmarkStorageLocation.Personal => "User",
                    BookmarkStorageLocation.Solution => "Workspace",
                    _ => "Root",
                };
            }
        }

        public int TreeDepth { get; }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
                    IsExpandedChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public ObservableCollection<BookmarkNodeViewModel> Children { get; } = new ObservableCollection<BookmarkNodeViewModel>();

        public override bool IsFolder => true;

        public override string DisplayText => FolderName;
    }

    public sealed class BookmarkItemNodeViewModel : BookmarkNodeViewModel
    {
        private const int TreeIndentPixels = 19;

        public BookmarkItemNodeViewModel(ManagedBookmark bookmark, int treeDepth)
        {
            Bookmark = bookmark ?? throw new ArgumentNullException(nameof(bookmark));
            TreeDepth = Math.Max(0, treeDepth);
        }

        public ManagedBookmark Bookmark { get; }

        public int TreeDepth { get; }

        public Thickness NonNameColumnMargin => new Thickness(-(Math.Max(0, TreeDepth - 1) * TreeIndentPixels), 0, 0, 0);

        public Thickness NonNameRightAlignedMargin => new Thickness(-(Math.Max(0, TreeDepth - 1) * TreeIndentPixels), 0, 4, 0);

        public override bool IsFolder => false;

        public override string DisplayText => string.IsNullOrWhiteSpace(Bookmark.Label)
            ? Bookmark.FileName
            : Bookmark.Label;
    }

    public sealed class BookmarkGridRowViewModel
    {
        private readonly ManagedBookmark? _bookmark;
        private readonly string _folderPath;

        public BookmarkGridRowViewModel(ManagedBookmark bookmark)
        {
            _bookmark = bookmark ?? throw new ArgumentNullException(nameof(bookmark));
            _folderPath = bookmark.FolderPath;
        }

        private BookmarkGridRowViewModel(string folderPath)
        {
            _folderPath = BookmarkIdentity.NormalizeFolderPath(folderPath);
        }

        public static BookmarkGridRowViewModel CreateFolderPlaceholder(string folderPath)
            => new BookmarkGridRowViewModel(folderPath);

        public bool IsFolderPlaceholder => _bookmark is null;

        public ManagedBookmark? Bookmark => _bookmark;

        public string? BookmarkId => Bookmark?.BookmarkId;

        public string Name => IsFolderPlaceholder
            ? string.Empty
            : string.IsNullOrWhiteSpace(Bookmark!.Label)
            ? Bookmark.FileName
            : Bookmark.Label;

        public string File => Bookmark?.RepositoryRelativePath ?? string.Empty;

        public string LineText => Bookmark?.LineText ?? string.Empty;

        public int? Line => Bookmark?.LineNumber;

        public int? Slot => Bookmark?.SlotNumber;

        public BookmarkColor Color => Bookmark?.Color ?? BookmarkColor.None;

        public string DocumentPath => Bookmark?.DocumentPath ?? string.Empty;

        public string FolderPath => _folderPath;

        public string FolderDisplayName => string.IsNullOrWhiteSpace(FolderPath)
            ? "Root"
            : FolderPath;

        public int FolderDepth => string.IsNullOrWhiteSpace(FolderPath)
            ? 0
            : FolderPath.Split('/').Length;

        public Thickness ColorIndentMargin => new Thickness(4 + (FolderDepth * 12), 0, 0, 0);

        public Thickness NameIndentMargin => new Thickness(FolderDepth * 12, 0, 0, 0);
    }
}