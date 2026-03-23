using System.ComponentModel;
using System.Runtime.InteropServices;

namespace BookmarkStudio
{
    public enum CommandInterceptionMode
    {
        Ask,
        Yes,
        No,
    }

    internal partial class OptionsProvider
    {
        [ComVisible(true)]
        public class GeneralOptions : BaseOptionPage<General> { }
    }

    internal class General : BaseOptionModel<General>
    {
        [Category("Bookmarks")]
        [DisplayName("Prompt for bookmark name")]
        [Description("When enabled, a dialog will prompt for a name when creating a new bookmark.")]
        [DefaultValue(false)]
        public bool PromptForBookmarkName { get; set; }

        [Category("Bookmarks")]
        [DisplayName("Default storage location")]
        [Description("Determines where new bookmarks are stored by default. 'Global' stores bookmarks in %userprofile%\\.bookmarks.json (persists across solutions). 'Workspace' stores bookmarks in the solution/repository folder for team sharing. 'Personal' stores bookmarks in the '.vs' folder.")]
        [DefaultValue(BookmarkStorageLocation.Personal)]
        [TypeConverter(typeof(EnumConverter))]
        public BookmarkStorageLocation DefaultStorageLocation { get; set; } = BookmarkStorageLocation.Personal;

        [Category("Commands")]
        [DisplayName("Intercept built-in bookmark commands")]
        [Description("Controls whether the built-in Visual Studio bookmark commands (Ctrl+K,K etc.) use Bookmark Studio. 'Ask' prompts on first use, 'Yes' always intercepts, 'No' never intercepts.")]
        [DefaultValue(CommandInterceptionMode.Ask)]
        [TypeConverter(typeof(EnumConverter))]
        public CommandInterceptionMode InterceptBuiltInCommands { get; set; } = CommandInterceptionMode.Ask;
    }
}
