global using System;
global using System.Threading.Tasks;
global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio;

namespace BookmarkStudio
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.SolutionExists_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.FolderOpened_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideToolWindow(typeof(BookmarkManagerToolWindow.Pane), Style = VsDockStyle.Tabbed, Window = WindowGuids.SolutionExplorer)]
    [ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), Vsix.Name, "General", 0, 0, true, ProvidesLocalizedCategoryName = false, SupportsProfiles = true)]
    [Guid(PackageGuids.BookmarkStudioString)]
    public sealed class BookmarkStudioPackage : ToolkitPackage
    {
        private bool _initialRefreshDone;

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            this.RegisterToolWindows();
            await BookmarkRefreshMonitorService.Instance.InitializeAsync(cancellationToken);
            await this.RegisterCommandsAsync();
            await BookmarkBuiltInCommandInterceptor.InitializeAsync();

            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            VS.Events.SolutionEvents.OnAfterOpenSolution += OnAfterOpenSolution;
            VS.Events.SolutionEvents.OnAfterCloseSolution += OnAfterCloseSolution;
            VS.Events.SolutionEvents.OnAfterOpenFolder += OnAfterOpenFolder;
            VS.Events.SolutionEvents.OnAfterCloseFolder += OnAfterCloseFolder;

            // Defer initial refresh to run on idle after main thread is available
            // This prevents deadlock when RefreshAsync needs the UI thread
            JoinableTaskFactory.StartOnIdle(async () =>
            {
                if (_initialRefreshDone)
                {
                    return;
                }

                _initialRefreshDone = true;
                await BookmarkStudioSession.Current.RefreshAsync(CancellationToken.None);
            }).FireAndForget();
        }

        private void OnAfterOpenSolution(Solution? obj)
        {
            _initialRefreshDone = true;
            BookmarkStudioSession.Current.InvalidateSolutionPath();
            ThreadHelper.JoinableTaskFactory.StartOnIdle(async () =>
            {
                await BookmarkStudioSession.Current.RefreshAsync(CancellationToken.None);
                await BookmarkManagerToolWindow.RefreshIfVisibleAsync(CancellationToken.None);
            }).FireAndForget();
        }

        private void OnAfterCloseSolution()
        {
            BookmarkStudioSession.Current.InvalidateSolutionPath();
            ThreadHelper.JoinableTaskFactory.StartOnIdle(async () =>
            {
                // Refresh to show only Global bookmarks
                await BookmarkStudioSession.Current.RefreshAsync(CancellationToken.None);
                await BookmarkManagerToolWindow.RefreshIfVisibleAsync(CancellationToken.None);
            }).FireAndForget();
        }

        private void OnAfterOpenFolder(string? folderPath)
        {
            _initialRefreshDone = true;
            BookmarkStudioSession.Current.InvalidateSolutionPath();
            ThreadHelper.JoinableTaskFactory.StartOnIdle(async () =>
            {
                await BookmarkStudioSession.Current.RefreshAsync(CancellationToken.None);
                await BookmarkManagerToolWindow.RefreshIfVisibleAsync(CancellationToken.None);
            }).FireAndForget();
        }

        private void OnAfterCloseFolder(string? folderPath)
        {
            BookmarkStudioSession.Current.InvalidateSolutionPath();
            ThreadHelper.JoinableTaskFactory.StartOnIdle(async () =>
            {
                // Refresh to show only Global bookmarks
                await BookmarkStudioSession.Current.RefreshAsync(CancellationToken.None);
                await BookmarkManagerToolWindow.RefreshIfVisibleAsync(CancellationToken.None);
            }).FireAndForget();
        }
    }
}