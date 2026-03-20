using System;
using System.Collections.Generic;
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

        private BookmarkStudioSession()
        {
            _repositoryService = new BookmarkRepositoryService(_metadataStore);
        }

        public static BookmarkStudioSession Current => _instance.Value;

        public event EventHandler? BookmarksChanged;

        public IReadOnlyList<ManagedBookmark> CachedBookmarks => _cachedBookmarks;

        public BookmarkMetadataStore MetadataStore => _metadataStore;

        public async Task<IReadOnlyList<ManagedBookmark>> RefreshAsync(CancellationToken cancellationToken)
        {
            await _repositoryGate.WaitAsync(cancellationToken);
            try
            {
                string solutionPath = await GetSolutionPathAsync(cancellationToken);
                SetCachedBookmarks(await _repositoryService.ListAsync(solutionPath, cancellationToken));
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
                SetCachedBookmarks(BookmarkRepositoryService.ToManagedBookmarks(metadata));
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
                return _cachedBookmarks;
            }
            finally
            {
                _repositoryGate.Release();
            }
        }

        public async Task<ManagedBookmark?> ToggleBookmarkAsync(BookmarkSnapshot snapshot, CancellationToken cancellationToken)
        {
            await _repositoryGate.WaitAsync(cancellationToken);
            try
            {
                string solutionPath = await GetSolutionPathAsync(cancellationToken);
                ManagedBookmark? bookmark = await _repositoryService.ToggleAsync(solutionPath, snapshot, cancellationToken);
                SetCachedBookmarks(await _repositoryService.ListAsync(solutionPath, cancellationToken));
                return bookmark;
            }
            finally
            {
                _repositoryGate.Release();
            }
        }

        public void Clear()
            => SetCachedBookmarks(Array.Empty<ManagedBookmark>());

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
    }
}