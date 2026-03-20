using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;

namespace BookmarkStudio
{
    internal enum BookmarkExportFormat
    {
        PlainText,
        Markdown,
        Csv,
    }

    internal sealed class BookmarkOperationsService
    {
        private static readonly Lazy<BookmarkOperationsService> _instance = new Lazy<BookmarkOperationsService>(() => new BookmarkOperationsService());
        private readonly BookmarkStudioSession _session = BookmarkStudioSession.Current;

        public static BookmarkOperationsService Current => _instance.Value;

        public Task<IReadOnlyList<ManagedBookmark>> RefreshAsync(CancellationToken cancellationToken)
            => _session.RefreshAsync(cancellationToken);

        public Task<ManagedBookmark> GetBookmarkAsync(string? bookmarkId, CancellationToken cancellationToken)
            => GetRequiredBookmarkAsync(bookmarkId, cancellationToken);

        public Task<ManagedBookmark?> ToggleBookmarkAsync(CancellationToken cancellationToken)
            => ToggleActiveBookmarkAsync(cancellationToken);

        public Task<ManagedBookmark> GoToNextBookmarkAsync(CancellationToken cancellationToken)
            => NavigateRelativeAsync(1, cancellationToken);

        public Task<ManagedBookmark> GoToPreviousBookmarkAsync(CancellationToken cancellationToken)
            => NavigateRelativeAsync(-1, cancellationToken);

        public async Task<IReadOnlyList<ManagedBookmark>> AssignSlotAsync(int slotNumber, string? bookmarkId, CancellationToken cancellationToken)
        {
            if (slotNumber < 1 || slotNumber > 9)
            {
                throw new ArgumentOutOfRangeException(nameof(slotNumber), "Bookmark slots must be in the range 1..9.");
            }

            ManagedBookmark targetBookmark = await GetRequiredBookmarkAsync(bookmarkId, cancellationToken);
            return await _session.UpdateBookmarksAsync(metadata =>
            {
                BookmarkMetadata targetMetadata = BookmarkRepositoryService.GetRequiredBookmark(metadata, targetBookmark.BookmarkId);

                foreach (BookmarkMetadata item in metadata.Where(item => item.SlotNumber == slotNumber && !string.Equals(item.BookmarkId, targetBookmark.BookmarkId, StringComparison.Ordinal)))
                {
                    item.SlotNumber = null;
                }

                targetMetadata.SlotNumber = slotNumber;
            }, cancellationToken);
        }

        public async Task<IReadOnlyList<ManagedBookmark>> RenameLabelAsync(string? bookmarkId, string label, CancellationToken cancellationToken)
        {
            ManagedBookmark targetBookmark = await GetRequiredBookmarkAsync(bookmarkId, cancellationToken);
            return await _session.UpdateBookmarksAsync(metadata =>
            {
                BookmarkMetadata targetMetadata = BookmarkRepositoryService.GetRequiredBookmark(metadata, targetBookmark.BookmarkId);
                targetMetadata.Label = label?.Trim() ?? string.Empty;
            }, cancellationToken);
        }

        public async Task<IReadOnlyList<ManagedBookmark>> SetGroupAsync(string? bookmarkId, string group, CancellationToken cancellationToken)
        {
            ManagedBookmark targetBookmark = await GetRequiredBookmarkAsync(bookmarkId, cancellationToken);
            return await _session.UpdateBookmarksAsync(metadata =>
            {
                BookmarkMetadata targetMetadata = BookmarkRepositoryService.GetRequiredBookmark(metadata, targetBookmark.BookmarkId);
                targetMetadata.Group = group?.Trim() ?? string.Empty;
            }, cancellationToken);
        }

        public async Task<IReadOnlyList<ManagedBookmark>> ClearSlotAsync(string? bookmarkId, CancellationToken cancellationToken)
        {
            ManagedBookmark targetBookmark = await GetRequiredBookmarkAsync(bookmarkId, cancellationToken);
            return await _session.UpdateBookmarksAsync(metadata =>
            {
                BookmarkMetadata targetMetadata = BookmarkRepositoryService.GetRequiredBookmark(metadata, targetBookmark.BookmarkId);
                targetMetadata.SlotNumber = null;
            }, cancellationToken);
        }

