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

        public override Task<FrameworkElement> CreateAsync(int toolWindowId, CancellationToken cancellationToken)
        {
            _currentControl = new BookmarkManagerControl();
            return Task.FromResult<FrameworkElement>(_currentControl);
        }

        internal static string? GetSelectedBookmarkId()
            => _currentControl?.SelectedBookmarkId;

        internal static void ClearIfVisible()
            => _currentControl?.ViewModel.Clear();

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

            await _currentControl.RefreshAsync(cancellationToken);
            _currentControl.SelectBookmark(bookmarkId);
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

        [Guid("d7033ba0-93cb-4b8e-b1d5-4eeef728e53a")]
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
                    return null;
                }

                return new BookmarkManagerSearchTask(dwCookie, pSearchQuery, pSearchCallback, _currentControl.ViewModel);
            }

            public override void ClearSearch()
                => _currentControl?.ViewModel.ClearSearch();
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
                uint resultCount = 0;
                ErrorCode = VSConstants.S_OK;

                try
                {
                    ThreadHelper.JoinableTaskFactory.Run(async delegate
                    {
                        await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                        resultCount = (uint)_viewModel.ApplySearchText(SearchQuery.SearchString);
                        _viewModel.SetStatus(string.Concat(resultCount.ToString(System.Globalization.CultureInfo.InvariantCulture), " matching bookmarks."));
                    });
                }
                catch (Exception ex)
                {
                    ex.Log();
                    ErrorCode = VSConstants.E_FAIL;
                }
                finally
                {
                    SearchResults = resultCount;
                }

                base.OnStartSearch();
            }

            protected override void OnStopSearch()
            {
                SearchResults = 0;
                base.OnStopSearch();
            }
        }
    }
}