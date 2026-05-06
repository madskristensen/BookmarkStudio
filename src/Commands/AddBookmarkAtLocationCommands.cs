namespace BookmarkStudio
{
    internal abstract class AddBookmarkAtLocationCommandBase<TCommand> : BookmarkCommandBase<TCommand>
        where TCommand : class, new()
    {
        protected abstract BookmarkStorageLocation TargetLocation { get; }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
            => await BookmarkCommandActions.ToggleBookmarkAsync(TargetLocation, System.Threading.CancellationToken.None);

        protected override void BeforeQueryStatus(EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var canToggle = BookmarkOperationsService.Current.CanToggleBookmarkInActiveDocument();

            // Personal and Workspace storage are scoped to the current solution/folder; hide them when none is open.
            if (canToggle && TargetLocation != BookmarkStorageLocation.Global)
            {
                canToggle = BookmarkOperationsService.Current.IsSolutionOrFolderOpen();
            }

            Command.Enabled = canToggle;
        }
    }

    [Command(PackageIds.AddGlobalBookmarkCommand)]
    internal sealed class AddGlobalBookmarkCommand : AddBookmarkAtLocationCommandBase<AddGlobalBookmarkCommand>
    {
        protected override BookmarkStorageLocation TargetLocation => BookmarkStorageLocation.Global;
    }

    [Command(PackageIds.AddPersonalBookmarkCommand)]
    internal sealed class AddPersonalBookmarkCommand : AddBookmarkAtLocationCommandBase<AddPersonalBookmarkCommand>
    {
        protected override BookmarkStorageLocation TargetLocation => BookmarkStorageLocation.Personal;
    }

    [Command(PackageIds.AddWorkspaceBookmarkCommand)]
    internal sealed class AddWorkspaceBookmarkCommand : AddBookmarkAtLocationCommandBase<AddWorkspaceBookmarkCommand>
    {
        protected override BookmarkStorageLocation TargetLocation => BookmarkStorageLocation.Workspace;
    }
}
