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

        private BookmarkStudioSession()
        {
            _repositoryService = new BookmarkRepositoryService(_metadataStore);
        }

        public static BookmarkStudioSession Current => _instance.Value;

        public event EventHandler? BookmarksChanged;

        public IReadOnlyList<ManagedBookmark> CachedBookmarks => _cachedBookmarks;

        public IReadOnlyList<string> CachedFolderPaths => _cachedFolderPaths;

        public BookmarkMetadataStore MetadataStore => _metadataStore;

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
                SetCachedBookmarks(await _repositoryService.UpdateAsync(solutionPath, updateAction, cancellationToken));
                BookmarkWorkspaceState workspace = await _repositoryService.LoadWorkspaceAsync(solutionPath, cancellationToken);
                BookmarkRepositoryService.NormalizeWorkspaceState(workspace);
                _cachedFolderPaths = workspace.FolderPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
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
                SetCachedBookmarks(await _repositoryService.TryUpdateAsync(solutionPath, updateAction, cancellationToken));
                BookmarkWorkspaceState workspace = await _repositoryService.LoadWorkspaceAsync(solutionPath, cancellationToken);
                BookmarkRepositoryService.NormalizeWorkspaceState(workspace);
                _cachedFolderPaths = workspace.FolderPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
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
                    await _repositoryGate.WaitAsync(cancellationToken);
                    try
                    {
                        string solutionPath = await GetSolutionPathAsync(cancellationToken);
                        ManagedBookmark? bookmark = await _repositoryService.ToggleAsync(solutionPath, snapshot, label, cancellationToken);
                        BookmarkWorkspaceState workspace = await _repositoryService.LoadWorkspaceAsync(solutionPath, cancellationToken);
                        BookmarkRepositoryService.NormalizeWorkspaceState(workspace);
                        SetCachedState(BookmarkRepositoryService.ToManagedBookmarks(workspace.Bookmarks), workspace.FolderPaths);
                        return bookmark;
                    }
                    finally
                    {
                        _repositoryGate.Release();
                    }
                }

        public void Clear()
            => SetCachedState(Array.Empty<ManagedBookmark>(), new[] { string.Empty });

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
                SetCachedBookmarks(await _repositoryService.UpdateWorkspaceAsync(solutionPath, updateAction, cancellationToken));
                BookmarkWorkspaceState workspace = await _repositoryService.LoadWorkspaceAsync(solutionPath, cancellationToken);
                BookmarkRepositoryService.NormalizeWorkspaceState(workspace);
                _cachedFolderPaths = workspace.FolderPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
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

        private static async Task<string> GetSolutionPathAsync(CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            EnvDTE80.DTE2? dte = await VS.GetServiceAsync<EnvDTE.DTE, EnvDTE80.DTE2>();
            return dte?.Solution?.FullName ?? string.Empty;
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