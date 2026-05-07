namespace BookmarkStudio
{
    internal abstract class GroupByCommandBase<TCommand> : BaseCommand<TCommand>
        where TCommand : class, new()
    {
        protected abstract BookmarkGroupMode GroupMode { get; }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            BookmarkManagerControl? control = await BookmarkManagerToolWindow.GetControlAsync();
            if (control is null)
            {
                return;
            }

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            control.ViewModel.GroupMode = GroupMode;
            General.Instance.GroupMode = GroupMode;
            await General.Instance.SaveAsync();
            control.ViewModel.SetStatus(string.Concat("Grouping by ", GroupMode.ToString(), "."));
        }

        protected override void BeforeQueryStatus(EventArgs e)
        {
            try
            {
                Command.Checked = General.Instance.GroupMode == GroupMode;
            }
            catch
            {
                Command.Checked = GroupMode == BookmarkGroupMode.Folders;
            }
        }
    }

    [Command(PackageIds.GroupByFoldersCommand)]
    internal sealed class GroupByFoldersCommand : GroupByCommandBase<GroupByFoldersCommand>
    {
        protected override BookmarkGroupMode GroupMode => BookmarkGroupMode.Folders;
    }

    [Command(PackageIds.GroupByColorCommand)]
    internal sealed class GroupByColorCommand : GroupByCommandBase<GroupByColorCommand>
    {
        protected override BookmarkGroupMode GroupMode => BookmarkGroupMode.Color;
    }

    [Command(PackageIds.GroupByFileCommand)]
    internal sealed class GroupByFileCommand : GroupByCommandBase<GroupByFileCommand>
    {
        protected override BookmarkGroupMode GroupMode => BookmarkGroupMode.File;
    }
}
