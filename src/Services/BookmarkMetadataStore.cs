using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace BookmarkStudio
{
    internal sealed class BookmarkMetadataStore
    {
        private const string FileHeader = "BookmarkStudio|2";
        private const string LegacyFileHeader = "BookmarkStudio|1";
        private const char Separator = '\t';

        public async Task<IReadOnlyList<BookmarkMetadata>> LoadAsync(string solutionPath, CancellationToken cancellationToken)
        {
            string storagePath = GetStoragePath(solutionPath);
            if (!File.Exists(storagePath))
            {
                return Array.Empty<BookmarkMetadata>();
            }

            string[] lines = await Task.Run(() => File.ReadAllLines(storagePath), cancellationToken);
            if (lines.Length == 0)
            {
                return Array.Empty<BookmarkMetadata>();
            }

            string header = lines[0];
            if (!string.Equals(header, FileHeader, StringComparison.Ordinal) && !string.Equals(header, LegacyFileHeader, StringComparison.Ordinal))
            {
                return Array.Empty<BookmarkMetadata>();
            }

            List<BookmarkMetadata> metadata = new List<BookmarkMetadata>();
            for (int i = 1; i < lines.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                BookmarkMetadata? entry = TryParse(lines[i]);
                if (entry is not null)
                {
                    metadata.Add(Normalize(entry));
                }
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

            string[] lines = new[] { FileHeader }
                .Concat(metadata.OrderBy(item => item.DocumentPath, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => item.LineNumber)
                    .ThenBy(item => item.BookmarkId, StringComparer.Ordinal)
                    .Select(Serialize))
                .ToArray();

            await Task.Run(() => File.WriteAllLines(storagePath, lines, Encoding.UTF8), cancellationToken);
        }

        public string GetStoragePath(string solutionPath)
        {
            if (string.IsNullOrWhiteSpace(solutionPath))
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(localAppData, "BookmarkStudio", "Transient", "bookmark-metadata.dat");
            }

            string solutionDirectory = Path.GetDirectoryName(solutionPath) ?? string.Empty;
            string solutionName = Path.GetFileNameWithoutExtension(solutionPath);
            return Path.Combine(solutionDirectory, ".vs", solutionName, "BookmarkStudio", "bookmark-metadata.dat");
        }

        private static string Serialize(BookmarkMetadata metadata)
        {
            string[] values =
            {
                Encode(metadata.BookmarkId),
                Encode(metadata.DocumentPath),
                metadata.LineNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
                metadata.ColumnNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Encode(metadata.LineText),
                metadata.SlotNumber?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                Encode(metadata.Label),
                Encode(metadata.Group),
                metadata.CreatedUtc.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture),
                metadata.LastVisitedUtc?.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                metadata.LastSeenUtc.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture),
            };

            return string.Join(Separator.ToString(), values);
        }

        private static BookmarkMetadata? TryParse(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return null;
            }

            string[] values = line.Split(Separator);
            if (values.Length != 11)
            {
                return null;
            }

            if (!int.TryParse(values[2], out int lineNumber) || !int.TryParse(values[3], out int columnNumber))
            {
                return null;
            }

            if (!long.TryParse(values[8], out long createdTicks) || !long.TryParse(values[10], out long lastSeenTicks))
            {
                return null;
            }

            DateTime? lastVisitedUtc = null;
            if (!string.IsNullOrWhiteSpace(values[9]))
            {
                if (!long.TryParse(values[9], out long lastVisitedTicks))
                {
                    return null;
                }

                lastVisitedUtc = new DateTime(lastVisitedTicks, DateTimeKind.Utc);
            }

            int? slotNumber = null;
            if (!string.IsNullOrWhiteSpace(values[5]))
            {
                if (!int.TryParse(values[5], out int slotValue))
                {
                    return null;
                }

                slotNumber = slotValue;
            }

            return new BookmarkMetadata
            {
                BookmarkId = Decode(values[0]),
                DocumentPath = Decode(values[1]),
                LineNumber = lineNumber,
                ColumnNumber = columnNumber,
                LineText = Decode(values[4]),
                SlotNumber = slotNumber,
                Label = Decode(values[6]),
                Group = Decode(values[7]),
                CreatedUtc = new DateTime(createdTicks, DateTimeKind.Utc),
                LastVisitedUtc = lastVisitedUtc,
                LastSeenUtc = new DateTime(lastSeenTicks, DateTimeKind.Utc),
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

        private static string Encode(string? value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            return Convert.ToBase64String(bytes);
        }

        private static string Decode(string value)
        {
            byte[] bytes = Convert.FromBase64String(value);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}