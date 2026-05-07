namespace BookmarkStudio
{
    [Command(PackageIds.ToggleAutoAssignShortcutCommand)]
    internal sealed class ToggleAutoAssignShortcutCommand : BaseCommand<ToggleAutoAssignShortcutCommand>
    {
        protected override Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            General.Instance.AutoAssignShortcutNumber = !General.Instance.AutoAssignShortcutNumber;
            General.Instance.Save();
            return Task.CompletedTask;
        }

        protected override void BeforeQueryStatus(EventArgs e)
        {
            Command.Checked = General.Instance.AutoAssignShortcutNumber;
        }
    }
}
