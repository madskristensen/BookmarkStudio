using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

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
            => ToggleActiveBookmarkAsync(label: null, cancellationToken);

        public Task<ManagedBookmark?> ToggleBookmarkAsync(string? label, CancellationToken cancellationToken)
            => ToggleActiveBookmarkAsync(label, cancellationToken);

        public async Task<bool> HasBookmarkAtCurrentLocationAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<ManagedBookmark> bookmarks = await _session.RefreshAsync(cancellationToken);
            ManagedBookmark? activeBookmark = await GetActiveBookmarkAsync(bookmarks, cancellationToken);
            return activeBookmark is not null;
        }

        public async Task<string> GetNextDefaultLabelAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<BookmarkMetadata> metadata = await _session.LoadMetadataAsync(cancellationToken);
            return BookmarkRepositoryService.FindNextDefaultLabel(metadata);
        }

        public bool CanToggleBookmarkInActiveDocument()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            DTE2? dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            Document? activeDocument = dte?.ActiveDocument;
            if (activeDocument is null || string.IsNullOrWhiteSpace(activeDocument.FullName))
            {
                return false;
            }

            TextDocument? textDocument = activeDocument.Object("TextDocument") as TextDocument;
            return textDocument is not null;
        }

        public bool IsSolutionOrFolderOpen()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            DTE2? dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
            if (dte?.Solution?.IsOpen == true)
            {
                return true;
            }

            IVsSolution? solutionService = Package.GetGlobalService(typeof(SVsSolution)) as IVsSolution;
            if (solutionService is null)
            {
                return false;
            }

            int hr = solutionService.GetProperty((int)__VSPROPID7.VSPROPID_IsInOpenFolderMode, out object value);
            return hr == 0 && value is bool isOpenFolderMode && isOpenFolderMode;
        }

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

        public async Task<IReadOnlyList<ManagedBookmark>> ClearSlotAsync(string? bookmarkId, CancellationToken cancellationToken)
        {
            ManagedBookmark targetBookmark = await GetRequiredBookmarkAsync(bookmarkId, cancellationToken);
            return await _session.UpdateBookmarksAsync(metadata =>
            {
                BookmarkMetadata targetMetadata = BookmarkRepositoryService.GetRequiredBookmark(metadata, targetBookmark.BookmarkId);
                targetMetadata.SlotNumber = null;
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

        public async Task<IReadOnlyList<ManagedBookmark>> RemoveBookmarkAsync(string? bookmarkId, CancellationToken cancellationToken)
        {
            ManagedBookmark targetBookmark = await GetRequiredBookmarkAsync(bookmarkId, cancellationToken);
            return await _session.UpdateBookmarksAsync(metadata =>
            {
                BookmarkMetadata targetMetadata = BookmarkRepositoryService.GetRequiredBookmark(metadata, targetBookmark.BookmarkId);
                metadata.Remove(targetMetadata);
            }, cancellationToken);
        }

        public async Task<IReadOnlyList<string>> GetFolderPathsAsync(CancellationToken cancellationToken)
        {
            BookmarkWorkspaceState workspace = await _session.LoadWorkspaceAsync(cancellationToken);
            return workspace.FolderPaths
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public async Task<IEnumerable<string>> GetExpandedFoldersAsync(CancellationToken cancellationToken)
        {
            BookmarkWorkspaceState workspace = await _session.LoadWorkspaceAsync(cancellationToken);
            return workspace.ExpandedFolders;
        }

        public async Task SetExpandedFoldersAsync(IEnumerable<string> expandedFolders, CancellationToken cancellationToken)
        {
            await _session.UpdateWorkspaceAsync(workspace =>
            {
                workspace.ExpandedFolders.Clear();
                foreach (string folder in expandedFolders)
                {
                    workspace.ExpandedFolders.Add(folder);
                }
            }, cancellationToken);
        }

        public async Task<IReadOnlyList<ManagedBookmark>> CreateFolderAsync(string? parentFolderPath, string? folderName, CancellationToken cancellationToken)
        {
            string normalizedFolderName = ValidateFolderName(folderName);
            string parentPath = BookmarkIdentity.NormalizeFolderPath(parentFolderPath);
            string targetPath = string.IsNullOrEmpty(parentPath)
                ? normalizedFolderName
                : string.Concat(parentPath, "/", normalizedFolderName);

            return await _session.UpdateWorkspaceAsync(workspace =>
            {
                if (workspace.FolderPaths.Contains(targetPath, StringComparer.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("A folder with the same name already exists in this location.");
                }

                BookmarkRepositoryService.EnsureFolderPath(workspace, targetPath);
            }, cancellationToken);
        }

        public async Task<IReadOnlyList<ManagedBookmark>> RenameFolderAsync(string? sourceFolderPath, string? targetFolderName, CancellationToken cancellationToken)
        {
            string sourcePath = BookmarkIdentity.NormalizeFolderPath(sourceFolderPath);
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("Select a folder to rename.", nameof(sourceFolderPath));
            }

            string normalizedFolderName = ValidateFolderName(targetFolderName);
            return await _session.UpdateWorkspaceAsync(workspace =>
            {
                string parentPath = GetParentFolderPath(sourcePath);
                string targetPath = string.IsNullOrEmpty(parentPath)
                    ? normalizedFolderName
                    : string.Concat(parentPath, "/", normalizedFolderName);

                if (workspace.FolderPaths.Contains(targetPath, StringComparer.OrdinalIgnoreCase)
                    && !string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("A folder with the same name already exists in this location.");
                }

                if (!BookmarkRepositoryService.RenameFolder(workspace, sourcePath, normalizedFolderName))
                {
                    throw new InvalidOperationException("The selected folder could not be renamed.");
                }
            }, cancellationToken);
        }

        public async Task<IReadOnlyList<ManagedBookmark>> DeleteFolderRecursiveAsync(string? folderPath, CancellationToken cancellationToken)
        {
            string normalizedFolderPath = BookmarkIdentity.NormalizeFolderPath(folderPath);
            if (string.IsNullOrWhiteSpace(normalizedFolderPath))
            {
                throw new ArgumentException("Select a folder to delete.", nameof(folderPath));
            }

            return await _session.UpdateWorkspaceAsync(workspace =>
            {
                if (!BookmarkRepositoryService.DeleteFolderRecursive(workspace, normalizedFolderPath))
                {
                    throw new InvalidOperationException("The selected folder could not be deleted.");
                }
            }, cancellationToken);
        }

        public Task<IReadOnlyList<ManagedBookmark>> ClearAllAsync(CancellationToken cancellationToken)
            => _session.UpdateWorkspaceAsync(workspace =>
            {
                workspace.Bookmarks.Clear();
                workspace.FolderPaths.Clear();
                workspace.FolderPaths.Add(string.Empty);
            }, cancellationToken);

        public async Task<IReadOnlyList<ManagedBookmark>> MoveBookmarkToFolderAsync(string? bookmarkId, string? folderPath, CancellationToken cancellationToken)
        {
            ManagedBookmark targetBookmark = await GetRequiredBookmarkAsync(bookmarkId, cancellationToken);

            return await _session.UpdateWorkspaceAsync(workspace =>
            {
                BookmarkRepositoryService.MoveBookmarkToFolder(workspace, targetBookmark.BookmarkId, folderPath);
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

            Window window = dte.ItemOperations.OpenFile(bookmark.DocumentPath, EnvDTE.Constants.vsViewKindTextView);
            Document? document = window.Document ?? dte.ActiveDocument;
            TextDocument? textDocument = document?.Object("TextDocument") as TextDocument;
            if (textDocument is null)
            {
                throw new InvalidOperationException("The selected bookmark could not be opened as a text document.");
            }

            document?.Activate();
            textDocument.Selection.MoveToLineAndOffset(bookmark.LineNumber, 1, false);
        }

        private async Task<ManagedBookmark> NavigateRelativeAsync(int direction, CancellationToken cancellationToken)
        {
            IReadOnlyList<ManagedBookmark> bookmarks = (await _session.RefreshAsync(cancellationToken))
                .OrderBy(item => BookmarkIdentity.NormalizeDocumentPath(item.DocumentPath), StringComparer.Ordinal)
                .ThenBy(item => item.LineNumber)
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

        private async Task<ManagedBookmark?> ToggleActiveBookmarkAsync(string? label, CancellationToken cancellationToken)
        {
            BookmarkSnapshot snapshot = await CaptureActiveSnapshotAsync(cancellationToken);
            ManagedBookmark? bookmark = await _session.ToggleBookmarkAsync(snapshot, label, cancellationToken);

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
            return new ActiveBookmarkLocation(activeDocument.FullName, activePoint.Line);
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

            return 0;
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
                    .Append(bookmark.Location)
                    .Append(" | ")
                    .AppendLine(bookmark.LineText);
            }

            return builder.ToString();
        }

        private static string BuildMarkdown(IEnumerable<ManagedBookmark> bookmarks)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("| Slot | Color | Label | File | Line | Preview |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- |");

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
            builder.AppendLine("Slot,Color,Label,Document,Line,Preview");

            foreach (ManagedBookmark bookmark in bookmarks)
            {
                builder.Append(EscapeCsv(bookmark.SlotNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)).Append(',')
                    .Append(EscapeCsv(bookmark.Color == BookmarkColor.None ? string.Empty : bookmark.Color.ToString())).Append(',')
                    .Append(EscapeCsv(bookmark.Label)).Append(',')
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

        private static string ValidateFolderName(string? folderName)
        {
            if (string.IsNullOrWhiteSpace(folderName))
            {
                throw new ArgumentException("Folder name cannot be empty.", nameof(folderName));
            }

            string normalized = BookmarkIdentity.NormalizeFolderPath(folderName);
            if (string.IsNullOrWhiteSpace(normalized) || normalized.Contains("/"))
            {
                throw new ArgumentException("Folder name must be a single segment.", nameof(folderName));
            }

            if (string.Equals(normalized, "_bookmarks", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "root", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("The folder name is reserved.", nameof(folderName));
            }

            return normalized;
        }

        private static string GetParentFolderPath(string folderPath)
        {
            string normalized = BookmarkIdentity.NormalizeFolderPath(folderPath);
            int separatorIndex = normalized.LastIndexOf('/');
            return separatorIndex <= 0
                ? string.Empty
                : normalized.Substring(0, separatorIndex);
        }

        private sealed class ActiveBookmarkLocation
        {
            public ActiveBookmarkLocation(string documentPath, int lineNumber)
            {
                NormalizedDocumentPath = BookmarkIdentity.NormalizeDocumentPath(documentPath);
                LineNumber = lineNumber;
            }

            public string NormalizedDocumentPath { get; }

            public int LineNumber { get; }
        }
    }
}