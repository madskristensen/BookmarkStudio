using System.Threading;

namespace BookmarkStudio
{
    internal abstract class BookmarkCommandBase<TCommand> : BaseCommand<TCommand>
        where TCommand : class, new()
    {
        protected static string? SelectedBookmarkId
            => BookmarkManagerToolWindow.GetSelectedBookmarkId();

        protected static async Task RefreshToolWindowAsync(string? bookmarkId = null)
            => await BookmarkManagerToolWindow.RefreshAsync(bookmarkId, CancellationToken.None);
    }
}
