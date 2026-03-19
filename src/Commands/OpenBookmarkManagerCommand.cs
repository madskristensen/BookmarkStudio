namespace BookmarkStudio
{
    [Command(PackageIds.OpenBookmarkManagerCommand)]
    internal sealed class OpenBookmarkManagerCommand : BaseCommand<OpenBookmarkManagerCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
            => await BookmarkManagerToolWindow.ShowAndRefreshAsync(System.Threading.CancellationToken.None);
    }
}
