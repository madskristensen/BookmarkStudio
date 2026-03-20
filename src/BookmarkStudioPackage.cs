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
    [ProvideToolWindow(typeof(BookmarkManagerToolWindow.Pane), Style = VsDockStyle.Tabbed, Window = "34e76e81-ee4a-11d0-ae2e-00a0c90fffc3")]
    [ProvideToolWindowVisibility(typeof(BookmarkManagerToolWindow), VSConstants.UICONTEXT.SolutionHasSingleProject_string)]
    [ProvideToolWindowVisibility(typeof(BookmarkManagerToolWindow), VSConstants.UICONTEXT.SolutionHasMultipleProjects_string)]
    //[ProvideOptionPage(typeof(OptionsProvider.GeneralOptions), "Bookmark Studio", "General", 0, 0, true, ProvidesLocalizedCategoryName = false)]
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
        }

        private void OnAfterOpenSolution(Solution? obj)
        {
            JoinableTaskFactory.RunAsync(async () =>
            {
                await BookmarkStudioSession.Current.RefreshAsync(CancellationToken.None);
                await BookmarkManagerToolWindow.RefreshIfVisibleAsync(CancellationToken.None);
            }).FireAndForget();
        }

        private void OnAfterCloseSolution()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            BookmarkStudioSession.Current.Clear();
            BookmarkManagerToolWindow.ClearIfVisible();
        }
    }
}