using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace BookmarkStudio
{
    internal sealed class BookmarkMetadataStore
    {
        private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";
        private const string RootPropertyName = "root";
        private const string BookmarksPropertyName = "_bookmarks";

        public async Task<IReadOnlyList<BookmarkMetadata>> LoadAsync(string solutionPath, CancellationToken cancellationToken)
            => (await LoadWorkspaceAsync(solutionPath, cancellationToken)).Bookmarks;

        public async Task<BookmarkWorkspaceState> LoadWorkspaceAsync(string solutionPath, CancellationToken cancellationToken)
        {
            string storagePath = GetStoragePath(solutionPath);
            if (!File.Exists(storagePath))
            {
                return new BookmarkWorkspaceState();
            }

            string json = await Task.Run(() => File.ReadAllText(storagePath, Encoding.UTF8), cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new BookmarkWorkspaceState();
            }

            string solutionDirectory = GetSolutionDirectory(solutionPath);
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement rootElement = document.RootElement;

            BookmarkWorkspaceState state = new BookmarkWorkspaceState();

            if (TryGetObjectProperty(rootElement, RootPropertyName, out JsonElement rootFolderElement))
            {
                ParseFolderNode(state, rootFolderElement, string.Empty, solutionDirectory, cancellationToken);
                return NormalizeState(state);
            }

            if (TryGetObjectProperty(rootElement, "bookmarks", out JsonElement legacyBookmarks) && legacyBookmarks.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in legacyBookmarks.EnumerateArray())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    BookmarkMetadata metadata = Normalize(ToMetadata(item, solutionDirectory, string.Empty));
                    state.Bookmarks.Add(metadata);
                    RegisterFolderPath(state.FolderPaths, metadata.Group);
                }
            }

            return NormalizeState(state);
        }

        public Task SaveAsync(string solutionPath, IEnumerable<BookmarkMetadata> metadata, CancellationToken cancellationToken)
        {
            BookmarkWorkspaceState state = new BookmarkWorkspaceState();
            foreach (BookmarkMetadata item in metadata)
            {
                BookmarkMetadata normalized = Normalize(item);
                state.Bookmarks.Add(normalized);
                RegisterFolderPath(state.FolderPaths, normalized.Group);
            }

            return SaveWorkspaceAsync(solutionPath, state, cancellationToken);
        }

        public async Task SaveWorkspaceAsync(string solutionPath, BookmarkWorkspaceState state, CancellationToken cancellationToken)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            string storagePath = GetStoragePath(solutionPath);
            string? directory = Path.GetDirectoryName(storagePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException("Bookmark storage directory could not be determined.");
            }

            Directory.CreateDirectory(directory);

            string solutionDirectory = GetSolutionDirectory(solutionPath);
            FolderNode root = BuildFolderTree(state);

            string json = await Task.Run(() => SerializeTree(root, solutionDirectory), cancellationToken);
            await Task.Run(() => File.WriteAllText(storagePath, json, Encoding.UTF8), cancellationToken);
        }

        public string GetStoragePath(string solutionPath)
        {
            if (string.IsNullOrWhiteSpace(solutionPath))
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(localAppData, "BookmarkStudio", "Transient", "bookmarks.json");
            }

            string solutionDirectory = Path.GetDirectoryName(solutionPath) ?? string.Empty;
            return Path.Combine(solutionDirectory, ".vs", "bookmarks.json");
        }

        private static BookmarkWorkspaceState NormalizeState(BookmarkWorkspaceState state)
        {
            RegisterFolderPath(state.FolderPaths, string.Empty);

            foreach (BookmarkMetadata bookmark in state.Bookmarks)
            {
                bookmark.Group = BookmarkIdentity.NormalizeFolderPath(bookmark.Group);
                RegisterFolderPath(state.FolderPaths, bookmark.Group);
            }

            return state;
        }

        private static void ParseFolderNode(BookmarkWorkspaceState state, JsonElement folderElement, string folderPath, string solutionDirectory, CancellationToken cancellationToken)
        {
            RegisterFolderPath(state.FolderPaths, folderPath);

            foreach (JsonProperty property in folderElement.EnumerateObject())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (string.Equals(property.Name, BookmarksPropertyName, StringComparison.Ordinal))
                {
                    if (property.Value.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (JsonElement bookmarkElement in property.Value.EnumerateArray())
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        BookmarkMetadata metadata = Normalize(ToMetadata(bookmarkElement, solutionDirectory, folderPath));
                        state.Bookmarks.Add(metadata);
                    }

                    continue;
                }

                if (property.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                string childPath = string.IsNullOrEmpty(folderPath)
                    ? property.Name
                    : string.Concat(folderPath, "/", property.Name);

                ParseFolderNode(state, property.Value, BookmarkIdentity.NormalizeFolderPath(childPath), solutionDirectory, cancellationToken);
            }
        }

        private static BookmarkMetadata ToMetadata(JsonElement element, string solutionDirectory, string folderPath)
        {
            string documentPath = GetStringProperty(element, "documentPath");
            int lineNumber = GetIntProperty(element, "lineNumber");

            return new BookmarkMetadata
            {
                BookmarkId = GetStringProperty(element, "id"),
                DocumentPath = MakeAbsolutePath(documentPath, solutionDirectory),
                LineNumber = lineNumber,
                LineText = GetStringProperty(element, "lineText"),
                SlotNumber = GetNullableIntProperty(element, "slotNumber"),
                Label = GetStringProperty(element, "label"),
                Group = !string.IsNullOrWhiteSpace(GetStringProperty(element, "group"))
                    ? GetStringProperty(element, "group")
                    : folderPath,
                Color = GetColorProperty(element, "color"),
                CreatedUtc = GetDateTimeProperty(element, "createdUtc"),
                LastVisitedUtc = GetNullableDateTimeProperty(element, "lastVisitedUtc"),
                LastSeenUtc = GetDateTimeProperty(element, "lastSeenUtc"),
            };
        }

        private static string SerializeTree(FolderNode root, string solutionDirectory)
        {
            using MemoryStream stream = new MemoryStream();
            using (Utf8JsonWriter writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WritePropertyName(RootPropertyName);
                WriteFolderNode(writer, root, solutionDirectory);
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static void WriteFolderNode(Utf8JsonWriter writer, FolderNode folderNode, string solutionDirectory)
        {
            writer.WriteStartObject();

            writer.WritePropertyName(BookmarksPropertyName);
            writer.WriteStartArray();

            foreach (BookmarkMetadata bookmark in folderNode.Bookmarks
                .OrderBy(item => item.DocumentPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.LineNumber)
                .ThenBy(item => item.BookmarkId, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("id", bookmark.BookmarkId);
                writer.WriteString("documentPath", MakeRelativePath(bookmark.DocumentPath, solutionDirectory));
                writer.WriteNumber("lineNumber", bookmark.LineNumber);
                writer.WriteString("lineText", bookmark.LineText ?? string.Empty);

                if (bookmark.SlotNumber.HasValue)
                {
                    writer.WriteNumber("slotNumber", bookmark.SlotNumber.Value);
                }

                if (!string.IsNullOrWhiteSpace(bookmark.Label))
                {
                    writer.WriteString("label", bookmark.Label);
                }

                if (bookmark.Color != BookmarkColor.None)
                {
                    writer.WriteString("color", bookmark.Color.ToString().ToLowerInvariant());
                }

                if (bookmark.CreatedUtc != default)
                {
                    writer.WriteString("createdUtc", bookmark.CreatedUtc.ToString(DateTimeFormat, CultureInfo.InvariantCulture));
                }

                if (bookmark.LastVisitedUtc.HasValue)
                {
                    writer.WriteString("lastVisitedUtc", bookmark.LastVisitedUtc.Value.ToString(DateTimeFormat, CultureInfo.InvariantCulture));
                }

                if (bookmark.LastSeenUtc != default)
                {
                    writer.WriteString("lastSeenUtc", bookmark.LastSeenUtc.ToString(DateTimeFormat, CultureInfo.InvariantCulture));
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            foreach (KeyValuePair<string, FolderNode> child in folderNode.Children.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
            {
                writer.WritePropertyName(child.Key);
                WriteFolderNode(writer, child.Value, solutionDirectory);
            }

            writer.WriteEndObject();
        }

        private static FolderNode BuildFolderTree(BookmarkWorkspaceState state)
        {
            FolderNode root = new FolderNode();

            foreach (string path in state.FolderPaths)
            {
                EnsureFolder(root, path);
            }

            foreach (BookmarkMetadata bookmark in state.Bookmarks)
            {
                BookmarkMetadata normalized = Normalize(bookmark);
                FolderNode folderNode = EnsureFolder(root, normalized.Group);
                folderNode.Bookmarks.Add(normalized);
            }

            return root;
        }

        private static FolderNode EnsureFolder(FolderNode root, string folderPath)
        {
            string normalizedPath = BookmarkIdentity.NormalizeFolderPath(folderPath);
            if (string.IsNullOrEmpty(normalizedPath))
            {
                return root;
            }

            FolderNode current = root;
            string[] segments = normalizedPath.Split('/');
            foreach (string segment in segments)
            {
                if (!current.Children.TryGetValue(segment, out FolderNode? child))
                {
                    child = new FolderNode();
                    current.Children[segment] = child;
                }

                current = child;
            }

            return current;
        }

        private static void RegisterFolderPath(ISet<string> folderPaths, string folderPath)
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

        private static bool TryGetObjectProperty(JsonElement element, string propertyName, out JsonElement propertyValue)
        {
            propertyValue = default;

            if (element.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            return element.TryGetProperty(propertyName, out propertyValue);
        }

        private static string GetStringProperty(JsonElement element, string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out JsonElement property))
            {
                return string.Empty;
            }

            return property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? string.Empty
                : string.Empty;
        }

        private static int GetIntProperty(JsonElement element, string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out JsonElement property))
            {
                return 0;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out int value))
            {
                return value;
            }

            return 0;
        }

        private static int? GetNullableIntProperty(JsonElement element, string propertyName)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out JsonElement property))
            {
                return null;
            }

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out int value))
            {
                return value;
            }

            return null;
        }

        private static BookmarkColor GetColorProperty(JsonElement element, string propertyName)
        {
            string value = GetStringProperty(element, propertyName);
            if (Enum.TryParse(value, true, out BookmarkColor color))
            {
                return color;
            }

            return BookmarkColor.None;
        }

        private static DateTime GetDateTimeProperty(JsonElement element, string propertyName)
        {
            string value = GetStringProperty(element, propertyName);
            return ParseDateTime(value);
        }

        private static DateTime? GetNullableDateTimeProperty(JsonElement element, string propertyName)
        {
            string value = GetStringProperty(element, propertyName);
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            DateTime parsed = ParseDateTime(value);
            return parsed == default ? null : parsed;
        }

        private static DateTime ParseDateTime(string? value)
        {
            if (DateTime.TryParseExact(value, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime result))
            {
                return result;
            }

            return default;
        }

        private static BookmarkMetadata Normalize(BookmarkMetadata metadata)
        {
            DateTime now = DateTime.UtcNow;

            if (string.IsNullOrWhiteSpace(metadata.BookmarkId))
            {
                metadata.BookmarkId = Guid.NewGuid().ToString("N");
            }

            metadata.DocumentPath = metadata.DocumentPath ?? string.Empty;
            metadata.LineText = metadata.LineText ?? string.Empty;
            metadata.Label = metadata.Label ?? string.Empty;
            metadata.Group = BookmarkIdentity.NormalizeFolderPath(metadata.Group);

            if (metadata.CreatedUtc == default)
            {
                metadata.CreatedUtc = metadata.LastSeenUtc == default ? now : metadata.LastSeenUtc;
            }

            if (metadata.LastSeenUtc == default)
            {
                metadata.LastSeenUtc = metadata.CreatedUtc == default ? now : metadata.CreatedUtc;
            }

            return metadata;
        }

        private static string GetSolutionDirectory(string solutionPath)
        {
            if (string.IsNullOrWhiteSpace(solutionPath))
            {
                return string.Empty;
            }

            return Path.GetDirectoryName(solutionPath) ?? string.Empty;
        }

        private static string MakeRelativePath(string documentPath, string solutionDirectory)
        {
            if (string.IsNullOrWhiteSpace(solutionDirectory) || string.IsNullOrWhiteSpace(documentPath))
            {
                return documentPath ?? string.Empty;
            }

            string normalizedDir = solutionDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;

            if (documentPath.StartsWith(normalizedDir, StringComparison.OrdinalIgnoreCase))
            {
                return documentPath.Substring(normalizedDir.Length);
            }

            return documentPath;
        }

        private static string MakeAbsolutePath(string documentPath, string solutionDirectory)
        {
            if (string.IsNullOrWhiteSpace(solutionDirectory) || string.IsNullOrWhiteSpace(documentPath))
            {
                return documentPath ?? string.Empty;
            }

            if (Path.IsPathRooted(documentPath))
            {
                return documentPath;
            }

            return Path.Combine(solutionDirectory, documentPath);
        }

        private sealed class FolderNode
        {
            public Dictionary<string, FolderNode> Children { get; } = new Dictionary<string, FolderNode>(StringComparer.OrdinalIgnoreCase);

            public List<BookmarkMetadata> Bookmarks { get; } = new List<BookmarkMetadata>();
        }
    }
}
