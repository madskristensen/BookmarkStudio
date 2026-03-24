using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace BookmarkStudio
{
    internal sealed class BookmarkRepositoryService
    {
        private readonly BookmarkMetadataStore _store;

        public BookmarkRepositoryService(BookmarkMetadataStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public Task<IReadOnlyList<BookmarkMetadata>> LoadAsync(string solutionPath, CancellationToken cancellationToken)
            => _store.LoadAsync(solutionPath, cancellationToken);

        public Task<BookmarkWorkspaceState> LoadWorkspaceAsync(string solutionPath, CancellationToken cancellationToken)
            => _store.LoadWorkspaceAsync(solutionPath, cancellationToken);

        public Task SaveAsync(string solutionPath, IEnumerable<BookmarkMetadata> bookmarks, CancellationToken cancellationToken)
            => _store.SaveAsync(solutionPath, bookmarks, cancellationToken);

        public Task SaveWorkspaceAsync(string solutionPath, BookmarkWorkspaceState state, CancellationToken cancellationToken)
            => _store.SaveWorkspaceAsync(solutionPath, state, cancellationToken);

        public async Task<IReadOnlyList<ManagedBookmark>> ListAsync(string solutionPath, CancellationToken cancellationToken)
        {
            IReadOnlyList<BookmarkMetadata> bookmarks = await LoadAsync(solutionPath, cancellationToken);
            return ToManagedBookmarks(bookmarks);
        }

        public async Task<IReadOnlyList<ManagedBookmark>> UpdateAsync(string solutionPath, Action<List<BookmarkMetadata>> updateAction, CancellationToken cancellationToken)
        {
            if (updateAction is null)
            {
                throw new ArgumentNullException(nameof(updateAction));
            }

            List<BookmarkMetadata> bookmarks = [.. (await LoadAsync(solutionPath, cancellationToken))];
            updateAction(bookmarks);
            await SaveAsync(solutionPath, bookmarks, cancellationToken);
            return ToManagedBookmarks(bookmarks);
        }

        public async Task<IReadOnlyList<ManagedBookmark>> UpdateWorkspaceAsync(string solutionPath, Action<BookmarkWorkspaceState> updateAction, CancellationToken cancellationToken)
        {
            if (updateAction is null)
            {
                throw new ArgumentNullException(nameof(updateAction));
            }

            BookmarkWorkspaceState state = await LoadWorkspaceAsync(solutionPath, cancellationToken);
            updateAction(state);
            NormalizeWorkspaceState(state);
            await SaveWorkspaceAsync(solutionPath, state, cancellationToken);
            return ToManagedBookmarks(state.Bookmarks);
        }

        public async Task<IReadOnlyList<ManagedBookmark>> TryUpdateAsync(string solutionPath, Func<List<BookmarkMetadata>, bool> updateAction, CancellationToken cancellationToken)
        {
            if (updateAction is null)
            {
                throw new ArgumentNullException(nameof(updateAction));
            }

            List<BookmarkMetadata> bookmarks = [.. (await LoadAsync(solutionPath, cancellationToken))];
            if (!updateAction(bookmarks))
            {
                return ToManagedBookmarks(bookmarks);
            }

            await SaveAsync(solutionPath, bookmarks, cancellationToken);
            return ToManagedBookmarks(bookmarks);
        }

        public async Task<ManagedBookmark?> ToggleAsync(string solutionPath, BookmarkSnapshot snapshot, CancellationToken cancellationToken)
                    => await ToggleAsync(solutionPath, snapshot, label: null, cancellationToken);

        public async Task<ManagedBookmark?> ToggleAsync(string solutionPath, BookmarkSnapshot snapshot, string? label, CancellationToken cancellationToken)
        {
            if (snapshot is null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            List<BookmarkMetadata> bookmarks = [.. (await LoadAsync(solutionPath, cancellationToken))];
            BookmarkMetadata existingBookmark = FindBySnapshot(bookmarks, snapshot);
            if (existingBookmark is not null)
            {
                bookmarks.Remove(existingBookmark);
                await SaveAsync(solutionPath, bookmarks, cancellationToken);
                return null;
            }

            BookmarkMetadata createdBookmark = CreateBookmarkMetadata(bookmarks, snapshot, label);
            bookmarks.Add(createdBookmark);
            await SaveAsync(solutionPath, bookmarks, cancellationToken);
            return createdBookmark.ToManagedBookmark();
        }

        public static BookmarkMetadata GetRequiredBookmark(IEnumerable<BookmarkMetadata> bookmarks, string bookmarkId)
            => bookmarks.FirstOrDefault(item => string.Equals(item.BookmarkId, bookmarkId, StringComparison.Ordinal))
                ?? throw new InvalidOperationException("Bookmark metadata could not be located.");

        public static BookmarkMetadata FindBySnapshot(IEnumerable<BookmarkMetadata> bookmarks, BookmarkSnapshot snapshot)
            => bookmarks.FirstOrDefault(item => string.Equals(item.ExactMatchKey, snapshot.ExactMatchKey, StringComparison.Ordinal));

        public static BookmarkMetadata CreateBookmarkMetadata(IEnumerable<BookmarkMetadata> existingBookmarks, BookmarkSnapshot snapshot, string? label)
        {
            var bookmark = new BookmarkMetadata
            {
                BookmarkId = Guid.NewGuid().ToString("N"),
                ShortcutNumber = FindNextAvailableShortcut(existingBookmarks),
                Label = string.IsNullOrWhiteSpace(label) ? FindNextDefaultLabel(existingBookmarks) : label,
                Color = BookmarkColor.Blue,
            };

            bookmark.UpdateFromSnapshot(snapshot);
            return bookmark;
        }

        public static void EnsureFolderPath(BookmarkWorkspaceState state, string? folderPath)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            RegisterFolderPath(state.FolderPaths, folderPath);
        }

        public static bool DeleteFolderRecursive(BookmarkWorkspaceState state, string? folderPath)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var targetPath = BookmarkIdentity.NormalizeFolderPath(folderPath);
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return false;
            }

            var prefix = string.Concat(targetPath, "/");
            var beforeBookmarkCount = state.Bookmarks.Count;
            state.Bookmarks.RemoveAll(item =>
            {
                var group = BookmarkIdentity.NormalizeFolderPath(item.Group);
                return string.Equals(group, targetPath, StringComparison.OrdinalIgnoreCase)
                    || group.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            });

            var beforeFolderCount = state.FolderPaths.Count;
            state.FolderPaths.RemoveWhere(path =>
                string.Equals(path, targetPath, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

            state.FolderPaths.Add(string.Empty);
            return beforeBookmarkCount != state.Bookmarks.Count || beforeFolderCount != state.FolderPaths.Count;
        }

        public static bool RenameFolder(BookmarkWorkspaceState state, string? sourceFolderPath, string? targetFolderName)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var sourcePath = BookmarkIdentity.NormalizeFolderPath(sourceFolderPath);
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return false;
            }

            var parentPath = GetParentFolderPath(sourcePath);
            var renamedPath = string.IsNullOrEmpty(parentPath)
                ? BookmarkIdentity.NormalizeFolderPath(targetFolderName)
                : string.Concat(parentPath, "/", BookmarkIdentity.NormalizeFolderPath(targetFolderName));

            if (string.IsNullOrWhiteSpace(renamedPath) || string.Equals(sourcePath, renamedPath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var sourcePrefix = string.Concat(sourcePath, "/");
            List<string> affectedFolders = [.. state.FolderPaths
                .Where(path => string.Equals(path, sourcePath, StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase))];

            foreach (var folder in affectedFolders)
            {
                state.FolderPaths.Remove(folder);
            }

            foreach (var folder in affectedFolders)
            {
                var suffix = folder.Length == sourcePath.Length ? string.Empty : folder.Substring(sourcePath.Length);
                RegisterFolderPath(state.FolderPaths, string.Concat(renamedPath, suffix));
            }

            // Update expanded folders to preserve expansion state after rename
            List<string> affectedExpandedFolders = [.. state.ExpandedFolders
                .Where(path => string.Equals(path, sourcePath, StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase))];

            foreach (var folder in affectedExpandedFolders)
            {
                state.ExpandedFolders.Remove(folder);
            }

            foreach (var folder in affectedExpandedFolders)
            {
                var suffix = folder.Length == sourcePath.Length ? string.Empty : folder.Substring(sourcePath.Length);
                state.ExpandedFolders.Add(string.Concat(renamedPath, suffix));
            }

            foreach (BookmarkMetadata bookmark in state.Bookmarks)
            {
                var group = BookmarkIdentity.NormalizeFolderPath(bookmark.Group);
                if (string.Equals(group, sourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    bookmark.Group = renamedPath;
                }
                else if (group.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    bookmark.Group = string.Concat(renamedPath, group.Substring(sourcePath.Length));
                }
            }

            return true;
        }

        public static void MoveBookmarkToFolder(BookmarkWorkspaceState state, string bookmarkId, string? folderPath)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            BookmarkMetadata bookmark = GetRequiredBookmark(state.Bookmarks, bookmarkId);
            var normalizedFolderPath = BookmarkIdentity.NormalizeFolderPath(folderPath);
            bookmark.Group = normalizedFolderPath;
            RegisterFolderPath(state.FolderPaths, normalizedFolderPath);
        }

        public static void MoveFolder(BookmarkWorkspaceState state, string sourceFolderPath, string targetFolderPath)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var sourcePath = BookmarkIdentity.NormalizeFolderPath(sourceFolderPath);
            var targetPath = BookmarkIdentity.NormalizeFolderPath(targetFolderPath);

            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("Source folder path cannot be empty.", nameof(sourceFolderPath));
            }

            if (string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var sourcePrefix = sourcePath + "/";

            // Update folder paths
            List<string> affectedFolders = [.. state.FolderPaths
                .Where(path => string.Equals(path, sourcePath, StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase))];

            foreach (var folder in affectedFolders)
            {
                state.FolderPaths.Remove(folder);
            }

            foreach (var folder in affectedFolders)
            {
                if (string.Equals(folder, sourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    RegisterFolderPath(state.FolderPaths, targetPath);
                }
                else
                {
                    var suffix = folder.Substring(sourcePath.Length);
                    RegisterFolderPath(state.FolderPaths, targetPath + suffix);
                }
            }

            // Update bookmark groups
            foreach (BookmarkMetadata bookmark in state.Bookmarks)
            {
                var group = BookmarkIdentity.NormalizeFolderPath(bookmark.Group);
                if (string.Equals(group, sourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    bookmark.Group = targetPath;
                }
                else if (group.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    bookmark.Group = targetPath + group.Substring(sourcePath.Length);
                }
            }
        }

        public static IReadOnlyList<ManagedBookmark> ToManagedBookmarks(IEnumerable<BookmarkMetadata> bookmarks)
            => bookmarks.Select(item => item.ToManagedBookmark())
                .OrderBy(item => item.ShortcutNumber.HasValue ? 0 : 1)
                .ThenBy(item => item.ShortcutNumber ?? int.MaxValue)
                .ThenBy(item => item.Group, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.DocumentPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.LineNumber)
                .ToArray();

        public static void NormalizeWorkspaceState(BookmarkWorkspaceState state)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            List<BookmarkMetadata> bookmarks = state.Bookmarks;
            foreach (BookmarkMetadata bookmark in bookmarks)
            {
                bookmark.Group = BookmarkIdentity.NormalizeFolderPath(bookmark.Group);
                RegisterFolderPath(state.FolderPaths, bookmark.Group);
            }

            state.FolderPaths.Add(string.Empty);
        }

        /// <summary>
        /// Returns the lowest available shortcut number (1-9), or null if all shortcuts are occupied.
        /// </summary>
        internal static int? FindNextAvailableShortcut(IEnumerable<BookmarkMetadata> bookmarks)
        {
            var usedShortcuts = new HashSet<int>(bookmarks.Where(b => b.ShortcutNumber.HasValue).Select(b => b.ShortcutNumber.GetValueOrDefault()));

            for (var shortcut = 1; shortcut <= 9; shortcut++)
            {
                if (!usedShortcuts.Contains(shortcut))
                {
                    return shortcut;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the next default label in the format "Bookmark1", "Bookmark2", etc.
        /// </summary>
        internal static string FindNextDefaultLabel(IEnumerable<BookmarkMetadata> bookmarks)
        {
            const string prefix = "Bookmark";
            var usedNumbers = new HashSet<int>();

            foreach (BookmarkMetadata bookmark in bookmarks)
            {
                var label = bookmark.Label ?? string.Empty;
                if (!label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var suffix = label.Substring(prefix.Length);
                if (int.TryParse(suffix, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number) && number > 0)
                {
                    usedNumbers.Add(number);
                }
            }

            var next = 1;
            while (usedNumbers.Contains(next))
            {
                next++;
            }

            return string.Concat(prefix, next.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Returns a unique label based on the preferred label.
        /// If the preferred label is already used, appends the lowest available numeric suffix (1, 2, 3, ...).
        /// </summary>
        internal static string FindNextAvailableLabel(IEnumerable<BookmarkMetadata> bookmarks, string? preferredLabel)
        {
            if (bookmarks is null)
            {
                throw new ArgumentNullException(nameof(bookmarks));
            }

            var baseLabel = string.IsNullOrWhiteSpace(preferredLabel)
                ? "Bookmark"
                : preferredLabel.Trim();

            var usedLabels = new HashSet<string>(
                bookmarks
                    .Select(item => item.Label)
                    .Where(label => !string.IsNullOrWhiteSpace(label))
                    .Select(label => label.Trim()),
                StringComparer.OrdinalIgnoreCase);

            if (!usedLabels.Contains(baseLabel))
            {
                return baseLabel;
            }

            var suffix = 1;
            while (usedLabels.Contains(string.Concat(baseLabel, suffix.ToString(System.Globalization.CultureInfo.InvariantCulture))))
            {
                suffix++;
            }

            return string.Concat(baseLabel, suffix.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Returns the next default folder name in the format "Folder1", "Folder2", etc.
        /// </summary>
        internal static string FindNextDefaultFolderName(IEnumerable<string> folderPaths)
        {
            const string prefix = "Folder";
            var usedNumbers = new HashSet<int>();

            foreach (string folderPath in folderPaths)
            {
                // Get the folder name (last segment of the path)
                var folderName = GetFolderNameFromPath(folderPath);
                if (string.IsNullOrEmpty(folderName) || !folderName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var suffix = folderName.Substring(prefix.Length);
                if (int.TryParse(suffix, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var number) && number > 0)
                {
                    usedNumbers.Add(number);
                }
            }

            var next = 1;
            while (usedNumbers.Contains(next))
            {
                next++;
            }

            return string.Concat(prefix, next.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        private static string GetFolderNameFromPath(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
            {
                return string.Empty;
            }

            var normalized = BookmarkIdentity.NormalizeFolderPath(folderPath);
            var separatorIndex = normalized.LastIndexOf('/');
            return separatorIndex < 0
                ? normalized
                : normalized.Substring(separatorIndex + 1);
        }

        private static string GetParentFolderPath(string folderPath)
        {
            var normalized = BookmarkIdentity.NormalizeFolderPath(folderPath);
            var separatorIndex = normalized.LastIndexOf('/');
            return separatorIndex <= 0
                ? string.Empty
                : normalized.Substring(0, separatorIndex);
        }

        private static void RegisterFolderPath(ISet<string> folderPaths, string? folderPath)
        {
            var normalized = BookmarkIdentity.NormalizeFolderPath(folderPath);
            folderPaths.Add(normalized);

            if (string.IsNullOrEmpty(normalized))
            {
                return;
            }

            var segments = normalized.Split('/');
            var current = string.Empty;
            foreach (var segment in segments)
            {
                current = string.IsNullOrEmpty(current)
                    ? segment
                    : string.Concat(current, "/", segment);

                folderPaths.Add(current);
            }
        }
    }
}
