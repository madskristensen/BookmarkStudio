using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace BookmarkStudio
{
    internal sealed class BookmarkStudioSession
    {
        private static readonly Lazy<BookmarkStudioSession> _instance = new Lazy<BookmarkStudioSession>(() => new BookmarkStudioSession());

        private readonly BookmarkMetadataStore _metadataStore = new BookmarkMetadataStore();
        private readonly BookmarkRepositoryService _repositoryService;
        private readonly SemaphoreSlim _repositoryGate = new SemaphoreSlim(1, 1);
        private IReadOnlyList<ManagedBookmark> _cachedBookmarks = Array.Empty<ManagedBookmark>();
        private IReadOnlyList<string> _cachedFolderPaths = new[] { string.Empty };
        private string? _cachedSolutionPath;

        private BookmarkStudioSession()
        {
            _repositoryService = new BookmarkRepositoryService(_metadataStore);
        }

        public static BookmarkStudioSession Current => _instance.Value;

        public event EventHandler? BookmarksChanged;

        public IReadOnlyList<ManagedBookmark> CachedBookmarks => _cachedBookmarks;

        public IReadOnlyList<string> CachedFolderPaths => _cachedFolderPaths;

        public string CachedSolutionPath => _cachedSolutionPath ?? string.Empty;

        public BookmarkMetadataStore MetadataStore => _metadataStore;

        /// <summary>
        /// Invalidates the cached solution path. Call when solution opens or closes.
        /// </summary>
        public void InvalidateSolutionPath()
        {
            _cachedSolutionPath = null;
        }

        public async Task<IReadOnlyList<ManagedBookmark>> RefreshAsync(CancellationToken cancellationToken)
        {
            await _repositoryGate.WaitAsync(cancellationToken);
            try
            {
                string solutionPath = await GetSolutionPathAsync(cancellationToken);
                BookmarkWorkspaceState workspace = await _repositoryService.LoadWorkspaceAsync(solutionPath, cancellationToken);
                BookmarkRepositoryService.NormalizeWorkspaceState(workspace);
                SetCachedState(BookmarkRepositoryService.ToManagedBookmarks(workspace.Bookmarks), workspace.FolderPaths);
                return _cachedBookmarks;
            }
            finally
            {
                _repositoryGate.Release();
            }
        }

        public async Task<IReadOnlyList<BookmarkMetadata>> LoadMetadataAsync(CancellationToken cancellationToken)
        {
            await _repositoryGate.WaitAsync(cancellationToken);
            try
            {
                string solutionPath = await GetSolutionPathAsync(cancellationToken);
                return await _repositoryService.LoadAsync(solutionPath, cancellationToken);
            }
            finally
            {
                _repositoryGate.Release();
            }
        }

        public async Task SaveMetadataAsync(IEnumerable<BookmarkMetadata> metadata, CancellationToken cancellationToken)
        {
            await _repositoryGate.WaitAsync(cancellationToken);
            try
            {
                string solutionPath = await GetSolutionPathAsync(cancellationToken);
                await _repositoryService.SaveAsync(solutionPath, metadata, cancellationToken);
                BookmarkWorkspaceState workspace = await _repositoryService.LoadWorkspaceAsync(solutionPath, cancellationToken);
                BookmarkRepositoryService.NormalizeWorkspaceState(workspace);
                SetCachedState(BookmarkRepositoryService.ToManagedBookmarks(workspace.Bookmarks), workspace.FolderPaths);
            }
            finally
            {
                _repositoryGate.Release();
            }
        }

        public async Task<IReadOnlyList<ManagedBookmark>> UpdateBookmarksAsync(Action<List<BookmarkMetadata>> updateAction, CancellationToken cancellationToken)
        {
            await _repositoryGate.WaitAsync(cancellationToken);
            try
            {
                string solutionPath = await GetSolutionPathAsync(cancellationToken);
                BookmarkWorkspaceState workspace = await _repositoryService.LoadWorkspaceAsync(solutionPath, cancellationToken);
                updateAction(workspace.Bookmarks);
                BookmarkRepositoryService.NormalizeWorkspaceState(workspace);
                await _repositoryService.SaveWorkspaceAsync(solutionPath, workspace, cancellationToken);
                SetCachedState(BookmarkRepositoryService.ToManagedBookmarks(workspace.Bookmarks), workspace.FolderPaths);
                return _cachedBookmarks;
            }
            finally
            {
                _repositoryGate.Release();
            }
        }

        public async Task<IReadOnlyList<ManagedBookmark>> TryUpdateBookmarksAsync(Func<List<BookmarkMetadata>, bool> updateAction, CancellationToken cancellationToken)
        {
            await _repositoryGate.WaitAsync(cancellationToken);
            try
            {
                string solutionPath = await GetSolutionPathAsync(cancellationToken);
                BookmarkWorkspaceState workspace = await _repositoryService.LoadWorkspaceAsync(solutionPath, cancellationToken);
                if (updateAction(workspace.Bookmarks))
                {
                    BookmarkRepositoryService.NormalizeWorkspaceState(workspace);
                    await _repositoryService.SaveWorkspaceAsync(solutionPath, workspace, cancellationToken);
                }

                SetCachedState(BookmarkRepositoryService.ToManagedBookmarks(workspace.Bookmarks), workspace.FolderPaths);
                return _cachedBookmarks;
            }
            finally
            {
                _repositoryGate.Release();
            }
        }

        public async Task<ManagedBookmark?> ToggleBookmarkAsync(BookmarkSnapshot snapshot, CancellationToken cancellationToken)
            => await ToggleBookmarkAsync(snapshot, label: null, cancellationToken);

        public async Task<ManagedBookmark?> ToggleBookmarkAsync(BookmarkSnapshot snapshot, string? label, CancellationToken cancellationToken)
        {
            if (snapshot is null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            await _repositoryGate.WaitAsync(cancellationToken);
            try
            {
                string solutionPath = await GetSolutionPathAsync(cancellationToken);
                BookmarkWorkspaceState workspace = await _repositoryService.LoadWorkspaceAsync(solutionPath, cancellationToken);

                BookmarkMetadata existingBookmark = BookmarkRepositoryService.FindBySnapshot(workspace.Bookmarks, snapshot);
                ManagedBookmark? result;

                if (existingBookmark is not null)
                {
                    workspace.Bookmarks.Remove(existingBookmark);
                    result = null;
                }
                else
                {
                    BookmarkMetadata createdBookmark = BookmarkRepositoryService.CreateBookmarkMetadata(workspace.Bookmarks, snapshot, label);
                    workspace.Bookmarks.Add(createdBookmark);
                    result = createdBookmark.ToManagedBookmark();
                }

                BookmarkRepositoryService.NormalizeWorkspaceState(workspace);
                await _repositoryService.SaveWorkspaceAsync(solutionPath, workspace, cancellationToken);
                SetCachedState(BookmarkRepositoryService.ToManagedBookmarks(workspace.Bookmarks), workspace.FolderPaths);
                return result;
            }
            finally
            {
                _repositoryGate.Release();
            }
        }

        public void Clear()
        {
            InvalidateSolutionPath();
            SetCachedState(Array.Empty<ManagedBookmark>(), new[] { string.Empty });
        }

        public async Task<BookmarkWorkspaceState> LoadWorkspaceAsync(CancellationToken cancellationToken)
        {
            await _repositoryGate.WaitAsync(cancellationToken);
            try
            {
                string solutionPath = await GetSolutionPathAsync(cancellationToken);
                BookmarkWorkspaceState workspace = await _repositoryService.LoadWorkspaceAsync(solutionPath, cancellationToken);
                BookmarkRepositoryService.NormalizeWorkspaceState(workspace);
                return workspace;
            }
            finally
            {
                _repositoryGate.Release();
            }
        }

        public async Task<IReadOnlyList<ManagedBookmark>> UpdateWorkspaceAsync(Action<BookmarkWorkspaceState> updateAction, CancellationToken cancellationToken)
        {
            await _repositoryGate.WaitAsync(cancellationToken);
            try
            {
                string solutionPath = await GetSolutionPathAsync(cancellationToken);
                BookmarkWorkspaceState workspace = await _repositoryService.LoadWorkspaceAsync(solutionPath, cancellationToken);
                updateAction(workspace);
                BookmarkRepositoryService.NormalizeWorkspaceState(workspace);
                await _repositoryService.SaveWorkspaceAsync(solutionPath, workspace, cancellationToken);
                SetCachedState(BookmarkRepositoryService.ToManagedBookmarks(workspace.Bookmarks), workspace.FolderPaths);
                return _cachedBookmarks;
            }
            finally
            {
                _repositoryGate.Release();
            }
        }

        public async Task<IReadOnlyList<ManagedBookmark>> UpdateWorkspaceAtLocationAsync(
            BookmarkStorageLocation location,
            Action<BookmarkWorkspaceState> updateAction,
            CancellationToken cancellationToken)
        {
            await _repositoryGate.WaitAsync(cancellationToken);
            try
            {
                string solutionPath = await GetSolutionPathAsync(cancellationToken);
                await _metadataStore.UpdateWorkspaceAtLocationAsync(solutionPath, location, updateAction, cancellationToken);

                // Refresh dual state to update cache
                DualBookmarkWorkspaceState dualState = await _metadataStore.LoadDualWorkspaceAsync(solutionPath, cancellationToken);

                List<ManagedBookmark> allBookmarks = new List<ManagedBookmark>();
                allBookmarks.AddRange(BookmarkRepositoryService.ToManagedBookmarks(dualState.PersonalState.Bookmarks));
                allBookmarks.AddRange(BookmarkRepositoryService.ToManagedBookmarks(dualState.SolutionState.Bookmarks));

                HashSet<string> allFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string path in dualState.PersonalState.FolderPaths)
                {
                    allFolders.Add(path);
                }

                foreach (string path in dualState.SolutionState.FolderPaths)
                {
                    allFolders.Add(path);
                }

                SetCachedState(allBookmarks, allFolders);
                return _cachedBookmarks;
            }
            finally
            {
                _repositoryGate.Release();
            }
        }

        public async Task<string> GetStoragePathAsync(CancellationToken cancellationToken)
        {
            await _repositoryGate.WaitAsync(cancellationToken);
            try
            {
                string solutionPath = await GetSolutionPathAsync(cancellationToken);
                return _metadataStore.GetStoragePath(solutionPath);
            }
            finally
            {
                _repositoryGate.Release();
            }
        }

        public async Task<BookmarkStorageInfo> GetStorageInfoAsync(CancellationToken cancellationToken)
        {
            string solutionPath = await GetSolutionPathAsync(cancellationToken);
            return _metadataStore.GetStorageInfo(solutionPath);
        }

        public async Task<BookmarkStorageInfo> MoveToLocationAsync(BookmarkStorageLocation targetLocation, CancellationToken cancellationToken)
        {
            await _repositoryGate.WaitAsync(cancellationToken);
            try
            {
                string solutionPath = await GetSolutionPathAsync(cancellationToken);
                return await _metadataStore.MoveToLocationAsync(solutionPath, targetLocation, cancellationToken);
            }
            finally
            {
                _repositoryGate.Release();
            }
        }

        public async Task<DualBookmarkWorkspaceState> RefreshDualAsync(CancellationToken cancellationToken)
        {
            await _repositoryGate.WaitAsync(cancellationToken);
            try
            {
                string solutionPath = await GetSolutionPathAsync(cancellationToken);
                DualBookmarkWorkspaceState dualState = await _metadataStore.LoadDualWorkspaceAsync(solutionPath, cancellationToken);

                // Combine and cache all bookmarks
                List<ManagedBookmark> allBookmarks = new List<ManagedBookmark>();
                allBookmarks.AddRange(BookmarkRepositoryService.ToManagedBookmarks(dualState.PersonalState.Bookmarks));
                allBookmarks.AddRange(BookmarkRepositoryService.ToManagedBookmarks(dualState.SolutionState.Bookmarks));

                HashSet<string> allFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string path in dualState.PersonalState.FolderPaths)
                {
                    allFolders.Add(path);
                }

                foreach (string path in dualState.SolutionState.FolderPaths)
                {
                    allFolders.Add(path);
                }

                SetCachedState(allBookmarks, allFolders);
                return dualState;
            }
            finally
            {
                _repositoryGate.Release();
            }
        }

        public async Task MoveBookmarkToStorageAsync(string bookmarkId, BookmarkStorageLocation targetLocation, CancellationToken cancellationToken)
        {
            await _repositoryGate.WaitAsync(cancellationToken);
            try
            {
                string solutionPath = await GetSolutionPathAsync(cancellationToken);
                await _metadataStore.MoveBookmarkBetweenLocationsAsync(solutionPath, bookmarkId, targetLocation, cancellationToken);
            }
            finally
            {
                _repositoryGate.Release();
            }
        }

        private async Task<string> GetSolutionPathAsync(CancellationToken cancellationToken)
        {
            // Use cached path if available to avoid UI thread switch
            if (_cachedSolutionPath is not null)
            {
                return _cachedSolutionPath;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            EnvDTE80.DTE2? dte = await VS.GetServiceAsync<EnvDTE.DTE, EnvDTE80.DTE2>();
            _cachedSolutionPath = dte?.Solution?.FullName ?? string.Empty;
            return _cachedSolutionPath;
        }

        private void SetCachedBookmarks(IReadOnlyList<ManagedBookmark> bookmarks)
        {
            _cachedBookmarks = bookmarks ?? Array.Empty<ManagedBookmark>();
            BookmarksChanged?.Invoke(this, EventArgs.Empty);
        }

        private void SetCachedState(IReadOnlyList<ManagedBookmark> bookmarks, IEnumerable<string> folderPaths)
        {
            _cachedBookmarks = bookmarks ?? Array.Empty<ManagedBookmark>();
            _cachedFolderPaths = (folderPaths ?? Array.Empty<string>())
                .Select(BookmarkIdentity.NormalizeFolderPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (!_cachedFolderPaths.Contains(string.Empty, StringComparer.OrdinalIgnoreCase))
            {
                _cachedFolderPaths = _cachedFolderPaths.Concat(new[] { string.Empty }).ToArray();
            }

            BookmarksChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}