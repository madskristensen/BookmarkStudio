using System.Threading;

namespace BookmarkStudio
{
    internal static class BookmarkCommandActions
    {
        public static async Task ToggleBookmarkAsync(CancellationToken cancellationToken)
        {
            string? label = null;

            if (General.Instance.PromptForBookmarkName)
            {
                bool hasExistingBookmark = await BookmarkOperationsService.Current.HasBookmarkAtCurrentLocationAsync(cancellationToken);

                if (!hasExistingBookmark)
                {
                    string defaultLabel = await BookmarkOperationsService.Current.GetNextDefaultLabelAsync(cancellationToken);
                    label = TextPromptWindow.Show("New Bookmark", "Enter a name for this bookmark:", defaultLabel, selectTextOnLoad: true);

                    if (label is null)
                    {
                        return;
                    }
                }
            }

            ManagedBookmark? bookmark = await BookmarkOperationsService.Current.ToggleBookmarkAsync(label, cancellationToken);
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
