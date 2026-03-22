using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell.Interop;

namespace BookmarkStudio
{
    internal sealed class BookmarkManagerToolWindow : BaseToolWindow<BookmarkManagerToolWindow>
    {
        private static BookmarkManagerControl? _currentControl;

        public override string GetTitle(int toolWindowId)
            => "Bookmark Manager";

        public override Type PaneType
            => typeof(Pane);

        public override async Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
        {
            // Create control on UI thread - it will load data itself via InitializeAsync
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            _currentControl = new BookmarkManagerControl();
            return _currentControl;
        }

        internal static string? GetSelectedBookmarkId()
            => _currentControl?.SelectedBookmarkId;

        internal static string? GetSelectedFolderPath()
            => _currentControl?.SelectedFolderPath;

        internal static async Task<BookmarkManagerControl?> GetControlAsync()
        {
            if (_currentControl is null)
            {
                await ShowAsync();
            }

            return _currentControl;
        }

        internal static void ClearIfVisible()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _currentControl?.ViewModel.Clear();
        }

        internal static async Task ShowAndRefreshAsync(CancellationToken cancellationToken)
        {
            await ShowAsync();
            await RefreshAsync(null, cancellationToken);
        }

        internal static async Task RefreshIfVisibleAsync(CancellationToken cancellationToken)
            => await RefreshIfVisibleAsync(null, cancellationToken);

        internal static async Task RefreshIfVisibleAsync(string? bookmarkId, CancellationToken cancellationToken)
        {
            if (_currentControl is null)
            {
                return;
            }

            // Refresh from the session's cached data without reloading from disk.
            // The session cache should already be up-to-date from the preceding RefreshAsync call.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            _currentControl.RefreshFromCache();
            _currentControl.SelectBookmark(bookmarkId);
        }

        internal static async Task PromptCreateFolderAsync()
        {
            if (_currentControl is null)
            {
                await ShowAsync();
            }

            if (_currentControl is null)
            {
                return;
            }

            await _currentControl.PromptCreateFolderAsync();
        }

        internal static async Task DeleteSelectionAsync(CancellationToken cancellationToken)
        {
            if (_currentControl is null)
            {
                await ShowAsync();
            }

            if (_currentControl is null)
            {
                return;
            }

            await _currentControl.DeleteSelectedAsync(cancellationToken);
        }

        internal static async Task RefreshAsync(string? bookmarkId, CancellationToken cancellationToken)
        {
            if (_currentControl is null)
            {
                await ShowAsync();
            }

            if (_currentControl is null)
            {
                return;
            }

            await _currentControl.RefreshAsync(cancellationToken);
            _currentControl.SelectBookmark(bookmarkId);
        }

        [Guid("62892db9-8c0f-4458-995c-b317a5c3dd78")]
        internal sealed class Pane : ToolWindowPane
        {
            public Pane()
            {
                BitmapImageMoniker = KnownMonikers.StatusInformation;
                ToolBar = new CommandID(PackageGuids.BookmarkStudio, PackageIds.BookmarkManagerToolbar);
                ToolBarLocation = (int)VSTWT_LOCATION.VSTWT_TOP;
            }

            public override bool SearchEnabled => true;

            public override IVsSearchTask CreateSearch(uint dwCookie, IVsSearchQuery pSearchQuery, IVsSearchCallback pSearchCallback)
            {
                if (_currentControl is null || pSearchQuery is null || pSearchCallback is null)
                {
                    return null!;
                }

                return new BookmarkManagerSearchTask(dwCookie, pSearchQuery, pSearchCallback, _currentControl.ViewModel);
            }

            public override void ClearSearch()
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                _currentControl?.ViewModel.ClearSearch();
            }
        }

        private sealed class BookmarkManagerSearchTask : VsSearchTask
        {
            private readonly BookmarkManagerViewModel _viewModel;

            public BookmarkManagerSearchTask(uint cookie, IVsSearchQuery searchQuery, IVsSearchCallback searchCallback, BookmarkManagerViewModel viewModel)
                : base(cookie, searchQuery, searchCallback)
            {
                _viewModel = viewModel;
            }

            protected override void OnStartSearch()
            {
                ErrorCode = VSConstants.S_OK;
                SearchResults = 0;

                try
                {
                    if (ThreadHelper.CheckAccess())
                    {
                        ApplySearchResults();
                    }
                    else
                    {
                        ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
                        {
                            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                            ApplySearchResults();
                        }).FireAndForget();
                    }
                }
                catch (Exception ex)
                {
                    ex.Log();
                    ErrorCode = VSConstants.E_FAIL;
                }

                base.OnStartSearch();
            }

            private void ApplySearchResults()
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                uint resultCount = (uint)_viewModel.ApplySearchText(SearchQuery.SearchString);
                SearchResults = resultCount;
                _viewModel.SetStatus(string.Concat(resultCount.ToString(System.Globalization.CultureInfo.InvariantCulture), " matching bookmarks."));
            }

            protected override void OnStopSearch()
            {
                SearchResults = 0;
                base.OnStopSearch();
            }
        }
    }
}