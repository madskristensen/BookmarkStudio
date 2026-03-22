using System.Globalization;
using System.Threading;

namespace BookmarkStudio
{
    internal abstract class GoToShortcutCommandBase<TCommand> : BaseCommand<TCommand>
        where TCommand : class, new()
    {
        /// <summary>
        /// Gets the 1-based shortcut number this command navigates to.
        /// </summary>
        protected abstract int ShortcutNumber { get; }

        protected override void BeforeQueryStatus(EventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                ManagedBookmark bookmark = await BookmarkOperationsService.Current.GetBookmarkByShortcutAsync(ShortcutNumber, CancellationToken.None);

                Command.Visible = bookmark is not null;
                Command.Enabled = bookmark is not null;
                if (bookmark is not null)
                {
                    Command.Text = string.Format(CultureInfo.CurrentCulture, "{0}", bookmark.Label);
                }
            });
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            try
            {
                ManagedBookmark bookmark = await BookmarkOperationsService.Current.GoToShortcutAsync(ShortcutNumber, CancellationToken.None);
                await BookmarkManagerToolWindow.RefreshIfVisibleAsync(bookmark.BookmarkId, CancellationToken.None);
            }
            catch (InvalidOperationException ex)
            {
                await ex.LogAsync();
                await VS.StatusBar.ShowMessageAsync(ex.Message);
            }
        }
    }

    [Command(PackageIds.GoToShortcut1Command)]
    internal sealed class GoToShortcut1Command : GoToShortcutCommandBase<GoToShortcut1Command>
    {
        protected override int ShortcutNumber => 1;
    }

    [Command(PackageIds.GoToShortcut2Command)]
    internal sealed class GoToShortcut2Command : GoToShortcutCommandBase<GoToShortcut2Command>
    {
        protected override int ShortcutNumber => 2;
    }

    [Command(PackageIds.GoToShortcut3Command)]
    internal sealed class GoToShortcut3Command : GoToShortcutCommandBase<GoToShortcut3Command>
    {
        protected override int ShortcutNumber => 3;
    }

    [Command(PackageIds.GoToShortcut4Command)]
    internal sealed class GoToShortcut4Command : GoToShortcutCommandBase<GoToShortcut4Command>
    {
        protected override int ShortcutNumber => 4;
    }

    [Command(PackageIds.GoToShortcut5Command)]
    internal sealed class GoToShortcut5Command : GoToShortcutCommandBase<GoToShortcut5Command>
    {
        protected override int ShortcutNumber => 5;
    }

    [Command(PackageIds.GoToShortcut6Command)]
    internal sealed class GoToShortcut6Command : GoToShortcutCommandBase<GoToShortcut6Command>
    {
        protected override int ShortcutNumber => 6;
    }

    [Command(PackageIds.GoToShortcut7Command)]
    internal sealed class GoToShortcut7Command : GoToShortcutCommandBase<GoToShortcut7Command>
    {
        protected override int ShortcutNumber => 7;
    }

    [Command(PackageIds.GoToShortcut8Command)]
    internal sealed class GoToShortcut8Command : GoToShortcutCommandBase<GoToShortcut8Command>
    {
        protected override int ShortcutNumber => 8;
    }

    [Command(PackageIds.GoToShortcut9Command)]
    internal sealed class GoToShortcut9Command : GoToShortcutCommandBase<GoToShortcut9Command>
    {
        protected override int ShortcutNumber => 9;
    }
}
