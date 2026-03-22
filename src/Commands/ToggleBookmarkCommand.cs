namespace BookmarkStudio
{
    [Command(PackageIds.ToggleBookmarkCommand)]
    internal sealed class ToggleBookmarkCommand : BookmarkCommandBase<ToggleBookmarkCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
            => await BookmarkCommandActions.ToggleBookmarkAsync(System.Threading.CancellationToken.None);

        protected override void BeforeQueryStatus(EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Command.Enabled = BookmarkOperationsService.Current.CanToggleBookmarkInActiveDocument();
        }
    }
}
