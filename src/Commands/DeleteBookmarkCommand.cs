namespace BookmarkStudio
{
    [Command(PackageIds.DeleteBookmarkCommand)]
    internal sealed class DeleteBookmarkCommand : BookmarkCommandBase<DeleteBookmarkCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await BookmarkManagerToolWindow.DeleteSelectionAsync(System.Threading.CancellationToken.None);
            await RefreshToolWindowAsync(SelectedBookmarkId);
        }
    }

    [Command(PackageIds.ClearAllBookmarksCommand)]
    internal sealed class ClearAllBookmarksCommand : BookmarkCommandBase<ClearAllBookmarksCommand>
    {
        protected override void BeforeQueryStatus(EventArgs e)
                {
                    ThreadHelper.ThrowIfNotOnUIThread();
                    Command.Visible = BookmarkOperationsService.Current.IsSolutionOrFolderOpen();
                }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await BookmarkOperationsService.Current.ClearAllAsync(System.Threading.CancellationToken.None);
            await RefreshToolWindowAsync();
        }
    }
}
