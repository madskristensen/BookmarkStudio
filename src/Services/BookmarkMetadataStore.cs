using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace BookmarkStudio
{
    public enum BookmarkStorageLocation
    {
        Global,
        Personal,
        Workspace,
    }

    internal sealed class BookmarkStorageInfo
    {
        public BookmarkStorageInfo(BookmarkStorageLocation location, string absolutePath, string relativePath)
        {
            Location = location;
            AbsolutePath = absolutePath;
            RelativePath = relativePath;
        }

        public BookmarkStorageLocation Location { get; }

        public string AbsolutePath { get; }

        public string RelativePath { get; }
    }

    internal sealed class BookmarkMetadataStore
    {
        private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";
        private const string RootPropertyName = "root";
        private const string DocumentPathRootPropertyName = "documentPathRoot";
        private const string BookmarksFileDocumentPathRoot = "bookmarksFile";
        private const string BookmarksPropertyName = "_bookmarks";
        private const string BookmarksFileName = ".bookmarks.json";
        private const string LegacyBookmarksFileName = "bookmarks.json";

        public async Task<IReadOnlyList<BookmarkMetadata>> LoadAsync(string solutionPath, CancellationToken cancellationToken)
            => (await LoadWorkspaceAsync(solutionPath, cancellationToken)).Bookmarks;

        public async Task<BookmarkWorkspaceState> LoadWorkspaceAsync(string solutionPath, CancellationToken cancellationToken)
        {
            BookmarkStorageInfo storageInfo = GetStorageInfo(solutionPath);
            var storagePath = storageInfo.AbsolutePath;
            if (!File.Exists(storagePath))
            {
                return new BookmarkWorkspaceState();
            }

            var json = await Task.Run(() => File.ReadAllText(storagePath, Encoding.UTF8), cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new BookmarkWorkspaceState();
            }

            try
            {
                return ParseWorkspaceJson(json, GetSolutionDirectory(solutionPath), GetStorageDirectory(storagePath), cancellationToken);
            }
            catch (JsonException ex)
            {
                await ex.LogAsync();
                return new BookmarkWorkspaceState();
            }
        }

        private BookmarkWorkspaceState ParseWorkspaceJson(string json, string solutionDirectory, string storageDirectory, CancellationToken cancellationToken)
        {
            using var document = JsonDocument.Parse(json);
            JsonElement rootElement = document.RootElement;
            var documentPathBaseDirectory = GetDocumentPathBaseDirectory(rootElement, solutionDirectory, storageDirectory);

            var state = new BookmarkWorkspaceState();

            if (TryGetObjectProperty(rootElement, RootPropertyName, out JsonElement rootFolderElement))
            {
                ParseFolderNode(state, rootFolderElement, string.Empty, documentPathBaseDirectory, cancellationToken);
                ParseExpandedFolders(state, rootElement);
                return NormalizeState(state);
            }

            if (TryGetObjectProperty(rootElement, "bookmarks", out JsonElement legacyBookmarks) && legacyBookmarks.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in legacyBookmarks.EnumerateArray())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    BookmarkMetadata metadata = Normalize(ToMetadata(item, documentPathBaseDirectory, string.Empty));
                    state.Bookmarks.Add(metadata);
                    RegisterFolderPath(state.FolderPaths, metadata.Group);
                }
            }

            return NormalizeState(state);
        }

        public async Task SaveAsync(string solutionPath, IEnumerable<BookmarkMetadata> metadata, CancellationToken cancellationToken)
        {
            BookmarkWorkspaceState state = await LoadWorkspaceAsync(solutionPath, cancellationToken);
            state.Bookmarks.Clear();

            foreach (BookmarkMetadata item in metadata)
            {
                BookmarkMetadata normalized = Normalize(item);
                state.Bookmarks.Add(normalized);
                RegisterFolderPath(state.FolderPaths, normalized.Group);
            }

            await SaveWorkspaceAsync(solutionPath, state, cancellationToken);
        }

        public async Task SaveWorkspaceAsync(string solutionPath, BookmarkWorkspaceState state, CancellationToken cancellationToken)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            BookmarkStorageInfo storageInfo = GetStorageInfo(solutionPath);
            var storagePath = storageInfo.AbsolutePath;

            // If the state is empty (no bookmarks and only the root folder), delete the file instead of persisting an empty state
            if (IsEmptyState(state))
            {
                if (File.Exists(storagePath))
                {
                    await Task.Run(() => File.Delete(storagePath), cancellationToken);
                }

                return;
            }

            var directory = Path.GetDirectoryName(storagePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException("Bookmark storage directory could not be determined.");
            }

            Directory.CreateDirectory(directory);

            FolderNode root = BuildFolderTree(state, state.ExpandedFolders);

            var documentPathBaseDirectory = storageInfo.Location == BookmarkStorageLocation.Workspace
                ? GetStorageDirectory(storagePath)
                : GetSolutionDirectory(solutionPath);
            var json = await Task.Run(() => SerializeTree(root, documentPathBaseDirectory, storageInfo.Location == BookmarkStorageLocation.Workspace), cancellationToken);
            await Task.Run(() => File.WriteAllText(storagePath, json, Encoding.UTF8), cancellationToken);
        }

        /// <summary>
        /// Determines whether the workspace state is empty (no bookmarks and no user-created folders).
        /// </summary>
        private static bool IsEmptyState(BookmarkWorkspaceState state)
        {
            // State is empty if there are no bookmarks
            if (state.Bookmarks.Count > 0)
            {
                return false;
            }

            // State is empty if there are no folders, or only the root folder (empty string)
            if (state.FolderPaths.Count == 0)
            {
                return true;
            }

            // If there's only one folder path and it's the root (empty string), state is empty
            if (state.FolderPaths.Count == 1 && state.FolderPaths.Contains(string.Empty))
            {
                return true;
            }

            return false;
        }

        public string GetStoragePath(string solutionPath)
            => GetStorageInfo(solutionPath).AbsolutePath;

        public BookmarkStorageInfo GetStorageInfo(string solutionPath)
        {
            if (string.IsNullOrWhiteSpace(solutionPath))
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var transientPath = Path.Combine(localAppData, "BookmarkStudio", "Transient", BookmarksFileName);
                return new BookmarkStorageInfo(BookmarkStorageLocation.Personal, transientPath, BookmarksFileName);
            }

            var solutionDirectory = Path.GetDirectoryName(solutionPath) ?? string.Empty;
            var repoRoot = GetRepositoryRoot(solutionDirectory);
            var baseDirectory = repoRoot ?? solutionDirectory;

            // Check solution/repo root first for team-shared bookmarks (new name, then legacy)
            var solutionRootPath = Path.Combine(baseDirectory, BookmarksFileName);
            if (File.Exists(solutionRootPath))
            {
                return new BookmarkStorageInfo(BookmarkStorageLocation.Workspace, solutionRootPath, BookmarksFileName);
            }

            var legacySolutionRootPath = Path.Combine(baseDirectory, LegacyBookmarksFileName);
            if (File.Exists(legacySolutionRootPath))
            {
                return new BookmarkStorageInfo(BookmarkStorageLocation.Workspace, legacySolutionRootPath, LegacyBookmarksFileName);
            }

            // Check .vs folder for user-specific bookmarks (new name, then legacy)
            var personalPath = Path.Combine(solutionDirectory, ".vs", BookmarksFileName);
            if (File.Exists(personalPath))
            {
                return new BookmarkStorageInfo(BookmarkStorageLocation.Personal, personalPath, ".vs/" + BookmarksFileName);
            }

            var legacyPersonalPath = Path.Combine(solutionDirectory, ".vs", LegacyBookmarksFileName);
            if (File.Exists(legacyPersonalPath))
            {
                return new BookmarkStorageInfo(BookmarkStorageLocation.Personal, legacyPersonalPath, ".vs/" + LegacyBookmarksFileName);
            }

            // Default to personal location with new name
            return new BookmarkStorageInfo(BookmarkStorageLocation.Personal, personalPath, ".vs/" + BookmarksFileName);
        }

        public string GetSolutionStoragePath(string solutionPath)
        {
            if (string.IsNullOrWhiteSpace(solutionPath))
            {
                throw new InvalidOperationException("Cannot determine solution storage path without a solution.");
            }

            var solutionDirectory = Path.GetDirectoryName(solutionPath) ?? string.Empty;
            var repoRoot = GetRepositoryRoot(solutionDirectory);
            var baseDirectory = repoRoot ?? solutionDirectory;
            return Path.Combine(baseDirectory, BookmarksFileName);
        }

        public string GetPersonalStoragePath(string solutionPath)
        {
            if (string.IsNullOrWhiteSpace(solutionPath))
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(localAppData, "BookmarkStudio", "Transient", BookmarksFileName);
            }

            var solutionDirectory = Path.GetDirectoryName(solutionPath) ?? string.Empty;
            return Path.Combine(solutionDirectory, ".vs", BookmarksFileName);
        }

        public string GetGlobalStoragePath()
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(userProfile, BookmarksFileName);
        }

        public string GetStoragePathForLocation(string solutionPath, BookmarkStorageLocation location)
        {
            return location switch
            {
                BookmarkStorageLocation.Global => GetGlobalStoragePath(),
                BookmarkStorageLocation.Workspace => GetSolutionStoragePath(solutionPath),
                _ => GetPersonalStoragePath(solutionPath),
            };
        }

        public async Task<BookmarkStorageInfo> MoveToLocationAsync(string solutionPath, BookmarkStorageLocation targetLocation, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(solutionPath))
            {
                throw new InvalidOperationException("Cannot move bookmarks without a solution.");
            }

            BookmarkStorageInfo currentInfo = GetStorageInfo(solutionPath);
            if (currentInfo.Location == targetLocation)
            {
                return currentInfo;
            }

            var targetPath = targetLocation == BookmarkStorageLocation.Workspace
                ? GetSolutionStoragePath(solutionPath)
                : GetPersonalStoragePath(solutionPath);

            var targetDirectory = Path.GetDirectoryName(targetPath);
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                throw new InvalidOperationException("Could not determine target directory for bookmarks.");
            }

            await Task.Run(() =>
            {
                Directory.CreateDirectory(targetDirectory);

                // Move or copy the file to the new location
                if (File.Exists(currentInfo.AbsolutePath))
                {
                    File.Copy(currentInfo.AbsolutePath, targetPath, overwrite: true);
                    File.Delete(currentInfo.AbsolutePath);

                    // Also clean up any legacy named files at the old location
                    CleanupLegacyFiles(currentInfo.AbsolutePath);
                }
            }, cancellationToken);

            return GetStorageInfo(solutionPath);
        }

        /// <summary>
        /// Loads workspace state from a specific storage location.
        /// </summary>
        public async Task<BookmarkWorkspaceState> LoadWorkspaceFromLocationAsync(string solutionPath, BookmarkStorageLocation location, CancellationToken cancellationToken)
        {
            // For Workspace storage, we need a solution path
            if (location == BookmarkStorageLocation.Workspace && string.IsNullOrWhiteSpace(solutionPath))
            {
                return new BookmarkWorkspaceState();
            }

            // For Personal storage without a solution, return empty (no .vs folder)
            if (location == BookmarkStorageLocation.Personal && string.IsNullOrWhiteSpace(solutionPath))
            {
                return new BookmarkWorkspaceState();
            }

            var storagePath = GetStoragePathForLocation(solutionPath, location);

            if (!File.Exists(storagePath))
            {
                return new BookmarkWorkspaceState();
            }

            var json = await Task.Run(() => File.ReadAllText(storagePath, Encoding.UTF8), cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new BookmarkWorkspaceState();
            }

            try
            {
                var solutionDirectory = location == BookmarkStorageLocation.Global ? string.Empty : GetSolutionDirectory(solutionPath);
                var storageDirectory = location == BookmarkStorageLocation.Global ? string.Empty : GetStorageDirectory(storagePath);
                return ParseWorkspaceJsonFromLocation(json, solutionDirectory, storageDirectory, location, cancellationToken);
            }
            catch (JsonException ex)
            {
                await ex.LogAsync();
                return new BookmarkWorkspaceState();
            }
        }

        private BookmarkWorkspaceState ParseWorkspaceJsonFromLocation(string json, string solutionDirectory, string storageDirectory, BookmarkStorageLocation location, CancellationToken cancellationToken)
        {
            using var document = JsonDocument.Parse(json);
            JsonElement rootElement = document.RootElement;
            var documentPathBaseDirectory = GetDocumentPathBaseDirectory(rootElement, solutionDirectory, storageDirectory);

            var state = new BookmarkWorkspaceState();

            if (TryGetObjectProperty(rootElement, RootPropertyName, out JsonElement rootFolderElement))
            {
                ParseFolderNode(state, rootFolderElement, string.Empty, documentPathBaseDirectory, location, cancellationToken);
                ParseExpandedFolders(state, rootElement);
                return NormalizeState(state, location);
            }

            if (TryGetObjectProperty(rootElement, "bookmarks", out JsonElement legacyBookmarks) && legacyBookmarks.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in legacyBookmarks.EnumerateArray())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    BookmarkMetadata metadata = Normalize(ToMetadata(item, documentPathBaseDirectory, string.Empty));
                    metadata.StorageLocation = location;
                    state.Bookmarks.Add(metadata);
                    RegisterFolderPath(state.FolderPaths, metadata.Group);
                }
            }

            return NormalizeState(state, location);
        }

        /// <summary>
        /// Saves workspace state to a specific storage location.
        /// </summary>
        public async Task SaveWorkspaceToLocationAsync(string solutionPath, BookmarkStorageLocation location, BookmarkWorkspaceState state, CancellationToken cancellationToken)
        {
            if (state is null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var storagePath = GetStoragePathForLocation(solutionPath, location);

            var directory = Path.GetDirectoryName(storagePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException("Bookmark storage directory could not be determined.");
            }

            Directory.CreateDirectory(directory);

            FolderNode root = BuildFolderTree(state, state.ExpandedFolders);

            var documentPathBaseDirectory = location switch
            {
                BookmarkStorageLocation.Global => string.Empty,
                BookmarkStorageLocation.Workspace => GetStorageDirectory(storagePath),
                _ => GetSolutionDirectory(solutionPath),
            };
            var json = await Task.Run(() => SerializeTree(root, documentPathBaseDirectory, location == BookmarkStorageLocation.Workspace), cancellationToken);
            await Task.Run(() => File.WriteAllText(storagePath, json, Encoding.UTF8), cancellationToken);
        }

        /// <summary>
        /// Loads, updates, and saves workspace state for a specific storage location.
        /// </summary>
        public async Task<BookmarkWorkspaceState> UpdateWorkspaceAtLocationAsync(
            string solutionPath,
            BookmarkStorageLocation location,
            Action<BookmarkWorkspaceState> updateAction,
            CancellationToken cancellationToken)
        {
            BookmarkWorkspaceState state = await LoadWorkspaceFromLocationAsync(solutionPath, location, cancellationToken);
            updateAction(state);
            BookmarkRepositoryService.NormalizeWorkspaceState(state);
            await SaveWorkspaceToLocationAsync(solutionPath, location, state, cancellationToken);
            return state;
        }

        /// <summary>
        /// Loads Global, Personal, and Workspace storage locations and returns combined state.
        /// </summary>
        public async Task<DualBookmarkWorkspaceState> LoadDualWorkspaceAsync(string solutionPath, CancellationToken cancellationToken)
        {
            BookmarkWorkspaceState globalState = await LoadWorkspaceFromLocationAsync(solutionPath, BookmarkStorageLocation.Global, cancellationToken);
            BookmarkWorkspaceState personalState = await LoadWorkspaceFromLocationAsync(solutionPath, BookmarkStorageLocation.Personal, cancellationToken);
            BookmarkWorkspaceState solutionState = await LoadWorkspaceFromLocationAsync(solutionPath, BookmarkStorageLocation.Workspace, cancellationToken);

            return new DualBookmarkWorkspaceState(globalState, personalState, solutionState);
        }

        /// <summary>
        /// Moves a bookmark from one storage location to another.
        /// </summary>
        public async Task MoveBookmarkBetweenLocationsAsync(string solutionPath, string bookmarkId, BookmarkStorageLocation sourceLocation, BookmarkStorageLocation targetLocation, CancellationToken cancellationToken)
        {
            DualBookmarkWorkspaceState dualState = await LoadDualWorkspaceAsync(solutionPath, cancellationToken);

            BookmarkWorkspaceState sourceState = dualState.GetStateForLocation(sourceLocation);
            BookmarkWorkspaceState targetState = dualState.GetStateForLocation(targetLocation);

            BookmarkMetadata? bookmark = sourceState.Bookmarks.FirstOrDefault(b => string.Equals(b.BookmarkId, bookmarkId, StringComparison.Ordinal));
            if (bookmark is null)
            {
                return;
            }

            sourceState.Bookmarks.Remove(bookmark);
            bookmark.StorageLocation = targetLocation;
            targetState.Bookmarks.Add(bookmark);
            RegisterFolderPath(targetState.FolderPaths, bookmark.Group);

            await SaveWorkspaceToLocationAsync(solutionPath, sourceLocation, sourceState, cancellationToken);
            await SaveWorkspaceToLocationAsync(solutionPath, targetLocation, targetState, cancellationToken);
        }

        /// <summary>
        /// Moves a bookmark from one storage location to another, and also changes its folder.
        /// </summary>
        public async Task MoveBookmarkBetweenLocationsAsync(
            string solutionPath,
            string bookmarkId,
            string targetFolderPath,
            BookmarkStorageLocation sourceLocation,
            BookmarkStorageLocation targetLocation,
            CancellationToken cancellationToken)
        {
            var normalizedTargetFolder = BookmarkIdentity.NormalizeFolderPath(targetFolderPath);

            DualBookmarkWorkspaceState dualState = await LoadDualWorkspaceAsync(solutionPath, cancellationToken);

            BookmarkWorkspaceState sourceState = dualState.GetStateForLocation(sourceLocation);
            BookmarkWorkspaceState targetState = dualState.GetStateForLocation(targetLocation);

            BookmarkMetadata? bookmark = sourceState.Bookmarks.FirstOrDefault(b => string.Equals(b.BookmarkId, bookmarkId, StringComparison.Ordinal));
            if (bookmark is null)
            {
                return;
            }

            sourceState.Bookmarks.Remove(bookmark);
            bookmark.StorageLocation = targetLocation;
            bookmark.Group = normalizedTargetFolder;
            targetState.Bookmarks.Add(bookmark);
            RegisterFolderPath(targetState.FolderPaths, normalizedTargetFolder);

            await SaveWorkspaceToLocationAsync(solutionPath, sourceLocation, sourceState, cancellationToken);
            await SaveWorkspaceToLocationAsync(solutionPath, targetLocation, targetState, cancellationToken);
        }

        /// <summary>
        /// Moves a folder and all its contents (bookmarks and subfolders) from one storage location to another.
        /// </summary>
        public async Task MoveFolderBetweenLocationsAsync(
            string solutionPath,
            string sourceFolderPath,
            BookmarkStorageLocation sourceLocation,
            string targetFolderPath,
            BookmarkStorageLocation targetLocation,
            CancellationToken cancellationToken)
        {
            var normalizedSource = BookmarkIdentity.NormalizeFolderPath(sourceFolderPath);
            var normalizedTarget = BookmarkIdentity.NormalizeFolderPath(targetFolderPath);

            if (string.IsNullOrWhiteSpace(normalizedSource))
            {
                throw new ArgumentException("Cannot move the root folder.", nameof(sourceFolderPath));
            }

            DualBookmarkWorkspaceState dualState = await LoadDualWorkspaceAsync(solutionPath, cancellationToken);

            BookmarkWorkspaceState sourceState = dualState.GetStateForLocation(sourceLocation);
            BookmarkWorkspaceState targetState = dualState.GetStateForLocation(targetLocation);

            var sourcePrefix = normalizedSource + "/";

            // Find all bookmarks in the source folder and its subfolders
            List<BookmarkMetadata> bookmarksToMove = [.. sourceState.Bookmarks
                .Where(b =>
                {
                    var group = BookmarkIdentity.NormalizeFolderPath(b.Group);
                    return string.Equals(group, normalizedSource, StringComparison.OrdinalIgnoreCase)
                        || group.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase);
                })];

            // Find all folder paths in the source folder
            List<string> foldersToMove = [.. sourceState.FolderPaths
                .Where(path =>
                    string.Equals(path, normalizedSource, StringComparison.OrdinalIgnoreCase)
                    || path.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase))];

            // Remove bookmarks from source
            foreach (BookmarkMetadata bookmark in bookmarksToMove)
            {
                sourceState.Bookmarks.Remove(bookmark);
            }

            // Remove folders from source
            foreach (var folder in foldersToMove)
            {
                sourceState.FolderPaths.Remove(folder);
            }

            // Ensure root folder exists in source
            sourceState.FolderPaths.Add(string.Empty);

            // Calculate path transformation: replace source path with target path
            foreach (BookmarkMetadata bookmark in bookmarksToMove)
            {
                var oldGroup = BookmarkIdentity.NormalizeFolderPath(bookmark.Group);
                string newGroup;

                if (string.Equals(oldGroup, normalizedSource, StringComparison.OrdinalIgnoreCase))
                {
                    newGroup = normalizedTarget;
                }
                else
                {
                    // Subfolder: replace prefix
                    newGroup = normalizedTarget + oldGroup.Substring(normalizedSource.Length);
                }

                bookmark.Group = newGroup;
                bookmark.StorageLocation = targetLocation;
                targetState.Bookmarks.Add(bookmark);
            }

            // Add transformed folder paths to target
            foreach (var folder in foldersToMove)
            {
                string newFolder;
                if (string.Equals(folder, normalizedSource, StringComparison.OrdinalIgnoreCase))
                {
                    newFolder = normalizedTarget;
                }
                else
                {
                    newFolder = normalizedTarget + folder.Substring(normalizedSource.Length);
                }

                RegisterFolderPath(targetState.FolderPaths, newFolder);
            }

            // Always ensure the target folder itself is registered
            RegisterFolderPath(targetState.FolderPaths, normalizedTarget);

            // Save both workspaces
            await SaveWorkspaceToLocationAsync(solutionPath, sourceLocation, sourceState, cancellationToken);
            await SaveWorkspaceToLocationAsync(solutionPath, targetLocation, targetState, cancellationToken);
        }

        private static void CleanupLegacyFiles(string currentPath)
        {
            var directory = Path.GetDirectoryName(currentPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            // If the current file uses the new name, also delete any legacy file
            var currentFileName = Path.GetFileName(currentPath);
            if (string.Equals(currentFileName, BookmarksFileName, StringComparison.OrdinalIgnoreCase))
            {
                var legacyPath = Path.Combine(directory, LegacyBookmarksFileName);
                if (File.Exists(legacyPath))
                {
                    try
                    {
                        File.Delete(legacyPath);
                    }
                    catch (IOException)
                    {
                        // Ignore if we can't delete the legacy file
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Ignore if we don't have permission
                    }
                }
            }
        }

        private static string? GetRepositoryRoot(string directoryPath)
        {
            string normalizedDirectoryPath;
            try
            {
                normalizedDirectoryPath = Path.GetFullPath(directoryPath)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (NotSupportedException)
            {
                return null;
            }

            var current = normalizedDirectoryPath;

            while (!string.IsNullOrWhiteSpace(current))
            {
                if (Directory.Exists(Path.Combine(current, ".git")))
                {
                    return current;
                }

                current = Path.GetDirectoryName(current.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            }

            return null;
        }

        private static BookmarkWorkspaceState NormalizeState(BookmarkWorkspaceState state)
        {
            return NormalizeState(state, BookmarkStorageLocation.Personal);
        }

        private static BookmarkWorkspaceState NormalizeState(BookmarkWorkspaceState state, BookmarkStorageLocation location)
        {
            RegisterFolderPath(state.FolderPaths, string.Empty);

            foreach (BookmarkMetadata bookmark in state.Bookmarks)
            {
                bookmark.Group = BookmarkIdentity.NormalizeFolderPath(bookmark.Group);
                bookmark.StorageLocation = location;
                RegisterFolderPath(state.FolderPaths, bookmark.Group);
            }

            return state;
        }

        private static void ParseFolderNode(BookmarkWorkspaceState state, JsonElement folderElement, string folderPath, string solutionDirectory, CancellationToken cancellationToken)
        {
            ParseFolderNode(state, folderElement, folderPath, solutionDirectory, BookmarkStorageLocation.Personal, cancellationToken);
        }

        private static void ParseFolderNode(BookmarkWorkspaceState state, JsonElement folderElement, string folderPath, string solutionDirectory, BookmarkStorageLocation location, CancellationToken cancellationToken)
        {
            RegisterFolderPath(state.FolderPaths, folderPath);

            if (folderElement.TryGetProperty("expanded", out JsonElement expandedElement) &&
                expandedElement.ValueKind == JsonValueKind.True &&
                !string.IsNullOrEmpty(folderPath))
            {
                state.ExpandedFolders.Add(folderPath);
            }

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
                        metadata.StorageLocation = location;
                        state.Bookmarks.Add(metadata);
                    }

                    continue;
                }

                if (property.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var childPath = string.IsNullOrEmpty(folderPath)
                    ? property.Name
                    : string.Concat(folderPath, "/", property.Name);

                ParseFolderNode(state, property.Value, BookmarkIdentity.NormalizeFolderPath(childPath), solutionDirectory, location, cancellationToken);
            }
        }

        private static void ParseExpandedFolders(BookmarkWorkspaceState state, JsonElement rootElement)
        {
            if (rootElement.TryGetProperty("expandedFolders", out JsonElement expandedFoldersElement) &&
                expandedFoldersElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement folderElement in expandedFoldersElement.EnumerateArray())
                {
                    if (folderElement.ValueKind == JsonValueKind.String)
                    {
                        var folderPath = folderElement.GetString();
                        if (!string.IsNullOrEmpty(folderPath))
                        {
                            state.ExpandedFolders.Add(folderPath!);
                        }
                    }
                }
            }
        }

        private static BookmarkMetadata ToMetadata(JsonElement element, string solutionDirectory, string folderPath)
        {
            var documentPath = GetStringProperty(element, "documentPath");
            var lineNumber = GetIntProperty(element, "lineNumber");

            return new BookmarkMetadata
            {
                BookmarkId = GetStringProperty(element, "id"),
                DocumentPath = MakeAbsolutePath(documentPath, solutionDirectory),
                LineNumber = lineNumber,
                LineText = GetStringProperty(element, "lineText"),
                ShortcutNumber = GetNullableIntProperty(element, "slotNumber"),
                Label = GetStringProperty(element, "label"),
                Group = !string.IsNullOrWhiteSpace(GetStringProperty(element, "group"))
                    ? GetStringProperty(element, "group")
                    : folderPath,
                Color = GetColorProperty(element, "color"),
                CreatedUtc = GetDateTimeProperty(element, "createdUtc"),
            };
        }

        private static string SerializeTree(FolderNode root, string documentPathBaseDirectory, bool writeDocumentPathRoot)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                if (writeDocumentPathRoot)
                {
                    writer.WriteString(DocumentPathRootPropertyName, BookmarksFileDocumentPathRoot);
                }

                writer.WritePropertyName(RootPropertyName);
                WriteFolderNode(writer, root, documentPathBaseDirectory);
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static void WriteFolderNode(Utf8JsonWriter writer, FolderNode folderNode, string documentPathBaseDirectory)
        {
            writer.WriteStartObject();

            if (folderNode.IsExpanded)
            {
                writer.WriteBoolean("expanded", true);
            }

            writer.WritePropertyName(BookmarksPropertyName);
            writer.WriteStartArray();

            foreach (BookmarkMetadata bookmark in folderNode.Bookmarks
                .OrderBy(item => item.DocumentPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.LineNumber)
                .ThenBy(item => item.BookmarkId, StringComparer.Ordinal))
            {
                writer.WriteStartObject();
                writer.WriteString("id", bookmark.BookmarkId);
                writer.WriteString("documentPath", MakeRelativePath(bookmark.DocumentPath, documentPathBaseDirectory));
                writer.WriteNumber("lineNumber", bookmark.LineNumber);
                writer.WriteString("lineText", bookmark.LineText?.Trim() ?? string.Empty);

                if (bookmark.ShortcutNumber.HasValue)
                {
                    writer.WriteNumber("slotNumber", bookmark.ShortcutNumber.Value);
                }

                if (!string.IsNullOrWhiteSpace(bookmark.Label))
                {
                    writer.WriteString("label", bookmark.Label);
                }

                if (bookmark.Color != BookmarkColor.None)
                {
                    writer.WriteString("color", bookmark.Color.ToString().ToLowerInvariant());
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            foreach (KeyValuePair<string, FolderNode> child in folderNode.Children.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
            {
                writer.WritePropertyName(child.Key);
                WriteFolderNode(writer, child.Value, documentPathBaseDirectory);
            }

            writer.WriteEndObject();
        }

        private static FolderNode BuildFolderTree(BookmarkWorkspaceState state, ISet<string> expandedFolders)
        {
            var root = new FolderNode();

            foreach (var path in state.FolderPaths)
            {
                FolderNode folder = EnsureFolder(root, path);
                if (expandedFolders.Contains(path))
                {
                    folder.IsExpanded = true;
                }
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
            var normalizedPath = BookmarkIdentity.NormalizeFolderPath(folderPath);
            if (string.IsNullOrEmpty(normalizedPath))
            {
                return root;
            }

            FolderNode current = root;
            var segments = normalizedPath.Split('/');
            foreach (var segment in segments)
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

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value))
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

            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value))
            {
                return value;
            }

            return null;
        }

        private static BookmarkColor GetColorProperty(JsonElement element, string propertyName)
        {
            var value = GetStringProperty(element, propertyName);
            if (Enum.TryParse(value, true, out BookmarkColor color))
            {
                return color;
            }

            return BookmarkColor.None;
        }

        private static DateTime GetDateTimeProperty(JsonElement element, string propertyName)
        {
            var value = GetStringProperty(element, propertyName);
            return ParseDateTime(value);
        }

        private static DateTime? GetNullableDateTimeProperty(JsonElement element, string propertyName)
        {
            var value = GetStringProperty(element, propertyName);
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
            if (string.IsNullOrWhiteSpace(metadata.BookmarkId))
            {
                metadata.BookmarkId = Guid.NewGuid().ToString("N");
            }

            metadata.DocumentPath = metadata.DocumentPath ?? string.Empty;
            metadata.LineText = metadata.LineText ?? string.Empty;
            metadata.Label = metadata.Label ?? string.Empty;
            metadata.Group = BookmarkIdentity.NormalizeFolderPath(metadata.Group);

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

        private static string GetStorageDirectory(string storagePath)
        {
            if (string.IsNullOrWhiteSpace(storagePath))
            {
                return string.Empty;
            }

            return Path.GetDirectoryName(storagePath) ?? string.Empty;
        }

        private static string GetDocumentPathBaseDirectory(JsonElement rootElement, string solutionDirectory, string storageDirectory)
        {
            var documentPathRoot = GetStringProperty(rootElement, DocumentPathRootPropertyName);
            return string.Equals(documentPathRoot, BookmarksFileDocumentPathRoot, StringComparison.OrdinalIgnoreCase)
                ? storageDirectory
                : solutionDirectory;
        }

        private static string MakeRelativePath(string documentPath, string documentPathBaseDirectory)
        {
            if (string.IsNullOrWhiteSpace(documentPathBaseDirectory) || string.IsNullOrWhiteSpace(documentPath))
            {
                return documentPath ?? string.Empty;
            }

            try
            {
                var normalizedDirectory = Path.GetFullPath(documentPathBaseDirectory)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    + Path.DirectorySeparatorChar;
                var normalizedDocumentPath = Path.GetFullPath(documentPath);

                var directoryRoot = Path.GetPathRoot(normalizedDirectory);
                var documentRoot = Path.GetPathRoot(normalizedDocumentPath);
                if (!string.Equals(directoryRoot, documentRoot, StringComparison.OrdinalIgnoreCase))
                {
                    return documentPath;
                }

                var directoryUri = new Uri(normalizedDirectory);
                var documentUri = new Uri(normalizedDocumentPath);
                var relativePath = Uri.UnescapeDataString(directoryUri.MakeRelativeUri(documentUri).ToString());
                return relativePath.Replace('\\', '/');
            }
            catch (Exception ex) when (ex is ArgumentException
                || ex is IOException
                || ex is NotSupportedException
                || ex is System.Security.SecurityException
                || ex is UriFormatException)
            {
                _ = ex.LogAsync();
                return documentPath;
            }
        }

        private static string MakeAbsolutePath(string documentPath, string documentPathBaseDirectory)
        {
            if (string.IsNullOrWhiteSpace(documentPathBaseDirectory) || string.IsNullOrWhiteSpace(documentPath))
            {
                return documentPath ?? string.Empty;
            }

            if (Path.IsPathRooted(documentPath))
            {
                return documentPath;
            }

            return Path.GetFullPath(Path.Combine(documentPathBaseDirectory, documentPath));
        }

        private sealed class FolderNode
        {
            public Dictionary<string, FolderNode> Children { get; } = new Dictionary<string, FolderNode>(StringComparer.OrdinalIgnoreCase);

            public List<BookmarkMetadata> Bookmarks { get; } = new List<BookmarkMetadata>();

            public bool IsExpanded { get; set; }
        }
    }
}
