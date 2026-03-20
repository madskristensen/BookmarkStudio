namespace BookmarkStudio
{
    [Command(PackageIds.AddBookmarkCommand)]
    internal sealed class AddBookmarkCommand : BookmarkCommandBase<AddBookmarkCommand>
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
