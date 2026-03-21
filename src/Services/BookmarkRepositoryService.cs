using System;
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

            List<BookmarkMetadata> bookmarks = (await LoadAsync(solutionPath, cancellationToken)).ToList();
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

            List<BookmarkMetadata> bookmarks = (await LoadAsync(solutionPath, cancellationToken)).ToList();
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

                    List<BookmarkMetadata> bookmarks = (await LoadAsync(solutionPath, cancellationToken)).ToList();
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
            DateTime now = DateTime.UtcNow;
            BookmarkMetadata bookmark = new BookmarkMetadata
            {
                BookmarkId = Guid.NewGuid().ToString("N"),
                CreatedUtc = now,
                SlotNumber = FindNextAvailableSlot(existingBookmarks),
                Label = string.IsNullOrWhiteSpace(label) ? FindNextDefaultLabel(existingBookmarks) : label,
                Color = BookmarkColor.Blue,
            };

            bookmark.UpdateFromSnapshot(snapshot, now);
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

            string targetPath = BookmarkIdentity.NormalizeFolderPath(folderPath);
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                return false;
            }

            string prefix = string.Concat(targetPath, "/");
            int beforeBookmarkCount = state.Bookmarks.Count;
            state.Bookmarks.RemoveAll(item =>
            {
                string group = BookmarkIdentity.NormalizeFolderPath(item.Group);
                return string.Equals(group, targetPath, StringComparison.OrdinalIgnoreCase)
                    || group.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            });

            int beforeFolderCount = state.FolderPaths.Count;
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

            string sourcePath = BookmarkIdentity.NormalizeFolderPath(sourceFolderPath);
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return false;
            }

            string parentPath = GetParentFolderPath(sourcePath);
            string renamedPath = string.IsNullOrEmpty(parentPath)
                ? BookmarkIdentity.NormalizeFolderPath(targetFolderName)
                : string.Concat(parentPath, "/", BookmarkIdentity.NormalizeFolderPath(targetFolderName));

            if (string.IsNullOrWhiteSpace(renamedPath) || string.Equals(sourcePath, renamedPath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string sourcePrefix = string.Concat(sourcePath, "/");
            List<string> affectedFolders = state.FolderPaths
                .Where(path => string.Equals(path, sourcePath, StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (string folder in affectedFolders)
            {
                state.FolderPaths.Remove(folder);
            }

            foreach (string folder in affectedFolders)
            {
                string suffix = folder.Length == sourcePath.Length ? string.Empty : folder.Substring(sourcePath.Length);
                RegisterFolderPath(state.FolderPaths, string.Concat(renamedPath, suffix));
            }

            foreach (BookmarkMetadata bookmark in state.Bookmarks)
            {
                string group = BookmarkIdentity.NormalizeFolderPath(bookmark.Group);
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
            string normalizedFolderPath = BookmarkIdentity.NormalizeFolderPath(folderPath);
            bookmark.Group = normalizedFolderPath;
            RegisterFolderPath(state.FolderPaths, normalizedFolderPath);
        }

        public static void MoveFolder(BookmarkWorkspaceState state, string sourceFolderPath, string targetFolderPath)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            string sourcePath = BookmarkIdentity.NormalizeFolderPath(sourceFolderPath);
            string targetPath = BookmarkIdentity.NormalizeFolderPath(targetFolderPath);

            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("Source folder path cannot be empty.", nameof(sourceFolderPath));
            }

            if (string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string sourcePrefix = sourcePath + "/";

            // Update folder paths
            List<string> affectedFolders = state.FolderPaths
                .Where(path => string.Equals(path, sourcePath, StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (string folder in affectedFolders)
            {
                state.FolderPaths.Remove(folder);
            }

            foreach (string folder in affectedFolders)
            {
                if (string.Equals(folder, sourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    RegisterFolderPath(state.FolderPaths, targetPath);
                }
                else
                {
                    string suffix = folder.Substring(sourcePath.Length);
                    RegisterFolderPath(state.FolderPaths, targetPath + suffix);
                }
            }

            // Update bookmark groups
            foreach (BookmarkMetadata bookmark in state.Bookmarks)
            {
                string group = BookmarkIdentity.NormalizeFolderPath(bookmark.Group);
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
                .OrderBy(item => item.SlotNumber.HasValue ? 0 : 1)
                .ThenBy(item => item.SlotNumber ?? int.MaxValue)
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
        /// Returns the lowest available slot number (1-9), or null if all slots are occupied.
        /// </summary>
        internal static int? FindNextAvailableSlot(IEnumerable<BookmarkMetadata> bookmarks)
        {
            HashSet<int> usedSlots = new HashSet<int>(bookmarks.Where(b => b.SlotNumber.HasValue).Select(b => b.SlotNumber.GetValueOrDefault()));

            for (int slot = 1; slot <= 9; slot++)
            {
                if (!usedSlots.Contains(slot))
                {
                    return slot;
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
            HashSet<int> usedNumbers = new HashSet<int>();

            foreach (BookmarkMetadata bookmark in bookmarks)
            {
                string label = bookmark.Label ?? string.Empty;
                if (!label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string suffix = label.Substring(prefix.Length);
                if (int.TryParse(suffix, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int number) && number > 0)
                {
                    usedNumbers.Add(number);
                }
            }

            int next = 1;
            while (usedNumbers.Contains(next))
            {
                next++;
            }

            return string.Concat(prefix, next.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        private static string GetParentFolderPath(string folderPath)
        {
            string normalized = BookmarkIdentity.NormalizeFolderPath(folderPath);
            int separatorIndex = normalized.LastIndexOf('/');
            return separatorIndex <= 0
                ? string.Empty
                : normalized.Substring(0, separatorIndex);
        }

        private static void RegisterFolderPath(ISet<string> folderPaths, string? folderPath)
        {
            string normalized = BookmarkIdentity.NormalizeFolderPath(folderPath);
            folderPaths.Add(normalized);

            if (string.IsNullOrEmpty(normalized))
            {
                return;
            }

            string[] segments = normalized.Split('/');
            string current = string.Empty;
            foreach (string segment in segments)
            {
                current = string.IsNullOrEmpty(current)
                    ? segment
                    : string.Concat(current, "/", segment);

                folderPaths.Add(current);
            }
        }
    }
}
