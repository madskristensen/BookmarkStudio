namespace BookmarkStudio
{
    [Command(PackageIds.TogglePromptForNameCommand)]
    internal sealed class TogglePromptForNameCommand : BaseCommand<TogglePromptForNameCommand>
    {
        protected override Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            General.Instance.PromptForBookmarkName = !General.Instance.PromptForBookmarkName;
            General.Instance.Save();
            return Task.CompletedTask;
        }

        protected override void BeforeQueryStatus(EventArgs e)
        {
            Command.Checked = General.Instance.PromptForBookmarkName;
        }
    }
}
