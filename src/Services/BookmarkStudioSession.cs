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

        public Task<IReadOnlyList<ManagedBookmark>> RefreshAsync(CancellationToken cancellationToken)
            => RefreshAsync(removeStaleBookmarks: false, cancellationToken);

        public async Task<(IReadOnlyList<ManagedBookmark> Bookmarks, int StaleCount)> RefreshAndCleanupAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<ManagedBookmark> bookmarks = await RefreshAsync(removeStaleBookmarks: true, cancellationToken);
            return (bookmarks, _lastStaleBookmarkCount);
        }

        private int _lastStaleBookmarkCount;

        private async Task<IReadOnlyList<ManagedBookmark>> RefreshAsync(bool removeStaleBookmarks, CancellationToken cancellationToken)
        {
            // Use RefreshDualAsync to load from both Personal and Solution storage locations
            (DualBookmarkWorkspaceState _, int staleCount) = await RefreshDualAsync(removeStaleBookmarks, cancellationToken);
            _lastStaleBookmarkCount = staleCount;
            return _cachedBookmarks;
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
                DualBookmarkWorkspaceState dualState = await _metadataStore.LoadDualWorkspaceAsync(solutionPath, cancellationToken);

                // Try to apply update to each workspace - catch exceptions if bookmark not found in that workspace
                bool personalChanged = TryApplyUpdate(dualState.PersonalState.Bookmarks, updateAction);
                bool solutionChanged = TryApplyUpdate(dualState.SolutionState.Bookmarks, updateAction);

                // Save changed workspaces
                if (personalChanged)
                {
                    BookmarkRepositoryService.NormalizeWorkspaceState(dualState.PersonalState);
                    await _metadataStore.SaveWorkspaceToLocationAsync(solutionPath, BookmarkStorageLocation.Personal, dualState.PersonalState, cancellationToken);
                }

                if (solutionChanged)
                {
                    BookmarkRepositoryService.NormalizeWorkspaceState(dualState.SolutionState);
                    await _metadataStore.SaveWorkspaceToLocationAsync(solutionPath, BookmarkStorageLocation.Solution, dualState.SolutionState, cancellationToken);
                }

                // Combine and cache all bookmarks from both locations
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

        private static bool TryApplyUpdate(List<BookmarkMetadata> bookmarks, Action<List<BookmarkMetadata>> updateAction)
        {
            try
            {
                updateAction(bookmarks);
                return true;
            }
            catch (InvalidOperationException)
            {
                // Bookmark not found in this list - that's expected when using dual storage
                return false;
            }
        }

        public async Task<IReadOnlyList<ManagedBookmark>> TryUpdateBookmarksAsync(Func<List<BookmarkMetadata>, bool> updateAction, CancellationToken cancellationToken)
        {
            await _repositoryGate.WaitAsync(cancellationToken);
            try
            {
                string solutionPath = await GetSolutionPathAsync(cancellationToken);
                DualBookmarkWorkspaceState dualState = await _metadataStore.LoadDualWorkspaceAsync(solutionPath, cancellationToken);

                // Try to apply update to both workspaces
                bool personalChanged = updateAction(dualState.PersonalState.Bookmarks);
                bool solutionChanged = updateAction(dualState.SolutionState.Bookmarks);

                if (personalChanged)
                {
                    BookmarkRepositoryService.NormalizeWorkspaceState(dualState.PersonalState);
                    await _metadataStore.SaveWorkspaceToLocationAsync(solutionPath, BookmarkStorageLocation.Personal, dualState.PersonalState, cancellationToken);
                }

                if (solutionChanged)
                {
                    BookmarkRepositoryService.NormalizeWorkspaceState(dualState.SolutionState);
                    await _metadataStore.SaveWorkspaceToLocationAsync(solutionPath, BookmarkStorageLocation.Solution, dualState.SolutionState, cancellationToken);
                }

                // Combine and cache all bookmarks from both locations
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
                DualBookmarkWorkspaceState dualState = await _metadataStore.LoadDualWorkspaceAsync(solutionPath, cancellationToken);

                // Check both locations for existing bookmark
                BookmarkMetadata existingPersonal = BookmarkRepositoryService.FindBySnapshot(dualState.PersonalState.Bookmarks, snapshot);
                BookmarkMetadata existingSolution = BookmarkRepositoryService.FindBySnapshot(dualState.SolutionState.Bookmarks, snapshot);
                ManagedBookmark? result;
                bool personalChanged = false;
                bool solutionChanged = false;

                if (existingPersonal is not null)
                {
                    dualState.PersonalState.Bookmarks.Remove(existingPersonal);
                    personalChanged = true;
                    result = null;
                }
                else if (existingSolution is not null)
                {
                    dualState.SolutionState.Bookmarks.Remove(existingSolution);
                    solutionChanged = true;
                    result = null;
                }
                else
                {
                    // Create new bookmark - combine all bookmarks to find next available slot/label
                    List<BookmarkMetadata> allBookmarks = new List<BookmarkMetadata>();
                    allBookmarks.AddRange(dualState.PersonalState.Bookmarks);
                    allBookmarks.AddRange(dualState.SolutionState.Bookmarks);

                    BookmarkMetadata createdBookmark = BookmarkRepositoryService.CreateBookmarkMetadata(allBookmarks, snapshot, label);

                    // New bookmarks go to Solution storage by default (team-shared)
                    createdBookmark.StorageLocation = BookmarkStorageLocation.Solution;
                    dualState.SolutionState.Bookmarks.Add(createdBookmark);
                    solutionChanged = true;
                    result = createdBookmark.ToManagedBookmark();
                }

                // Save changed workspaces
                if (personalChanged)
                {
                    BookmarkRepositoryService.NormalizeWorkspaceState(dualState.PersonalState);
                    await _metadataStore.SaveWorkspaceToLocationAsync(solutionPath, BookmarkStorageLocation.Personal, dualState.PersonalState, cancellationToken);
                }

                if (solutionChanged)
                {
                    BookmarkRepositoryService.NormalizeWorkspaceState(dualState.SolutionState);
                    await _metadataStore.SaveWorkspaceToLocationAsync(solutionPath, BookmarkStorageLocation.Solution, dualState.SolutionState, cancellationToken);
                }

                // Combine and cache all bookmarks from both locations
                List<ManagedBookmark> allManagedBookmarks = new List<ManagedBookmark>();
                allManagedBookmarks.AddRange(BookmarkRepositoryService.ToManagedBookmarks(dualState.PersonalState.Bookmarks));
                allManagedBookmarks.AddRange(BookmarkRepositoryService.ToManagedBookmarks(dualState.SolutionState.Bookmarks));

                HashSet<string> allFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string path in dualState.PersonalState.FolderPaths)
                {
                    allFolders.Add(path);
                }

                foreach (string path in dualState.SolutionState.FolderPaths)
                {
                    allFolders.Add(path);
                }

                SetCachedState(allManagedBookmarks, allFolders);
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

        /// <summary>
        /// Removes bookmarks that reference files which no longer exist.
        /// </summary>
        /// <returns>The number of stale bookmarks that were removed.</returns>
        public async Task<int> RemoveStaleBookmarksAsync(CancellationToken cancellationToken)
        {
            await _repositoryGate.WaitAsync(cancellationToken);
            try
            {
                string solutionPath = await GetSolutionPathAsync(cancellationToken);
                if (string.IsNullOrEmpty(solutionPath))
                {
                    return 0;
                }

                BookmarkWorkspaceState workspace = await _repositoryService.LoadWorkspaceAsync(solutionPath, cancellationToken);

                List<BookmarkMetadata> staleBookmarks = workspace.Bookmarks
                    .Where(bookmark => !string.IsNullOrWhiteSpace(bookmark.DocumentPath) && !System.IO.File.Exists(bookmark.DocumentPath))
                    .ToList();

                if (staleBookmarks.Count == 0)
                {
                    return 0;
                }

                foreach (BookmarkMetadata stale in staleBookmarks)
                {
                    workspace.Bookmarks.Remove(stale);
                }

                BookmarkRepositoryService.NormalizeWorkspaceState(workspace);
                await _repositoryService.SaveWorkspaceAsync(solutionPath, workspace, cancellationToken);
                SetCachedState(BookmarkRepositoryService.ToManagedBookmarks(workspace.Bookmarks), workspace.FolderPaths);

                return staleBookmarks.Count;
            }
            finally
            {
                _repositoryGate.Release();
            }
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

        /// <summary>
        /// Updates both personal and solution workspaces if the update function returns true.
        /// </summary>
        public async Task<bool> TryUpdateDualWorkspaceAsync(
            Func<BookmarkWorkspaceState, BookmarkWorkspaceState, bool> updateAction,
            CancellationToken cancellationToken)
        {
            await _repositoryGate.WaitAsync(cancellationToken);
            try
            {
                string solutionPath = await GetSolutionPathAsync(cancellationToken);
                if (string.IsNullOrEmpty(solutionPath))
                {
                    return false;
                }

                DualBookmarkWorkspaceState dualState = await _metadataStore.LoadDualWorkspaceAsync(solutionPath, cancellationToken);

                if (!updateAction(dualState.PersonalState, dualState.SolutionState))
                {
                    return false;
                }

                // Save both workspaces
                BookmarkRepositoryService.NormalizeWorkspaceState(dualState.PersonalState);
                BookmarkRepositoryService.NormalizeWorkspaceState(dualState.SolutionState);
                await _metadataStore.SaveWorkspaceToLocationAsync(solutionPath, BookmarkStorageLocation.Personal, dualState.PersonalState, cancellationToken);
                await _metadataStore.SaveWorkspaceToLocationAsync(solutionPath, BookmarkStorageLocation.Solution, dualState.SolutionState, cancellationToken);

                // Update cache
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
                return true;
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
            (DualBookmarkWorkspaceState dualState, _) = await RefreshDualAsync(removeStaleBookmarks: false, cancellationToken);
            return dualState;
        }

        public async Task<(DualBookmarkWorkspaceState State, int StaleCount)> RefreshDualAndCleanupAsync(CancellationToken cancellationToken)
            => await RefreshDualAsync(removeStaleBookmarks: true, cancellationToken);

        private async Task<(DualBookmarkWorkspaceState State, int StaleCount)> RefreshDualAsync(bool removeStaleBookmarks, CancellationToken cancellationToken)
        {
            await _repositoryGate.WaitAsync(cancellationToken);
            try
            {
                string solutionPath = await GetSolutionPathAsync(cancellationToken);
                DualBookmarkWorkspaceState dualState = await _metadataStore.LoadDualWorkspaceAsync(solutionPath, cancellationToken);

                int staleCount = 0;
                if (removeStaleBookmarks && !string.IsNullOrEmpty(solutionPath))
                {
                    staleCount = await RemoveStaleFromDualStateAsync(solutionPath, dualState, cancellationToken);
                }

                // Resolve slot conflicts - workspace bookmarks take precedence
                ResolveSlotConflicts(dualState);

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
                return (dualState, staleCount);
            }
            finally
            {
                _repositoryGate.Release();
            }
        }

        private async Task<int> RemoveStaleFromDualStateAsync(string solutionPath, DualBookmarkWorkspaceState dualState, CancellationToken cancellationToken)
        {
            int totalRemoved = 0;

            // Remove stale from personal bookmarks
            List<BookmarkMetadata> stalePersonal = dualState.PersonalState.Bookmarks
                .Where(bookmark => !string.IsNullOrWhiteSpace(bookmark.DocumentPath) && !System.IO.File.Exists(bookmark.DocumentPath))
                .ToList();

            if (stalePersonal.Count > 0)
            {
                foreach (BookmarkMetadata stale in stalePersonal)
                {
                    dualState.PersonalState.Bookmarks.Remove(stale);
                }

                totalRemoved += stalePersonal.Count;
                BookmarkRepositoryService.NormalizeWorkspaceState(dualState.PersonalState);
                await _metadataStore.SaveWorkspaceToLocationAsync(solutionPath, BookmarkStorageLocation.Personal, dualState.PersonalState, cancellationToken);
            }

            // Remove stale from solution bookmarks
            List<BookmarkMetadata> staleSolution = dualState.SolutionState.Bookmarks
                .Where(bookmark => !string.IsNullOrWhiteSpace(bookmark.DocumentPath) && !System.IO.File.Exists(bookmark.DocumentPath))
                .ToList();

            if (staleSolution.Count > 0)
            {
                foreach (BookmarkMetadata stale in staleSolution)
                {
                    dualState.SolutionState.Bookmarks.Remove(stale);
                }

                totalRemoved += staleSolution.Count;
                BookmarkRepositoryService.NormalizeWorkspaceState(dualState.SolutionState);
                await _metadataStore.SaveWorkspaceToLocationAsync(solutionPath, BookmarkStorageLocation.Solution, dualState.SolutionState, cancellationToken);
            }

            return totalRemoved;
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

        public async Task MoveBookmarkToStorageAsync(string bookmarkId, string targetFolderPath, BookmarkStorageLocation targetLocation, CancellationToken cancellationToken)
        {
            await _repositoryGate.WaitAsync(cancellationToken);
            try
            {
                string solutionPath = await GetSolutionPathAsync(cancellationToken);
                await _metadataStore.MoveBookmarkBetweenLocationsAsync(solutionPath, bookmarkId, targetFolderPath, targetLocation, cancellationToken);
            }
            finally
            {
                _repositoryGate.Release();
            }
        }

        public async Task<IReadOnlyList<ManagedBookmark>> MoveFolderBetweenStorageAsync(
            string sourceFolderPath,
            BookmarkStorageLocation sourceLocation,
            string targetFolderPath,
            BookmarkStorageLocation targetLocation,
            CancellationToken cancellationToken)
        {
            await _repositoryGate.WaitAsync(cancellationToken);
            try
            {
                string solutionPath = await GetSolutionPathAsync(cancellationToken);
                await _metadataStore.MoveFolderBetweenLocationsAsync(
                    solutionPath,
                    sourceFolderPath,
                    sourceLocation,
                    targetFolderPath,
                    targetLocation,
                    cancellationToken);

                // Refresh dual state to update cache
                DualBookmarkWorkspaceState dualState = await _metadataStore.LoadDualWorkspaceAsync(solutionPath, cancellationToken);

                // Resolve slot conflicts - workspace bookmarks take precedence
                ResolveSlotConflicts(dualState);

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

        private async Task<string> GetSolutionPathAsync(CancellationToken cancellationToken)
        {
            // Use cached path if available to avoid UI thread switch
            if (_cachedSolutionPath is not null)
            {
                return _cachedSolutionPath;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            EnvDTE80.DTE2? dte = await VS.GetServiceAsync<EnvDTE.DTE, EnvDTE80.DTE2>();
            string? solutionFullName = dte?.Solution?.FullName;

            // If a solution is open, use its path
            if (!string.IsNullOrEmpty(solutionFullName))
            {
                _cachedSolutionPath = solutionFullName;
                return _cachedSolutionPath;
            }

            // Check for Open Folder mode - get the solution directory which works for both solution and folder modes
            Microsoft.VisualStudio.Shell.Interop.IVsSolution? solution = await VS.GetServiceAsync<Microsoft.VisualStudio.Shell.Interop.SVsSolution, Microsoft.VisualStudio.Shell.Interop.IVsSolution>();
            if (solution is not null &&
                solution.GetProperty((int)Microsoft.VisualStudio.Shell.Interop.__VSPROPID.VSPROPID_SolutionDirectory, out object directoryObj) == Microsoft.VisualStudio.VSConstants.S_OK &&
                directoryObj is string directory &&
                !string.IsNullOrEmpty(directory))
            {
                // In Open Folder mode, use the directory path as the "solution path"
                // This allows bookmark storage to work based on the folder location
                _cachedSolutionPath = directory.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                return _cachedSolutionPath;
            }

            _cachedSolutionPath = string.Empty;
            return _cachedSolutionPath;
        }

        /// <summary>
        /// Clears slot numbers from personal bookmarks that conflict with workspace bookmarks.
        /// Workspace bookmarks take precedence when there are duplicate slot assignments.
        /// </summary>
        private static void ResolveSlotConflicts(DualBookmarkWorkspaceState dualState)
        {
            HashSet<int> workspaceSlots = new HashSet<int>(
                dualState.SolutionState.Bookmarks
                    .Where(b => b.SlotNumber.HasValue)
                    .Select(b => b.SlotNumber.GetValueOrDefault()));

            foreach (BookmarkMetadata personalBookmark in dualState.PersonalState.Bookmarks)
            {
                if (personalBookmark.SlotNumber.HasValue && workspaceSlots.Contains(personalBookmark.SlotNumber.Value))
                {
                    personalBookmark.SlotNumber = null;
                }
            }
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