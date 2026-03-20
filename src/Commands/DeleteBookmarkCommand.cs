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
}
