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
    [ProvideToolWindow(typeof(BookmarkManagerToolWindow.Pane), DockedHeight = 500, DocumentLikeTool = false, Orientation = ToolWindowOrientation.Bottom, Style = VsDockStyle.Linked, Window = WindowGuids.SolutionExplorer)]
    [ProvideToolWindowVisibility(typeof(BookmarkManagerToolWindow.Pane), VSConstants.UICONTEXT.SolutionHasSingleProject_string)]
    [ProvideToolWindowVisibility(typeof(BookmarkManagerToolWindow.Pane), VSConstants.UICONTEXT.SolutionHasMultipleProjects_string)]
    [ProvideToolWindowVisibility(typeof(BookmarkManagerToolWindow.Pane), VSConstants.UICONTEXT.Debugging_string)]
    [ProvideToolWindowVisibility(typeof(BookmarkManagerToolWindow.Pane), VSConstants.UICONTEXT.EmptySolution_string)]
    [ProvideToolWindowVisibility(typeof(BookmarkManagerToolWindow.Pane), VSConstants.UICONTEXT.FolderOpened_string)]
    //[ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), "Bookmark Studio", "General", 0, 0, true, ProvidesLocalizedCategoryName = false)]
    [ProvideProfile(typeof(OptionsProvider.GeneralOptions), Vsix.Name, "General", 0, 0, false)]
    [Guid(PackageGuids.BookmarkStudioString)]
    public sealed class BookmarkStudioPackage : ToolkitPackage
    {
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            this.RegisterToolWindows();
            await BookmarkStudioSession.Current.RefreshAsync(cancellationToken);
            await BookmarkRefreshMonitorService.Instance.InitializeAsync(cancellationToken);
            await this.RegisterCommandsAsync();
            await BookmarkBuiltInCommandInterceptor.InitializeAsync();

            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            VS.Events.SolutionEvents.OnAfterOpenSolution += OnAfterOpenSolution;
            VS.Events.SolutionEvents.OnAfterCloseSolution += OnAfterCloseSolution;
            VS.Events.SolutionEvents.OnAfterOpenFolder += OnAfterOpenFolder;
            VS.Events.SolutionEvents.OnAfterCloseFolder += OnAfterCloseFolder;
        }

        private void OnAfterOpenSolution(Solution? obj)
        {
            BookmarkStudioSession.Current.InvalidateSolutionPath();
            ThreadHelper.JoinableTaskFactory.StartOnIdle(async () =>
            {
                await BookmarkStudioSession.Current.RefreshAsync(CancellationToken.None);
                await BookmarkManagerToolWindow.RefreshIfVisibleAsync(CancellationToken.None);
            }).FireAndForget();
        }

        private void OnAfterCloseSolution()
        {
            ThreadHelper.JoinableTaskFactory.StartOnIdle(() =>
            {
                BookmarkStudioSession.Current.Clear();
                BookmarkManagerToolWindow.ClearIfVisible();
            }).FireAndForget();
        }

        private void OnAfterOpenFolder(string? folderPath)
        {
            BookmarkStudioSession.Current.InvalidateSolutionPath();
            ThreadHelper.JoinableTaskFactory.StartOnIdle(async () =>
            {
                await BookmarkStudioSession.Current.RefreshAsync(CancellationToken.None);
                await BookmarkManagerToolWindow.RefreshIfVisibleAsync(CancellationToken.None);
            }).FireAndForget();
        }

        private void OnAfterCloseFolder(string? folderPath)
        {
            ThreadHelper.JoinableTaskFactory.StartOnIdle(() =>
            {
                BookmarkStudioSession.Current.Clear();
                BookmarkManagerToolWindow.ClearIfVisible();
            }).FireAndForget();
        }
    }
}