        public async Task<IReadOnlyList<ManagedBookmark>> ClearGroupAsync(string? bookmarkId, CancellationToken cancellationToken)
        {
            ManagedBookmark targetBookmark = await GetRequiredBookmarkAsync(bookmarkId, cancellationToken);
            return await _session.UpdateBookmarksAsync(metadata =>
            {
                BookmarkMetadata targetMetadata = BookmarkRepositoryService.GetRequiredBookmark(metadata, targetBookmark.BookmarkId);
                targetMetadata.Group = string.Empty;
            }, cancellationToken);
        }

        public async Task<IReadOnlyList<ManagedBookmark>> SetColorAsync(string? bookmarkId, BookmarkColor color, CancellationToken cancellationToken)
        {
            ManagedBookmark targetBookmark = await GetRequiredBookmarkAsync(bookmarkId, cancellationToken);
            return await _session.UpdateBookmarksAsync(metadata =>
            {
                BookmarkMetadata targetMetadata = BookmarkRepositoryService.GetRequiredBookmark(metadata, targetBookmark.BookmarkId);
                targetMetadata.Color = color;
            }, cancellationToken);
        }

        public async Task<IReadOnlyList<ManagedBookmark>> ClearColorAsync(string? bookmarkId, CancellationToken cancellationToken)
        {
            ManagedBookmark targetBookmark = await GetRequiredBookmarkAsync(bookmarkId, cancellationToken);
            return await _session.UpdateBookmarksAsync(metadata =>
            {
                BookmarkMetadata targetMetadata = BookmarkRepositoryService.GetRequiredBookmark(metadata, targetBookmark.BookmarkId);
                targetMetadata.Color = BookmarkColor.None;
            }, cancellationToken);
        }

        public async Task<IReadOnlyList<ManagedBookmark>> RemoveBookmarkAsync(string? bookmarkId, CancellationToken cancellationToken)
        {
            ManagedBookmark targetBookmark = await GetRequiredBookmarkAsync(bookmarkId, cancellationToken);
            return await _session.UpdateBookmarksAsync(metadata =>
            {
                BookmarkMetadata targetMetadata = BookmarkRepositoryService.GetRequiredBookmark(metadata, targetBookmark.BookmarkId);
                metadata.Remove(targetMetadata);
            }, cancellationToken);
        }

        public async Task<ManagedBookmark> GoToSlotAsync(int slotNumber, CancellationToken cancellationToken)
        {
            if (slotNumber < 1 || slotNumber > 9)
            {
                throw new ArgumentOutOfRangeException(nameof(slotNumber), "Bookmark slots must be in the range 1..9.");
            }

            IReadOnlyList<ManagedBookmark> bookmarks = await _session.RefreshAsync(cancellationToken);
            ManagedBookmark bookmark = bookmarks.FirstOrDefault(item => item.SlotNumber == slotNumber)
                ?? throw new InvalidOperationException(string.Concat("No bookmark is assigned to slot ", slotNumber.ToString(System.Globalization.CultureInfo.InvariantCulture), "."));

            await NavigateToBookmarkAsync(bookmark, cancellationToken);
            await TouchLastVisitedAsync(bookmark.BookmarkId, cancellationToken);
            return bookmark;
        }

        public async Task<string> ExportAsync(BookmarkExportFormat format, CancellationToken cancellationToken)
        {
            IReadOnlyList<ManagedBookmark> bookmarks = await _session.RefreshAsync(cancellationToken);
            return format switch
            {
                BookmarkExportFormat.Markdown => BuildMarkdown(bookmarks),
                BookmarkExportFormat.Csv => BuildCsv(bookmarks),
                _ => BuildPlainText(bookmarks),
            };
        }

