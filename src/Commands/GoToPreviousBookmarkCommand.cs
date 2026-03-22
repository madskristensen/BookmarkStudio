namespace BookmarkStudio
{
    [Command(PackageIds.GoToPreviousBookmarkCommand)]
    internal sealed class GoToPreviousBookmarkCommand : BookmarkCommandBase<GoToPreviousBookmarkCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
            => await BookmarkCommandActions.GoToPreviousBookmarkAsync(System.Threading.CancellationToken.None);
    }
}
