using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace BookmarkStudio
{
    /// <summary>
    /// Format definition names for overview mark (scrollbar marker) colors.
    /// These must match the Name attribute on each format definition class.
    /// </summary>
    internal static class BookmarkOverviewMarkFormatNames
    {
        public const string Blue = "BookmarkStudio.OverviewMark.Blue";
        public const string Red = "BookmarkStudio.OverviewMark.Red";
        public const string Orange = "BookmarkStudio.OverviewMark.Orange";
        public const string Yellow = "BookmarkStudio.OverviewMark.Yellow";
        public const string Green = "BookmarkStudio.OverviewMark.Green";
        public const string Purple = "BookmarkStudio.OverviewMark.Purple";
        public const string Pink = "BookmarkStudio.OverviewMark.Pink";
        public const string Teal = "BookmarkStudio.OverviewMark.Teal";

        public static string GetFormatName(BookmarkColor color)
        {
            return color switch
            {
                BookmarkColor.Red => Red,
                BookmarkColor.Orange => Orange,
                BookmarkColor.Yellow => Yellow,
                BookmarkColor.Green => Green,
                BookmarkColor.Purple => Purple,
                BookmarkColor.Pink => Pink,
                BookmarkColor.Teal => Teal,
                _ => Blue,
            };
        }
    }

    #region Overview Mark Format Definitions

    [Export(typeof(EditorFormatDefinition))]
    [Name(BookmarkOverviewMarkFormatNames.Blue)]
    [UserVisible(true)]
    internal sealed class BlueBookmarkOverviewMarkFormatDefinition : EditorFormatDefinition
    {
        public BlueBookmarkOverviewMarkFormatDefinition()
        {
            DisplayName = "Bookmark Scrollbar Mark - Blue";
            BackgroundColor = Color.FromRgb(0x1E, 0x88, 0xE5);
            ForegroundColor = Color.FromRgb(0x1E, 0x88, 0xE5);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name(BookmarkOverviewMarkFormatNames.Red)]
    [UserVisible(true)]
    internal sealed class RedBookmarkOverviewMarkFormatDefinition : EditorFormatDefinition
    {
        public RedBookmarkOverviewMarkFormatDefinition()
        {
            DisplayName = "Bookmark Scrollbar Mark - Red";
            BackgroundColor = Color.FromRgb(0xE5, 0x39, 0x35);
            ForegroundColor = Color.FromRgb(0xE5, 0x39, 0x35);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name(BookmarkOverviewMarkFormatNames.Orange)]
    [UserVisible(true)]
    internal sealed class OrangeBookmarkOverviewMarkFormatDefinition : EditorFormatDefinition
    {
        public OrangeBookmarkOverviewMarkFormatDefinition()
        {
            DisplayName = "Bookmark Scrollbar Mark - Orange";
            BackgroundColor = Color.FromRgb(0xFB, 0x8C, 0x00);
            ForegroundColor = Color.FromRgb(0xFB, 0x8C, 0x00);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name(BookmarkOverviewMarkFormatNames.Yellow)]
    [UserVisible(true)]
    internal sealed class YellowBookmarkOverviewMarkFormatDefinition : EditorFormatDefinition
    {
        public YellowBookmarkOverviewMarkFormatDefinition()
        {
            DisplayName = "Bookmark Scrollbar Mark - Yellow";
            BackgroundColor = Color.FromRgb(0xFD, 0xD8, 0x35);
            ForegroundColor = Color.FromRgb(0xFD, 0xD8, 0x35);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name(BookmarkOverviewMarkFormatNames.Green)]
    [UserVisible(true)]
    internal sealed class GreenBookmarkOverviewMarkFormatDefinition : EditorFormatDefinition
    {
        public GreenBookmarkOverviewMarkFormatDefinition()
        {
            DisplayName = "Bookmark Scrollbar Mark - Green";
            BackgroundColor = Color.FromRgb(0x43, 0xA0, 0x47);
            ForegroundColor = Color.FromRgb(0x43, 0xA0, 0x47);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name(BookmarkOverviewMarkFormatNames.Purple)]
    [UserVisible(true)]
    internal sealed class PurpleBookmarkOverviewMarkFormatDefinition : EditorFormatDefinition
    {
        public PurpleBookmarkOverviewMarkFormatDefinition()
        {
            DisplayName = "Bookmark Scrollbar Mark - Purple";
            BackgroundColor = Color.FromRgb(0x8E, 0x24, 0xAA);
            ForegroundColor = Color.FromRgb(0x8E, 0x24, 0xAA);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name(BookmarkOverviewMarkFormatNames.Pink)]
    [UserVisible(true)]
    internal sealed class PinkBookmarkOverviewMarkFormatDefinition : EditorFormatDefinition
    {
        public PinkBookmarkOverviewMarkFormatDefinition()
        {
            DisplayName = "Bookmark Scrollbar Mark - Pink";
            BackgroundColor = Color.FromRgb(0xEC, 0x40, 0x7A);
            ForegroundColor = Color.FromRgb(0xEC, 0x40, 0x7A);
        }
    }

    [Export(typeof(EditorFormatDefinition))]
    [Name(BookmarkOverviewMarkFormatNames.Teal)]
    [UserVisible(true)]
    internal sealed class TealBookmarkOverviewMarkFormatDefinition : EditorFormatDefinition
    {
        public TealBookmarkOverviewMarkFormatDefinition()
        {
            DisplayName = "Bookmark Scrollbar Mark - Teal";
            BackgroundColor = Color.FromRgb(0x00, 0x89, 0x7B);
            ForegroundColor = Color.FromRgb(0x00, 0x89, 0x7B);
        }
    }

    #endregion
}