        public async Task<string> GetBookmarkLocationAsync(string? bookmarkId, CancellationToken cancellationToken)
        {
            ManagedBookmark bookmark = await GetRequiredBookmarkAsync(bookmarkId, cancellationToken);
            return bookmark.Location;
        }

        public async Task<ManagedBookmark> NavigateToBookmarkAsync(string? bookmarkId, CancellationToken cancellationToken)
        {
            ManagedBookmark bookmark = await GetRequiredBookmarkAsync(bookmarkId, cancellationToken);
            await NavigateToBookmarkAsync(bookmark, cancellationToken);
            await TouchLastVisitedAsync(bookmark.BookmarkId, cancellationToken);
            return bookmark;
        }

        private async Task<ManagedBookmark> GetRequiredBookmarkAsync(string? bookmarkId, CancellationToken cancellationToken)
        {
            IReadOnlyList<ManagedBookmark> bookmarks = await _session.RefreshAsync(cancellationToken);
            ManagedBookmark? bookmark = !string.IsNullOrWhiteSpace(bookmarkId)
                ? bookmarks.FirstOrDefault(item => string.Equals(item.BookmarkId, bookmarkId, StringComparison.Ordinal))
                : await GetActiveBookmarkAsync(bookmarks, cancellationToken);

            return bookmark ?? throw new InvalidOperationException("No bookmark could be resolved for the current operation.");
        }

        private static async Task<ManagedBookmark?> GetActiveBookmarkAsync(IEnumerable<ManagedBookmark> bookmarks, CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            DTE2? dte = await VS.GetServiceAsync<DTE, DTE2>();
            Document? activeDocument = dte?.ActiveDocument;
            if (activeDocument is null || string.IsNullOrWhiteSpace(activeDocument.FullName))
            {
                return null;
            }

            TextDocument? textDocument = activeDocument.Object("TextDocument") as TextDocument;
            if (textDocument is null)
            {
                return null;
            }

            int lineNumber = textDocument.Selection.ActivePoint.Line;
            return bookmarks.FirstOrDefault(item =>
                string.Equals(BookmarkIdentity.NormalizeDocumentPath(item.DocumentPath), BookmarkIdentity.NormalizeDocumentPath(activeDocument.FullName), StringComparison.Ordinal) &&
                item.LineNumber == lineNumber);
        }

        private static async Task NavigateToBookmarkAsync(ManagedBookmark bookmark, CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            DTE2? dte = await VS.GetServiceAsync<DTE, DTE2>();
            if (dte is null)
            {
                throw new InvalidOperationException("Visual Studio automation services are unavailable.");
            }

            Window window = dte.ItemOperations.OpenFile(bookmark.DocumentPath, Constants.vsViewKindTextView);
            Document? document = window.Document ?? dte.ActiveDocument;
            TextDocument? textDocument = document?.Object("TextDocument") as TextDocument;
            if (textDocument is null)
            {
                throw new InvalidOperationException("The selected bookmark could not be opened as a text document.");
            }

            document?.Activate();
            int column = bookmark.ColumnNumber > 0 ? bookmark.ColumnNumber : 1;
            textDocument.Selection.MoveToLineAndOffset(bookmark.LineNumber, column, false);
        }

        private async Task<ManagedBookmark> NavigateRelativeAsync(int direction, CancellationToken cancellationToken)
        {
            IReadOnlyList<ManagedBookmark> bookmarks = (await _session.RefreshAsync(cancellationToken))
                .OrderBy(item => BookmarkIdentity.NormalizeDocumentPath(item.DocumentPath), StringComparer.Ordinal)
                .ThenBy(item => item.LineNumber)
                .ThenBy(item => item.ColumnNumber)
                .ToArray();

            if (bookmarks.Count == 0)
            {
                throw new InvalidOperationException("No BookmarkStudio bookmarks exist yet.");
            }

            ActiveBookmarkLocation? activeLocation = await GetActiveLocationAsync(cancellationToken);
            ManagedBookmark bookmark;

            if (activeLocation is null)
            {
                bookmark = direction >= 0 ? bookmarks[0] : bookmarks[bookmarks.Count - 1];
            }
            else if (direction >= 0)
            {
                bookmark = bookmarks.FirstOrDefault(item => CompareBookmark(item, activeLocation) > 0) ?? bookmarks[0];
            }
            else
            {
                bookmark = bookmarks.LastOrDefault(item => CompareBookmark(item, activeLocation) < 0) ?? bookmarks[bookmarks.Count - 1];
            }

            await NavigateToBookmarkAsync(bookmark, cancellationToken);
            await TouchLastVisitedAsync(bookmark.BookmarkId, cancellationToken);
            return bookmark;
        }

