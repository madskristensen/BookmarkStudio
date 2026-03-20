using System.ComponentModel;

namespace BookmarkStudio
{
    //internal partial class OptionsProvider
    //{
    //    [ComVisible(true)]
    //    public class GeneralOptions : BaseOptionPage<General> { }
    //}

    internal class General : BaseOptionModel<General>
    {
        [Category("Bookmarks")]
        [DisplayName("Prompt for bookmark name")]
        [Description("When enabled, a dialog will prompt for a name when creating a new bookmark.")]
        [DefaultValue(false)]
        public bool PromptForBookmarkName { get; set; }
    }
}
