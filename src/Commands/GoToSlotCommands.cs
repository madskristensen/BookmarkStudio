using System.Globalization;
using System.Threading;

namespace BookmarkStudio
{
    internal abstract class GoToSlotCommandBase<TCommand> : BaseCommand<TCommand>
        where TCommand : class, new()
    {
        /// <summary>
        /// Gets the 1-based slot number this command navigates to.
        /// </summary>
        protected abstract int SlotNumber { get; }

        protected override void BeforeQueryStatus(EventArgs e)
        {
            ThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                ManagedBookmark bookmark = await BookmarkOperationsService.Current.GetBookmarkBySlotAsync(SlotNumber, CancellationToken.None);

                Command.Visible = bookmark is not null;
                Command.Enabled = bookmark is not null;
                if (bookmark is not null)
                {
                    Command.Text = string.Format(CultureInfo.CurrentCulture, "Go to {0}", bookmark.Label);
                }
            });
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            try
            {
                ManagedBookmark bookmark = await BookmarkOperationsService.Current.GoToSlotAsync(SlotNumber, CancellationToken.None);
                await BookmarkManagerToolWindow.RefreshIfVisibleAsync(bookmark.BookmarkId, CancellationToken.None);
            }
            catch (InvalidOperationException ex)
            {
                await ex.LogAsync();
                await VS.StatusBar.ShowMessageAsync(ex.Message);
            }
        }
    }

    [Command(PackageIds.GoToSlot1Command)]
    internal sealed class GoToSlot1Command : GoToSlotCommandBase<GoToSlot1Command>
    {
        protected override int SlotNumber => 1;
    }

    [Command(PackageIds.GoToSlot2Command)]
    internal sealed class GoToSlot2Command : GoToSlotCommandBase<GoToSlot2Command>
    {
        protected override int SlotNumber => 2;
    }

    [Command(PackageIds.GoToSlot3Command)]
    internal sealed class GoToSlot3Command : GoToSlotCommandBase<GoToSlot3Command>
    {
        protected override int SlotNumber => 3;
    }

    [Command(PackageIds.GoToSlot4Command)]
    internal sealed class GoToSlot4Command : GoToSlotCommandBase<GoToSlot4Command>
    {
        protected override int SlotNumber => 4;
    }

    [Command(PackageIds.GoToSlot5Command)]
    internal sealed class GoToSlot5Command : GoToSlotCommandBase<GoToSlot5Command>
    {
        protected override int SlotNumber => 5;
    }

    [Command(PackageIds.GoToSlot6Command)]
    internal sealed class GoToSlot6Command : GoToSlotCommandBase<GoToSlot6Command>
    {
        protected override int SlotNumber => 6;
    }

    [Command(PackageIds.GoToSlot7Command)]
    internal sealed class GoToSlot7Command : GoToSlotCommandBase<GoToSlot7Command>
    {
        protected override int SlotNumber => 7;
    }

    [Command(PackageIds.GoToSlot8Command)]
    internal sealed class GoToSlot8Command : GoToSlotCommandBase<GoToSlot8Command>
    {
        protected override int SlotNumber => 8;
    }

    [Command(PackageIds.GoToSlot9Command)]
    internal sealed class GoToSlot9Command : GoToSlotCommandBase<GoToSlot9Command>
    {
        protected override int SlotNumber => 9;
    }
}