        private async Task<ManagedBookmark?> ToggleActiveBookmarkAsync(CancellationToken cancellationToken)
        {
            BookmarkSnapshot snapshot = await CaptureActiveSnapshotAsync(cancellationToken);
            ManagedBookmark? bookmark = await _session.ToggleBookmarkAsync(snapshot, cancellationToken);

            if (bookmark is null)
            {
                return null;
            }

            return (await _session.RefreshAsync(cancellationToken))
                .FirstOrDefault(item => string.Equals(item.BookmarkId, bookmark.BookmarkId, StringComparison.Ordinal));
        }

        private static async Task<BookmarkSnapshot> CaptureActiveSnapshotAsync(CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            DTE2? dte = await VS.GetServiceAsync<DTE, DTE2>();
            Document? activeDocument = dte?.ActiveDocument;
            if (activeDocument is null || string.IsNullOrWhiteSpace(activeDocument.FullName))
            {
                throw new InvalidOperationException("Open a text document before toggling a bookmark.");
            }

            TextDocument? textDocument = activeDocument.Object("TextDocument") as TextDocument;
            if (textDocument is null)
            {
                throw new InvalidOperationException("The active document does not support BookmarkStudio bookmarks.");
            }

            VirtualPoint activePoint = textDocument.Selection.ActivePoint;
            int lineNumber = activePoint.Line;
            EditPoint lineStart = textDocument.CreateEditPoint();
            lineStart.MoveToLineAndOffset(lineNumber, 1);
            string lineText = lineStart.GetLines(lineNumber, lineNumber + 1).TrimEnd('\r', '\n');

            return new BookmarkSnapshot
            {
                DocumentPath = activeDocument.FullName,
                LineNumber = lineNumber,
                ColumnNumber = activePoint.LineCharOffset,
                LineText = lineText,
            };
        }

        private static async Task<ActiveBookmarkLocation?> GetActiveLocationAsync(CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            DTE2? dte = await VS.GetServiceAsync<DTE, DTE2>();
            Document? activeDocument = dte?.ActiveDocument;
            if (activeDocument is null || string.IsNullOrWhiteSpace(activeDocument.FullName))
            {
                return null;
            }

            TextDocument? textDocument = activeDocument.Object("TextDocument") as TextDocument;
            if (textDocument is null)
            {
                return null;
            }

            VirtualPoint activePoint = textDocument.Selection.ActivePoint;
            return new ActiveBookmarkLocation(activeDocument.FullName, activePoint.Line, activePoint.LineCharOffset);
        }

        private static int CompareBookmark(ManagedBookmark bookmark, ActiveBookmarkLocation location)
        {
            int documentComparison = string.Compare(
                BookmarkIdentity.NormalizeDocumentPath(bookmark.DocumentPath),
                location.NormalizedDocumentPath,
                StringComparison.Ordinal);

            if (documentComparison != 0)
            {
                return documentComparison;
            }

            int lineComparison = bookmark.LineNumber.CompareTo(location.LineNumber);
            if (lineComparison != 0)
            {
                return lineComparison;
            }

            return bookmark.ColumnNumber.CompareTo(location.ColumnNumber);
        }

