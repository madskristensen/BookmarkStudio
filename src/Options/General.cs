using System.ComponentModel;
using System.Runtime.InteropServices;

namespace BookmarkStudio
{
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
        [Description("Determines where new bookmarks are stored by default. 'Workspace' stores bookmarks in the solution/repository folder for team sharing. 'Personal' stores bookmarks in the '.vs' folder.")]
        [DefaultValue(BookmarkStorageLocation.Personal)]
        [TypeConverter(typeof(EnumConverter))]
        public BookmarkStorageLocation DefaultStorageLocation { get; set; } = BookmarkStorageLocation.Personal;

        [Category("Commands")]
        [DisplayName("Intercept old commands")]
        [Description("When enabled, the built-in Visual Studio bookmark commands (Ctrl+K,K etc.) will use Bookmark Studio instead.")]
        [DefaultValue(true)]
        public bool InterceptBuiltInCommands { get; set; } = true;
    }
}
