using System.Collections.Generic;

namespace BookmarkStudio
{
    internal sealed class ToolWindowInitData
    {
        public ToolWindowInitData(
            IReadOnlyList<ManagedBookmark> bookmarks,
            IReadOnlyList<string> folderPaths,
            IEnumerable<string> expandedFolders)
        {
            Bookmarks = bookmarks;
            FolderPaths = folderPaths;
            ExpandedFolders = expandedFolders;
        }

        public IReadOnlyList<ManagedBookmark> Bookmarks { get; }

        public IReadOnlyList<string> FolderPaths { get; }

        public IEnumerable<string> ExpandedFolders { get; }
    }
}
