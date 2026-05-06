using System.Threading;

namespace BookmarkStudio
{
    internal static class BookmarkCommandActions
    {
        public static Task ToggleBookmarkAsync(CancellationToken cancellationToken)
            => ToggleBookmarkAsync(overrideLocation: null, cancellationToken);

        public static async Task ToggleBookmarkAsync(BookmarkStorageLocation? overrideLocation, CancellationToken cancellationToken)
        {
            string? label = null;

            var hasExistingBookmark = await BookmarkOperationsService.Current.HasBookmarkAtCurrentLocationAsync(cancellationToken);

            if (!hasExistingBookmark)
            {
                // Use smart fallback naming (single selected span, classified identifier,
                // word under caret, file name, line text, Bookmark)
                // with numeric suffixing when needed for uniqueness.
                label = await BookmarkOperationsService.Current.GetSuggestedLabelAsync(cancellationToken);

                if (General.Instance.PromptForBookmarkName)
                {
                    label = TextPromptWindow.Show("New Bookmark", "Enter a name for this bookmark:", label, selectTextOnLoad: true);

                    if (label is null)
                    {
                        return;
                    }
                }
            }

            ManagedBookmark? bookmark = await BookmarkOperationsService.Current.ToggleBookmarkAsync(label, overrideLocation, cancellationToken);
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
