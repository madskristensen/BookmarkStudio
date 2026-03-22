using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BookmarkStudio
{
    public enum BookmarkColor
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
        private const int MaxRepositoryRootCacheSize = 100;
        private static readonly object RepositoryRootCacheGate = new();
        private static readonly Dictionary<string, string?> RepositoryRootCache = new(StringComparer.OrdinalIgnoreCase);

        public static string NormalizeDocumentPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToUpperInvariant();
        }

        public static string NormalizeLineText(string? lineText)
            => (lineText ?? string.Empty).Trim();

        public static string CreateExactMatchKey(string documentPath, int lineNumber)
            => string.Concat(NormalizeDocumentPath(documentPath), "|", lineNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));

        public static string NormalizeFolderPath(string? folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                return string.Empty;
            }

            var normalized = folderPath!.Trim()
                .Replace('\\', '/');

            var segments = normalized.Split(['/'], StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return string.Empty;
            }

            if (string.Equals(segments[0], "Root", StringComparison.OrdinalIgnoreCase))
            {
                segments = [.. segments.Skip(1)];
            }

            if (segments.Length == 0)
            {
                return string.Empty;
            }

            return string.Join("/", segments);
        }

        public static string GetRepositoryRelativePath(string? documentPath)
        {
            if (string.IsNullOrWhiteSpace(documentPath))
            {
                return string.Empty;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(documentPath);
            }
            catch (ArgumentException)
            {
                return documentPath!;
            }
            catch (NotSupportedException)
            {
                return documentPath!;
            }

            var directoryPath = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return Path.GetFileName(fullPath);
            }

            var repositoryRoot = GetRepositoryRoot(directoryPath);
            if (string.IsNullOrWhiteSpace(repositoryRoot))
            {
                return Path.GetFileName(fullPath);
            }

            var rootWithSeparator = repositoryRoot!.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            if (!fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetFileName(fullPath);
            }

            var relativePath = fullPath.Substring(rootWithSeparator.Length);
            return string.Join("/", relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Where(segment => !string.IsNullOrWhiteSpace(segment)));
        }

        private static string? GetRepositoryRoot(string directoryPath)
        {
            var normalizedDirectoryPath = Path.GetFullPath(directoryPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            lock (RepositoryRootCacheGate)
            {
                if (RepositoryRootCache.TryGetValue(normalizedDirectoryPath, out var cachedRoot))
                {
                    return cachedRoot;
                }
            }

            var current = normalizedDirectoryPath;
            string? foundRoot = null;

            while (!string.IsNullOrWhiteSpace(current))
            {
                if (Directory.Exists(Path.Combine(current, ".git")))
                {
                    foundRoot = current;
                    break;
                }

                current = Path.GetDirectoryName(current.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }

            lock (RepositoryRootCacheGate)
            {
                // Evict oldest entries if cache is full
                if (RepositoryRootCache.Count >= MaxRepositoryRootCacheSize)
                {
                    // Remove approximately half of the entries to avoid frequent evictions
                    var entriesToRemove = RepositoryRootCache.Count / 2;
                    foreach (var key in RepositoryRootCache.Keys.Take(entriesToRemove).ToList())
                    {
                        RepositoryRootCache.Remove(key);
                    }
                }

                RepositoryRootCache[normalizedDirectoryPath] = foundRoot;
            }

            return foundRoot;
        }

    }

    internal sealed class BookmarkSnapshot
    {
        public string DocumentPath { get; set; } = string.Empty;

        public int LineNumber { get; set; }

        public string LineText { get; set; } = string.Empty;

        public string ExactMatchKey => BookmarkIdentity.CreateExactMatchKey(DocumentPath, LineNumber);

    }

    internal sealed class BookmarkMetadata
    {
        public string BookmarkId { get; set; } = Guid.NewGuid().ToString("N");

        public string DocumentPath { get; set; } = string.Empty;

        public int LineNumber { get; set; }

        public string LineText { get; set; } = string.Empty;

        public int? SlotNumber { get; set; }

        public string Label { get; set; } = string.Empty;

        public string Group { get; set; } = string.Empty;

        public string FolderPath
        {
            get => Group;
            set => Group = BookmarkIdentity.NormalizeFolderPath(value);
        }

        public BookmarkColor Color { get; set; }

        public BookmarkStorageLocation StorageLocation { get; set; }

        public DateTime CreatedUtc { get; set; }

        public string ExactMatchKey => BookmarkIdentity.CreateExactMatchKey(DocumentPath, LineNumber);

        public void UpdateFromSnapshot(BookmarkSnapshot snapshot)
        {
            if (snapshot is null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            DocumentPath = snapshot.DocumentPath;
            LineNumber = snapshot.LineNumber;
            LineText = snapshot.LineText;
        }

        public ManagedBookmark ToManagedBookmark()
        {
            return new ManagedBookmark
            {
                BookmarkId = BookmarkId,
                DocumentPath = DocumentPath,
                NormalizedDocumentPath = BookmarkIdentity.NormalizeDocumentPath(DocumentPath),
                LineNumber = LineNumber,
                LineText = (LineText ?? string.Empty).Trim(),
                SlotNumber = SlotNumber,
                Label = Label,
                Group = Group,
                Color = Color,
                StorageLocation = StorageLocation,
            };
        }
    }

    public sealed class ManagedBookmark
    {
        public string BookmarkId { get; set; } = string.Empty;

        public string DocumentPath { get; set; } = string.Empty;

        public string NormalizedDocumentPath { get; set; } = string.Empty;

        public int LineNumber { get; set; }

        public string LineText { get; set; } = string.Empty;

        public int? SlotNumber { get; set; }

        public string Label { get; set; } = string.Empty;

        public string Group { get; set; } = string.Empty;

        public string FolderPath
        {
            get => Group;
            set => Group = BookmarkIdentity.NormalizeFolderPath(value);
        }

        public BookmarkColor Color { get; set; }

        public BookmarkStorageLocation StorageLocation { get; set; }

        public string FileName => Path.GetFileName(DocumentPath);

        public string RepositoryRelativePath => BookmarkIdentity.GetRepositoryRelativePath(DocumentPath);

        public string Location => string.IsNullOrWhiteSpace(DocumentPath)
            ? string.Empty
            : string.Concat(DocumentPath, "(", LineNumber.ToString(System.Globalization.CultureInfo.InvariantCulture), ")");

    }

    internal sealed class BookmarkWorkspaceState
    {
        public List<BookmarkMetadata> Bookmarks { get; } = new List<BookmarkMetadata>();

        public HashSet<string> FolderPaths { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public HashSet<string> ExpandedFolders { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    internal sealed class DualBookmarkWorkspaceState
    {
        private IReadOnlyList<BookmarkMetadata>? _cachedAllBookmarks;
        private IReadOnlyList<string>? _cachedAllFolderPaths;

        public DualBookmarkWorkspaceState(BookmarkWorkspaceState personalState, BookmarkWorkspaceState solutionState)
        {
            PersonalState = personalState ?? new BookmarkWorkspaceState();
            SolutionState = solutionState ?? new BookmarkWorkspaceState();
        }

        public BookmarkWorkspaceState PersonalState { get; }

        public BookmarkWorkspaceState SolutionState { get; }

        public IReadOnlyList<BookmarkMetadata> AllBookmarks
        {
            get
            {
                if (_cachedAllBookmarks is null)
                {
                    List<BookmarkMetadata> all = new List<BookmarkMetadata>();
                    all.AddRange(PersonalState.Bookmarks);
                    all.AddRange(SolutionState.Bookmarks);
                    _cachedAllBookmarks = all;
                }

                return _cachedAllBookmarks;
            }
        }

        public IReadOnlyList<string> AllFolderPaths
        {
            get
            {
                if (_cachedAllFolderPaths is null)
                {
                    HashSet<string> all = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var path in PersonalState.FolderPaths)
                    {
                        all.Add(path);
                    }

                    foreach (var path in SolutionState.FolderPaths)
                    {
                        all.Add(path);
                    }

                    _cachedAllFolderPaths = all.ToArray();
                }

                return _cachedAllFolderPaths;
            }
        }
    }
}