namespace BookmarkStudio
{
    [Command(PackageIds.GoToNextBookmarkCommand)]
    internal sealed class GoToNextBookmarkCommand : BookmarkCommandBase<GoToNextBookmarkCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
            => await BookmarkCommandActions.GoToNextBookmarkAsync(System.Threading.CancellationToken.None);
    }
}
