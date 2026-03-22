using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;

namespace BookmarkStudio
{
    /// <summary>
    /// Helper class for detecting and removing native Visual Studio bookmarks.
    /// This enables seamless conversion from native bookmarks to BookmarkStudio bookmarks.
    /// </summary>
    internal static class NativeBookmarkHelper
    {
        // MARKERTYPE.MARKER_BOOKMARK
        private const int MARKER_BOOKMARK = 3;

        // MARKERTYPE2.MARKER_BOOKMARK_DISABLED
        private const int MARKER_BOOKMARK_DISABLED = 21;

        // ENUMMARKERFLAGS.EM_GLYPHINSPAN - Return markers that have a margin glyph and start on the same line
        private const uint EM_GLYPHINSPAN = 16;

        private static IVsEditorAdaptersFactoryService? _editorAdaptersService;

        /// <summary>
        /// Checks if the given line has a native VS bookmark and removes it if found.
        /// This should be called when creating a new BookmarkStudio bookmark to convert
        /// any existing native bookmark on the same line.
        /// </summary>
        /// <param name="documentPath">The full path to the document.</param>
        /// <param name="lineNumber">The 1-based line number to check.</param>
        /// <returns>True if a native bookmark was found and removed.</returns>
        public static async Task<bool> TryRemoveNativeBookmarkAsync(string documentPath, int lineNumber)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (string.IsNullOrWhiteSpace(documentPath))
            {
                return false;
            }

            IVsTextLines? vsTextLines = await GetVsTextLinesForDocumentAsync(documentPath);
            if (vsTextLines is null)
            {
                return false;
            }

            return TryRemoveNativeBookmarkCore(vsTextLines, lineNumber);
        }

        /// <summary>
        /// Checks if the given line has a native VS bookmark and removes it if found.
        /// </summary>
        /// <param name="textBuffer">The text buffer to check.</param>
        /// <param name="lineNumber">The 1-based line number to check.</param>
        /// <returns>True if a native bookmark was found and removed.</returns>
        public static bool TryRemoveNativeBookmark(ITextBuffer textBuffer, int lineNumber)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (textBuffer is null)
            {
                return false;
            }

            IVsTextLines? vsTextLines = GetVsTextLinesFromBuffer(textBuffer);
            if (vsTextLines is null)
            {
                return false;
            }

            return TryRemoveNativeBookmarkCore(vsTextLines, lineNumber);
        }

        /// <summary>
        /// Checks if a native bookmark exists on the specified line.
        /// </summary>
        /// <param name="documentPath">The full path to the document.</param>
        /// <param name="lineNumber">The 1-based line number to check.</param>
        /// <returns>True if a native bookmark exists on the line.</returns>
        public static async Task<bool> HasNativeBookmarkAsync(string documentPath, int lineNumber)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (string.IsNullOrWhiteSpace(documentPath))
            {
                return false;
            }

            IVsTextLines? vsTextLines = await GetVsTextLinesForDocumentAsync(documentPath);
            if (vsTextLines is null)
            {
                return false;
            }

            // Line numbers in IVsTextLines are 0-based
            int vsLine = lineNumber - 1;

            return HasMarkerOnLine(vsTextLines, vsLine, MARKER_BOOKMARK)
                || HasMarkerOnLine(vsTextLines, vsLine, MARKER_BOOKMARK_DISABLED);
        }

        private static bool TryRemoveNativeBookmarkCore(IVsTextLines vsTextLines, int lineNumber)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Line numbers in IVsTextLines are 0-based, but the input is 1-based
            int vsLine = lineNumber - 1;

            // Try to find and remove bookmark markers on this line
            bool removed = TryFindAndRemoveMarker(vsTextLines, vsLine, MARKER_BOOKMARK);

            // Also check for disabled bookmarks
            if (TryFindAndRemoveMarker(vsTextLines, vsLine, MARKER_BOOKMARK_DISABLED))
            {
                removed = true;
            }

            return removed;
        }

        private static bool TryFindAndRemoveMarker(IVsTextLines vsTextLines, int line, int markerType)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // Enumerate markers on this specific line
            int hr = vsTextLines.EnumMarkers(
                line, 0, line, 0,
                markerType,
                EM_GLYPHINSPAN,
                out IVsEnumLineMarkers enumMarkers);

            if (hr != VSConstants.S_OK || enumMarkers is null)
            {
                return false;
            }

            bool removed = false;

            while (enumMarkers.Next(out IVsTextLineMarker marker) == VSConstants.S_OK && marker is not null)
            {
                // Get the marker's line position to confirm it's on our line
                var span = new TextSpan[1];
                marker.GetCurrentSpan(span);
                if (span[0].iStartLine == line)
                {
                    marker.Invalidate();
                    removed = true;
                }
            }

            return removed;
        }

        private static bool HasMarkerOnLine(IVsTextLines vsTextLines, int line, int markerType)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            int hr = vsTextLines.EnumMarkers(
                line, 0, line, 0,
                markerType,
                EM_GLYPHINSPAN,
                out IVsEnumLineMarkers enumMarkers);

            if (hr != VSConstants.S_OK || enumMarkers is null)
            {
                return false;
            }

            while (enumMarkers.Next(out IVsTextLineMarker marker) == VSConstants.S_OK && marker is not null)
            {
                var span = new TextSpan[1];
                marker.GetCurrentSpan(span);
                if (span[0].iStartLine == line)
                {
                    return true;
                }
            }

            return false;
        }

        private static async Task<IVsTextLines?> GetVsTextLinesForDocumentAsync(string documentPath)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Try to get the running document table to find an already-open document
            var rdt = await VS.Services.GetRunningDocumentTableAsync();
            if (rdt is null)
            {
                return null;
            }

            // Find the document in the RDT
            int hr = rdt.FindAndLockDocument(
                (uint)_VSRDTFLAGS.RDT_NoLock,
                documentPath,
                out _,
                out _,
                out IntPtr docData,
                out _);

            if (hr != VSConstants.S_OK || docData == IntPtr.Zero)
            {
                return null;
            }

            try
            {
                object? docDataObj = System.Runtime.InteropServices.Marshal.GetObjectForIUnknown(docData);
                return docDataObj as IVsTextLines;
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.Release(docData);
            }
        }

        private static IVsTextLines? GetVsTextLinesFromBuffer(ITextBuffer textBuffer)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            // First, check if it's already cached in properties
            if (textBuffer.Properties.TryGetProperty(typeof(IVsTextLines), out IVsTextLines? vsTextLines))
            {
                return vsTextLines;
            }

            // Get the editor adapters service
            IVsEditorAdaptersFactoryService? adaptersService = GetEditorAdaptersService();
            if (adaptersService is null)
            {
                return null;
            }

            IVsTextBuffer? vsTextBuffer = adaptersService.GetBufferAdapter(textBuffer);
            return vsTextBuffer as IVsTextLines;
        }

        private static IVsEditorAdaptersFactoryService? GetEditorAdaptersService()
        {
            if (_editorAdaptersService is null)
            {
                _editorAdaptersService = VS.GetMefService<IVsEditorAdaptersFactoryService>();
            }

            return _editorAdaptersService;
        }
    }
}
