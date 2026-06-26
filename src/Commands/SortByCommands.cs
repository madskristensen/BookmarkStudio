namespace BookmarkStudio
{
    internal abstract class SortByCommandBase<TCommand> : BaseCommand<TCommand>
        where TCommand : class, new()
    {
        protected abstract BookmarkSortMode SortMode { get; }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            BookmarkManagerControl? control = await BookmarkManagerToolWindow.GetControlAsync();
            if (control is null)
            {
                return;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            control.ViewModel.SortMode = SortMode;
            General.Instance.SortMode = SortMode;
            await General.Instance.SaveAsync();
            control.ViewModel.SetStatus(string.Concat("Sorting by ", SortMode.ToString(), "."));
        }

        protected override void BeforeQueryStatus(EventArgs e)
        {
            try
            {
                Command.Checked = General.Instance.SortMode == SortMode;
            }
            catch
            {
                Command.Checked = SortMode == BookmarkSortMode.LineNumber;
            }
        }
    }

    [Command(PackageIds.SortByLineNumberCommand)]
    internal sealed class SortByLineNumberCommand : SortByCommandBase<SortByLineNumberCommand>
    {
        protected override BookmarkSortMode SortMode => BookmarkSortMode.LineNumber;
    }

    [Command(PackageIds.SortByAlphabeticalCommand)]
    internal sealed class SortByAlphabeticalCommand : SortByCommandBase<SortByAlphabeticalCommand>
    {
        protected override BookmarkSortMode SortMode => BookmarkSortMode.Alphabetical;
    }

    [Command(PackageIds.SortBySlotCommand)]
    internal sealed class SortBySlotCommand : SortByCommandBase<SortBySlotCommand>
    {
        protected override BookmarkSortMode SortMode => BookmarkSortMode.Slot;
    }

    [Command(PackageIds.SortByCreatedCommand)]
    internal sealed class SortByCreatedCommand : SortByCommandBase<SortByCreatedCommand>
    {
        protected override BookmarkSortMode SortMode => BookmarkSortMode.Created;
    }

    [Command(PackageIds.SortByManualCommand)]
    internal sealed class SortByManualCommand : SortByCommandBase<SortByManualCommand>
    {
        protected override BookmarkSortMode SortMode => BookmarkSortMode.Manual;
    }
}
