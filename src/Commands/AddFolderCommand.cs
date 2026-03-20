namespace BookmarkStudio
{
    [Command(PackageIds.AddFolderCommand)]
    internal sealed class AddFolderCommand : BookmarkCommandBase<AddFolderCommand>
    {
        protected override void BeforeQueryStatus(EventArgs e)
            => Command.Visible = BookmarkOperationsService.Current.IsSolutionOrFolderOpen();

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
            => await BookmarkManagerToolWindow.PromptCreateFolderAsync();
    }
}
