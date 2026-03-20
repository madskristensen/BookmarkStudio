namespace BookmarkStudio
{
    [Command(PackageIds.DeleteBookmarkCommand)]
    internal sealed class DeleteBookmarkCommand : BookmarkCommandBase<DeleteBookmarkCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            string? bookmarkId = SelectedBookmarkId;
            if (string.IsNullOrWhiteSpace(bookmarkId))
            {
                return;
            }

            await BookmarkOperationsService.Current.RemoveBookmarkAsync(bookmarkId, System.Threading.CancellationToken.None);
            await RefreshToolWindowAsync();
        }
    }
}
