using System;
using System.IO;

namespace BookmarkStudio
{
    internal enum BookmarkColor
    {
        None = 0,
        Red = 1,
        Orange = 2,
        Yellow = 3,
        Green = 4,
        Blue = 5,
        Purple = 6,
        Pink = 7,
        Teal = 8,
    }

    internal static class BookmarkIdentity
    {
        public static string NormalizeDocumentPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant();
        }

        public static string NormalizeLineText(string lineText)
            => (lineText ?? string.Empty).Trim();

        public static string CreateExactMatchKey(string documentPath, int lineNumber)
            => string.Concat(NormalizeDocumentPath(documentPath), "|", lineNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));

    }

    internal sealed class BookmarkSnapshot
    {
        public string DocumentPath { get; set; } = string.Empty;

        public int LineNumber { get; set; }

        public int ColumnNumber { get; set; }

        public string LineText { get; set; } = string.Empty;

        public string ExactMatchKey => BookmarkIdentity.CreateExactMatchKey(DocumentPath, LineNumber);

    }

    internal sealed class BookmarkMetadata
    {
        public string BookmarkId { get; set; } = Guid.NewGuid().ToString("N");

        public string DocumentPath { get; set; } = string.Empty;

        public int LineNumber { get; set; }

        public int ColumnNumber { get; set; }

        public string LineText { get; set; } = string.Empty;

        public int? SlotNumber { get; set; }

        public string Label { get; set; } = string.Empty;

        public string Group { get; set; } = string.Empty;

        public BookmarkColor Color { get; set; }

        public DateTime CreatedUtc { get; set; }

        public DateTime? LastVisitedUtc { get; set; }

        public DateTime LastSeenUtc { get; set; }

        public string ExactMatchKey => BookmarkIdentity.CreateExactMatchKey(DocumentPath, LineNumber);

        public void UpdateFromSnapshot(BookmarkSnapshot snapshot, DateTime seenUtc)
        {
            if (snapshot is null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            DocumentPath = snapshot.DocumentPath;
            LineNumber = snapshot.LineNumber;
            ColumnNumber = snapshot.ColumnNumber;
            LineText = snapshot.LineText;
            LastSeenUtc = seenUtc;

            if (CreatedUtc == default)
            {
                CreatedUtc = seenUtc;
            }
        }

        public ManagedBookmark ToManagedBookmark()
        {
            return new ManagedBookmark
            {
                BookmarkId = BookmarkId,
                DocumentPath = DocumentPath,
                LineNumber = LineNumber,
                ColumnNumber = ColumnNumber,
                LineText = (LineText ?? string.Empty).Trim(),
                SlotNumber = SlotNumber,
                Label = Label,
                Group = Group,
                Color = Color,
                CreatedUtc = CreatedUtc,
                LastVisitedUtc = LastVisitedUtc,
            };
        }
    }

    internal sealed class ManagedBookmark
    {
        public string BookmarkId { get; set; } = string.Empty;

        public string DocumentPath { get; set; } = string.Empty;

        public int LineNumber { get; set; }

        public int ColumnNumber { get; set; }

        public string LineText { get; set; } = string.Empty;

        public int? SlotNumber { get; set; }

        public string Label { get; set; } = string.Empty;

        public string Group { get; set; } = string.Empty;

        public BookmarkColor Color { get; set; }

        public DateTime CreatedUtc { get; set; }

        public DateTime? LastVisitedUtc { get; set; }

        public string FileName => Path.GetFileName(DocumentPath);

        public string Location => string.IsNullOrWhiteSpace(DocumentPath)
            ? string.Empty
            : string.Concat(DocumentPath, "(", LineNumber.ToString(System.Globalization.CultureInfo.InvariantCulture), ")");

    }
}