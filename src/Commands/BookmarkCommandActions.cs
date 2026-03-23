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
                var hasExistingBookmark = await BookmarkOperationsService.Current.HasBookmarkAtCurrentLocationAsync(cancellationToken);

                if (!hasExistingBookmark)
                {
                    // Use selected text as suggestion if available, otherwise try to extract a meaningful
                    // identifier from the current line, falling back to default "Bookmark N" naming
                    var selectedText = await BookmarkOperationsService.Current.GetSelectedTextAsync(cancellationToken);
                    var defaultLabel = selectedText ?? await BookmarkOperationsService.Current.GetSuggestedLabelAsync(cancellationToken);

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

        public static async Task GoToNextBookmarkInDocumentAsync(CancellationToken cancellationToken)
        {
            ManagedBookmark bookmark = await BookmarkOperationsService.Current.GoToNextBookmarkInDocumentAsync(cancellationToken);
            await BookmarkManagerToolWindow.RefreshIfVisibleAsync(bookmark.BookmarkId, cancellationToken);
        }

        public static async Task GoToPreviousBookmarkInDocumentAsync(CancellationToken cancellationToken)
        {
            ManagedBookmark bookmark = await BookmarkOperationsService.Current.GoToPreviousBookmarkInDocumentAsync(cancellationToken);
            await BookmarkManagerToolWindow.RefreshIfVisibleAsync(bookmark.BookmarkId, cancellationToken);
        }

        public static async Task ClearBookmarksInDocumentAsync(CancellationToken cancellationToken)
        {
            await BookmarkOperationsService.Current.ClearBookmarksInDocumentAsync(cancellationToken);
            await BookmarkManagerToolWindow.RefreshIfVisibleAsync(cancellationToken);
        }
    }
}
