namespace BookmarkStudio
{
    [Command(PackageIds.RefreshBookmarkManagerCommand)]
    internal sealed class RefreshBookmarkManagerCommand : BookmarkCommandBase<RefreshBookmarkManagerCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
            => await RefreshToolWindowAsync();
    }
}
