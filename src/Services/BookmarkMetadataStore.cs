using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace BookmarkStudio
{
    internal sealed class BookmarkMetadataStore
    {
        private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

        private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

        public async Task<IReadOnlyList<BookmarkMetadata>> LoadAsync(string solutionPath, CancellationToken cancellationToken)
        {
            string storagePath = GetStoragePath(solutionPath);
            if (!File.Exists(storagePath))
            {
                return Array.Empty<BookmarkMetadata>();
            }

            string json = await Task.Run(() => File.ReadAllText(storagePath, Encoding.UTF8), cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                return Array.Empty<BookmarkMetadata>();
            }

            BookmarkFile? file = JsonSerializer.Deserialize<BookmarkFile>(json, SerializerOptions);
            if (file?.Bookmarks is null)
            {
                return Array.Empty<BookmarkMetadata>();
            }

            string solutionDirectory = GetSolutionDirectory(solutionPath);

            List<BookmarkMetadata> metadata = new List<BookmarkMetadata>();
            foreach (BookmarkEntry entry in file.Bookmarks)
            {
                cancellationToken.ThrowIfCancellationRequested();
                metadata.Add(Normalize(ToMetadata(entry, solutionDirectory)));
            }

            return metadata;
        }

        public async Task SaveAsync(string solutionPath, IEnumerable<BookmarkMetadata> metadata, CancellationToken cancellationToken)
        {
            string storagePath = GetStoragePath(solutionPath);
            string? directory = Path.GetDirectoryName(storagePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException("Bookmark storage directory could not be determined.");
            }

            Directory.CreateDirectory(directory);

            string solutionDirectory = GetSolutionDirectory(solutionPath);

            BookmarkEntry[] entries = metadata
                .OrderBy(item => item.DocumentPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.LineNumber)
                .ThenBy(item => item.BookmarkId, StringComparer.Ordinal)
                .Select(item => ToEntry(item, solutionDirectory))
                .ToArray();

            BookmarkFile file = new BookmarkFile { Bookmarks = entries };
            string json = JsonSerializer.Serialize(file, SerializerOptions);

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

        private static JsonSerializerOptions CreateSerializerOptions()
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            options.Converters.Add(new UtcDateTimeConverter());
            return options;
        }

        private static BookmarkEntry ToEntry(BookmarkMetadata metadata, string solutionDirectory)
        {
            return new BookmarkEntry
            {
                Id = metadata.BookmarkId,
                DocumentPath = MakeRelativePath(metadata.DocumentPath, solutionDirectory),
                LineNumber = metadata.LineNumber,
                ColumnNumber = metadata.ColumnNumber,
                LineText = metadata.LineText,
                SlotNumber = metadata.SlotNumber,
                Label = metadata.Label,
                Group = metadata.Group,
                Color = metadata.Color,
                CreatedUtc = metadata.CreatedUtc,
                LastVisitedUtc = metadata.LastVisitedUtc,
                LastSeenUtc = metadata.LastSeenUtc,
            };
        }

        private static BookmarkMetadata ToMetadata(BookmarkEntry entry, string solutionDirectory)
        {
            return new BookmarkMetadata
            {
                BookmarkId = entry.Id ?? string.Empty,
                DocumentPath = MakeAbsolutePath(entry.DocumentPath ?? string.Empty, solutionDirectory),
                LineNumber = entry.LineNumber,
                ColumnNumber = entry.ColumnNumber,
                LineText = entry.LineText ?? string.Empty,
                SlotNumber = entry.SlotNumber,
                Label = entry.Label ?? string.Empty,
                Group = entry.Group ?? string.Empty,
                Color = entry.Color,
                CreatedUtc = entry.CreatedUtc,
                LastVisitedUtc = entry.LastVisitedUtc,
                LastSeenUtc = entry.LastSeenUtc,
            };
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
            metadata.Group = metadata.Group ?? string.Empty;

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

        private sealed class BookmarkFile
        {
            [JsonPropertyName("bookmarks")]
            public BookmarkEntry[] Bookmarks { get; set; } = Array.Empty<BookmarkEntry>();
        }

        private sealed class BookmarkEntry
        {
            public string? Id { get; set; }
            public string? DocumentPath { get; set; }
            public int LineNumber { get; set; }
            public int ColumnNumber { get; set; }
            public string? LineText { get; set; }
            public int? SlotNumber { get; set; }
            public string? Label { get; set; }
            public string? Group { get; set; }
            public BookmarkColor Color { get; set; }
            public DateTime CreatedUtc { get; set; }
            public DateTime? LastVisitedUtc { get; set; }
            public DateTime LastSeenUtc { get; set; }
        }

        private sealed class UtcDateTimeConverter : JsonConverter<DateTime>
        {
            public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                string? value = reader.GetString();
                if (DateTime.TryParseExact(value, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime result))
                {
                    return result;
                }

                return default;
            }

            public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.ToString(DateTimeFormat, CultureInfo.InvariantCulture));
            }
        }
    }
}