        private async Task TouchLastVisitedAsync(string bookmarkId, CancellationToken cancellationToken)
        {
            await _session.UpdateBookmarksAsync(metadata =>
            {
                BookmarkMetadata targetMetadata = BookmarkRepositoryService.GetRequiredBookmark(metadata, bookmarkId);
                targetMetadata.LastVisitedUtc = DateTime.UtcNow;
            }, cancellationToken);
        }

        private static string BuildPlainText(IEnumerable<ManagedBookmark> bookmarks)
        {
            StringBuilder builder = new StringBuilder();

            foreach (ManagedBookmark bookmark in bookmarks)
            {
                string slot = bookmark.SlotNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-";
                string color = bookmark.Color == BookmarkColor.None ? "-" : bookmark.Color.ToString();
                builder.Append(slot)
                    .Append(" | ")
                    .Append(color)
                    .Append(" | ")
                    .Append(string.IsNullOrWhiteSpace(bookmark.Label) ? "(no label)" : bookmark.Label)
                    .Append(" | ")
                    .Append(string.IsNullOrWhiteSpace(bookmark.Group) ? "(no group)" : bookmark.Group)
                    .Append(" | ")
                    .Append(bookmark.Location)
                    .Append(" | ")
                    .AppendLine(bookmark.LineText);
            }

            return builder.ToString();
        }

        private static string BuildMarkdown(IEnumerable<ManagedBookmark> bookmarks)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("| Slot | Color | Label | Group | File | Line | Preview |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");

            foreach (ManagedBookmark bookmark in bookmarks)
            {
                builder.Append('|')
                    .Append(' ')
                    .Append(bookmark.SlotNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-")
                    .Append(" | ")
                    .Append(bookmark.Color == BookmarkColor.None ? "-" : bookmark.Color.ToString())
                    .Append(" | ")
                    .Append(EscapeMarkdown(bookmark.Label))
                    .Append(" | ")
                    .Append(EscapeMarkdown(bookmark.Group))
                    .Append(" | ")
                    .Append(EscapeMarkdown(bookmark.FileName))
                    .Append(" | ")
                    .Append(bookmark.LineNumber.ToString(System.Globalization.CultureInfo.InvariantCulture))
                    .Append(" | ")
                    .Append(EscapeMarkdown(bookmark.LineText))
                    .AppendLine(" |");
            }

            return builder.ToString();
        }

        private static string BuildCsv(IEnumerable<ManagedBookmark> bookmarks)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Slot,Color,Label,Group,Document,Line,Preview");

            foreach (ManagedBookmark bookmark in bookmarks)
            {
                builder.Append(EscapeCsv(bookmark.SlotNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)).Append(',')
                    .Append(EscapeCsv(bookmark.Color == BookmarkColor.None ? string.Empty : bookmark.Color.ToString())).Append(',')
                    .Append(EscapeCsv(bookmark.Label)).Append(',')
                    .Append(EscapeCsv(bookmark.Group)).Append(',')
                    .Append(EscapeCsv(bookmark.DocumentPath)).Append(',')
                    .Append(EscapeCsv(bookmark.LineNumber.ToString(System.Globalization.CultureInfo.InvariantCulture))).Append(',')
                    .Append(EscapeCsv(bookmark.LineText))
                    .AppendLine();
            }

            return builder.ToString();
        }

        private static string EscapeMarkdown(string value)
            => (value ?? string.Empty).Replace("|", "\\|").Replace("\r", string.Empty).Replace("\n", " ");

        private static string EscapeCsv(string value)
        {
            string normalized = (value ?? string.Empty).Replace("\r", string.Empty).Replace("\n", " ");
            return string.Concat("\"", normalized.Replace("\"", "\"\""), "\"");
        }

        private sealed class ActiveBookmarkLocation
        {
            public ActiveBookmarkLocation(string documentPath, int lineNumber, int columnNumber)
            {
                NormalizedDocumentPath = BookmarkIdentity.NormalizeDocumentPath(documentPath);
                LineNumber = lineNumber;
                ColumnNumber = columnNumber;
            }

            public string NormalizedDocumentPath { get; }

            public int LineNumber { get; }

            public int ColumnNumber { get; }
        }
    }
}