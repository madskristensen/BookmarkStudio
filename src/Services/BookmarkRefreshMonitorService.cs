using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace BookmarkStudio
{
    /// <summary>
    /// Monitors file operations (delete, rename, move) within Visual Studio and updates bookmarks accordingly.
    /// </summary>
    internal sealed class BookmarkRefreshMonitorService : IVsTrackProjectDocumentsEvents2, IDisposable
    {
        private readonly BookmarkStudioSession _session = BookmarkStudioSession.Current;
        private uint _eventsCookie;
        private IVsTrackProjectDocuments2? _trackDocuments;
        private bool _isDisposed;

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            _trackDocuments = await VS.GetServiceAsync<SVsTrackProjectDocuments, IVsTrackProjectDocuments2>();
            if (_trackDocuments is not null)
            {
                _trackDocuments.AdviseTrackProjectDocumentsEvents(this, out _eventsCookie);
            }
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            if (_trackDocuments is not null && _eventsCookie != 0)
            {
                ThreadHelper.JoinableTaskFactory.Run(async () =>
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                    _trackDocuments.UnadviseTrackProjectDocumentsEvents(_eventsCookie);
                });
            }

            _eventsCookie = 0;
            _trackDocuments = null;
        }

        #region IVsTrackProjectDocumentsEvents2 - File Removal

        public int OnAfterRemoveFiles(int cProjects, int cFiles, IVsProject[] rgpProjects, int[] rgFirstIndices, string[] rgpszMkDocuments, VSREMOVEFILEFLAGS[] rgFlags)
        {
            if (rgpszMkDocuments is null || rgpszMkDocuments.Length == 0)
            {
                return VSConstants.S_OK;
            }

            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    await RemoveBookmarksForFilesAsync(rgpszMkDocuments, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    await ex.LogAsync();
                }
            }).FireAndForget();

            return VSConstants.S_OK;
        }

        public int OnQueryRemoveFiles(IVsProject pProject, int cFiles, string[] rgpszMkDocuments, VSQUERYREMOVEFILEFLAGS[] rgFlags, VSQUERYREMOVEFILERESULTS[] pSummaryResult, VSQUERYREMOVEFILERESULTS[] rgResults)
            => VSConstants.S_OK;

        #endregion

        #region IVsTrackProjectDocumentsEvents2 - File Rename/Move

        public int OnAfterRenameFiles(int cProjects, int cFiles, IVsProject[] rgpProjects, int[] rgFirstIndices, string[] rgszMkOldNames, string[] rgszMkNewNames, VSRENAMEFILEFLAGS[] rgFlags)
        {
            if (rgszMkOldNames is null || rgszMkNewNames is null || rgszMkOldNames.Length == 0)
            {
                return VSConstants.S_OK;
            }

            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                try
                {
                    await UpdateBookmarkPathsAsync(rgszMkOldNames, rgszMkNewNames, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    await ex.LogAsync();
                }
            }).FireAndForget();

            return VSConstants.S_OK;
        }

        public int OnQueryRenameFiles(IVsProject pProject, int cFiles, string[] rgszMkOldNames, string[] rgszMkNewNames, VSQUERYRENAMEFILEFLAGS[] rgFlags, VSQUERYRENAMEFILERESULTS[] pSummaryResult, VSQUERYRENAMEFILERESULTS[] rgResults)
            => VSConstants.S_OK;

        #endregion

        #region IVsTrackProjectDocumentsEvents2 - Directory operations (not implemented)

        public int OnAfterAddDirectoriesEx(int cProjects, int cDirectories, IVsProject[] rgpProjects, int[] rgFirstIndices, string[] rgpszMkDocuments, VSADDDIRECTORYFLAGS[] rgFlags)
            => VSConstants.S_OK;

        public int OnAfterAddFilesEx(int cProjects, int cFiles, IVsProject[] rgpProjects, int[] rgFirstIndices, string[] rgpszMkDocuments, VSADDFILEFLAGS[] rgFlags)
            => VSConstants.S_OK;

        public int OnAfterRemoveDirectories(int cProjects, int cDirectories, IVsProject[] rgpProjects, int[] rgFirstIndices, string[] rgpszMkDocuments, VSREMOVEDIRECTORYFLAGS[] rgFlags)
            => VSConstants.S_OK;

        public int OnAfterRenameDirectories(int cProjects, int cDirs, IVsProject[] rgpProjects, int[] rgFirstIndices, string[] rgszMkOldNames, string[] rgszMkNewNames, VSRENAMEDIRECTORYFLAGS[] rgFlags)
            => VSConstants.S_OK;

        public int OnAfterSccStatusChanged(int cProjects, int cFiles, IVsProject[] rgpProjects, int[] rgFirstIndices, string[] rgpszMkDocuments, uint[] rgdwSccStatus)
            => VSConstants.S_OK;

        public int OnQueryAddDirectories(IVsProject pProject, int cDirectories, string[] rgpszMkDocuments, VSQUERYADDDIRECTORYFLAGS[] rgFlags, VSQUERYADDDIRECTORYRESULTS[] pSummaryResult, VSQUERYADDDIRECTORYRESULTS[] rgResults)
            => VSConstants.S_OK;

        public int OnQueryAddFiles(IVsProject pProject, int cFiles, string[] rgpszMkDocuments, VSQUERYADDFILEFLAGS[] rgFlags, VSQUERYADDFILERESULTS[] pSummaryResult, VSQUERYADDFILERESULTS[] rgResults)
            => VSConstants.S_OK;

        public int OnQueryRemoveDirectories(IVsProject pProject, int cDirectories, string[] rgpszMkDocuments, VSQUERYREMOVEDIRECTORYFLAGS[] rgFlags, VSQUERYREMOVEDIRECTORYRESULTS[] pSummaryResult, VSQUERYREMOVEDIRECTORYRESULTS[] rgResults)
            => VSConstants.S_OK;

        public int OnQueryRenameDirectories(IVsProject pProject, int cDirs, string[] rgszMkOldNames, string[] rgszMkNewNames, VSQUERYRENAMEDIRECTORYFLAGS[] rgFlags, VSQUERYRENAMEDIRECTORYRESULTS[] pSummaryResult, VSQUERYRENAMEDIRECTORYRESULTS[] rgResults)
            => VSConstants.S_OK;

        #endregion

        #region Bookmark update helpers

        private async Task RemoveBookmarksForFilesAsync(string[] filePaths, CancellationToken cancellationToken)
        {
            if (filePaths.Length == 0)
            {
                return;
            }

            var normalizedPaths = new HashSet<string>(
                filePaths.Select(BookmarkIdentity.NormalizeDocumentPath),
                StringComparer.Ordinal);

            await _session.TryUpdateDualWorkspaceAsync((personal, solution) =>
            {
                var changed = false;

                List<BookmarkMetadata> personalToRemove = [.. personal.Bookmarks.Where(b => normalizedPaths.Contains(BookmarkIdentity.NormalizeDocumentPath(b.DocumentPath)))];

                if (personalToRemove.Count > 0)
                {
                    foreach (BookmarkMetadata bookmark in personalToRemove)
                    {
                        personal.Bookmarks.Remove(bookmark);
                    }

                    changed = true;
                }

                List<BookmarkMetadata> solutionToRemove = [.. solution.Bookmarks.Where(b => normalizedPaths.Contains(BookmarkIdentity.NormalizeDocumentPath(b.DocumentPath)))];

                if (solutionToRemove.Count > 0)
                {
                    foreach (BookmarkMetadata bookmark in solutionToRemove)
                    {
                        solution.Bookmarks.Remove(bookmark);
                    }

                    changed = true;
                }

                return changed;
            }, cancellationToken);

            // Refresh the tool window if visible
            await BookmarkManagerToolWindow.RefreshIfVisibleAsync(cancellationToken);
        }

        private async Task UpdateBookmarkPathsAsync(string[] oldPaths, string[] newPaths, CancellationToken cancellationToken)
        {
            if (oldPaths.Length == 0 || oldPaths.Length != newPaths.Length)
            {
                return;
            }

            // Build a mapping of old paths to new paths
            var pathMapping = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < oldPaths.Length; i++)
            {
                var normalizedOld = BookmarkIdentity.NormalizeDocumentPath(oldPaths[i]);
                if (!string.IsNullOrEmpty(normalizedOld))
                {
                    pathMapping[normalizedOld] = newPaths[i];
                }
            }

            await _session.TryUpdateDualWorkspaceAsync((personal, solution) =>
            {
                var changed = false;

                foreach (BookmarkMetadata bookmark in personal.Bookmarks)
                {
                    var normalizedPath = BookmarkIdentity.NormalizeDocumentPath(bookmark.DocumentPath);
                    if (pathMapping.TryGetValue(normalizedPath, out var newPath))
                    {
                        bookmark.DocumentPath = newPath;
                        changed = true;
                    }
                }

                foreach (BookmarkMetadata bookmark in solution.Bookmarks)
                {
                    var normalizedPath = BookmarkIdentity.NormalizeDocumentPath(bookmark.DocumentPath);
                    if (pathMapping.TryGetValue(normalizedPath, out var newPath))
                    {
                        bookmark.DocumentPath = newPath;
                        changed = true;
                    }
                }

                return changed;
            }, cancellationToken);

            // Refresh the tool window if visible
            await BookmarkManagerToolWindow.RefreshIfVisibleAsync(cancellationToken);
        }

        #endregion

        private static class BookmarkRefreshMonitorServiceHolder
        {
            internal static readonly BookmarkRefreshMonitorService Instance = new();
        }

        internal static BookmarkRefreshMonitorService Instance => BookmarkRefreshMonitorServiceHolder.Instance;
    }
}