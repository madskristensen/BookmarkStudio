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

        public Task SaveAsync(string solutionPath, IEnumerable<BookmarkMetadata> bookmarks, CancellationToken cancellationToken)
            => _store.SaveAsync(solutionPath, bookmarks, cancellationToken);

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

            DateTime now = DateTime.UtcNow;
            BookmarkMetadata createdBookmark = new BookmarkMetadata
            {
                BookmarkId = Guid.NewGuid().ToString("N"),
                CreatedUtc = now,
                SlotNumber = FindNextAvailableSlot(bookmarks),
            };

            createdBookmark.UpdateFromSnapshot(snapshot, now);
            bookmarks.Add(createdBookmark);
            await SaveAsync(solutionPath, bookmarks, cancellationToken);
            return createdBookmark.ToManagedBookmark();
        }

        public static BookmarkMetadata GetRequiredBookmark(IEnumerable<BookmarkMetadata> bookmarks, string bookmarkId)
            => bookmarks.FirstOrDefault(item => string.Equals(item.BookmarkId, bookmarkId, StringComparison.Ordinal))
                ?? throw new InvalidOperationException("Bookmark metadata could not be located.");

        public static BookmarkMetadata FindBySnapshot(IEnumerable<BookmarkMetadata> bookmarks, BookmarkSnapshot snapshot)
            => bookmarks.FirstOrDefault(item => string.Equals(item.ExactMatchKey, snapshot.ExactMatchKey, StringComparison.Ordinal));

        public static IReadOnlyList<ManagedBookmark> ToManagedBookmarks(IEnumerable<BookmarkMetadata> bookmarks)
            => bookmarks.Select(item => item.ToManagedBookmark())
                .OrderBy(item => item.SlotNumber.HasValue ? 0 : 1)
                .ThenBy(item => item.SlotNumber ?? int.MaxValue)
                .ThenBy(item => item.DocumentPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.LineNumber)
                .ToArray();

        /// <summary>
        /// Returns the lowest available slot number (1-9), or null if all slots are occupied.
        /// </summary>
        private static int? FindNextAvailableSlot(IEnumerable<BookmarkMetadata> bookmarks)
        {
            HashSet<int> usedSlots = new HashSet<int>(bookmarks.Where(b => b.SlotNumber.HasValue).Select(b => b.SlotNumber.Value));

            for (int slot = 1; slot <= 9; slot++)
            {
                if (!usedSlots.Contains(slot))
                {
                    return slot;
                }
            }

            return null;
        }
    }
}
