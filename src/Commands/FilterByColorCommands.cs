namespace BookmarkStudio
{
    internal abstract class FilterByColorCommandBase<TCommand> : BaseCommand<TCommand>
        where TCommand : class, new()
    {
        /// <summary>
        /// Gets the color to filter by, or null to show all colors.
        /// </summary>
        protected abstract BookmarkColor? FilterColor { get; }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            BookmarkManagerControl? control = await BookmarkManagerToolWindow.GetControlAsync();
            if (control is null)
            {
                return;
            }

            control.ViewModel.FilterColor = FilterColor;

            var message = FilterColor.HasValue
                ? string.Concat("Filtering by ", FilterColor.Value.ToString(), ".")
                : "Showing all colors.";

            control.ViewModel.SetStatus(message);
        }
    }

    [Command(PackageIds.FilterByColorAllCommand)]
    internal sealed class FilterByColorAllCommand : FilterByColorCommandBase<FilterByColorAllCommand>
    {
        protected override BookmarkColor? FilterColor => null;
    }

    [Command(PackageIds.FilterByColorBlueCommand)]
    internal sealed class FilterByColorBlueCommand : FilterByColorCommandBase<FilterByColorBlueCommand>
    {
        protected override BookmarkColor? FilterColor => BookmarkColor.Blue;
    }

    [Command(PackageIds.FilterByColorRedCommand)]
    internal sealed class FilterByColorRedCommand : FilterByColorCommandBase<FilterByColorRedCommand>
    {
        protected override BookmarkColor? FilterColor => BookmarkColor.Red;
    }

    [Command(PackageIds.FilterByColorOrangeCommand)]
    internal sealed class FilterByColorOrangeCommand : FilterByColorCommandBase<FilterByColorOrangeCommand>
    {
        protected override BookmarkColor? FilterColor => BookmarkColor.Orange;
    }

    [Command(PackageIds.FilterByColorYellowCommand)]
    internal sealed class FilterByColorYellowCommand : FilterByColorCommandBase<FilterByColorYellowCommand>
    {
        protected override BookmarkColor? FilterColor => BookmarkColor.Yellow;
    }

    [Command(PackageIds.FilterByColorGreenCommand)]
    internal sealed class FilterByColorGreenCommand : FilterByColorCommandBase<FilterByColorGreenCommand>
    {
        protected override BookmarkColor? FilterColor => BookmarkColor.Green;
    }

    [Command(PackageIds.FilterByColorPurpleCommand)]
    internal sealed class FilterByColorPurpleCommand : FilterByColorCommandBase<FilterByColorPurpleCommand>
    {
        protected override BookmarkColor? FilterColor => BookmarkColor.Purple;
    }

    [Command(PackageIds.FilterByColorPinkCommand)]
    internal sealed class FilterByColorPinkCommand : FilterByColorCommandBase<FilterByColorPinkCommand>
    {
        protected override BookmarkColor? FilterColor => BookmarkColor.Pink;
    }

    [Command(PackageIds.FilterByColorTealCommand)]
    internal sealed class FilterByColorTealCommand : FilterByColorCommandBase<FilterByColorTealCommand>
    {
        protected override BookmarkColor? FilterColor => BookmarkColor.Teal;
    }
}
