using System.Threading;

namespace BookmarkStudio
{
    internal static class BookmarkCommandActions
    {
        public static async Task ToggleBookmarkAsync(CancellationToken cancellationToken)
        {
            ManagedBookmark? bookmark = await BookmarkOperationsService.Current.ToggleBookmarkAsync(cancellationToken);
            await BookmarkManagerToolWindow.RefreshIfVisibleAsync(bookmark?.BookmarkId, cancellationToken);
        }

        public static async Task GoToNextBookmarkAsync(CancellationToken cancellationToken)
        {
            ManagedBookmark bookmark = await BookmarkOperationsService.Current.GoToNextBookmarkAsync(cancellationToken);
            await BookmarkManagerToolWindow.RefreshIfVisibleAsync(bookmark.BookmarkId, cancellationToken);
        }

        public static async Task GoToPreviousBookmarkAsync(CancellationToken cancellationToken)
        {
            ManagedBookmark bookmark = await BookmarkOperationsService.Current.GoToPreviousBookmarkAsync(cancellationToken);
            await BookmarkManagerToolWindow.RefreshIfVisibleAsync(bookmark.BookmarkId, cancellationToken);
        }
    }
}
