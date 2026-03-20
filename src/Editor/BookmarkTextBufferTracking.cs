using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace BookmarkStudio
{
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType("text")]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class BookmarkTextViewCreationListener : IWpfTextViewCreationListener
    {
        [Import]
        internal ITextDocumentFactoryService TextDocumentFactoryService = null!;

        public void TextViewCreated(IWpfTextView textView)
        {
            if (textView is null)
            {
                return;
            }

            if (!TextDocumentFactoryService.TryGetTextDocument(textView.TextBuffer, out ITextDocument document))
            {
                return;
            }

            BookmarkTextBufferTracker tracker = textView.TextBuffer.Properties.GetOrCreateSingletonProperty(() => new BookmarkTextBufferTracker(textView.TextBuffer, document.FilePath));
            tracker.AttachToView(textView);
        }
    }

    internal sealed class BookmarkTextBufferTracker : IDisposable
    {
        private readonly string _documentPath;
        private readonly string _normalizedDocumentPath;
        private readonly SemaphoreSlim _updateGate = new(1, 1);
        private readonly ITextBuffer _textBuffer;
        private int _attachedViewCount;
        private int _isDisposed;

        public BookmarkTextBufferTracker(ITextBuffer textBuffer, string documentPath)
        {
            _textBuffer = textBuffer ?? throw new ArgumentNullException(nameof(textBuffer));
            _documentPath = documentPath ?? string.Empty;
            _normalizedDocumentPath = BookmarkIdentity.NormalizeDocumentPath(_documentPath);
            _textBuffer.ChangedLowPriority += OnTextBufferChanged;
        }

        public void AttachToView(IWpfTextView textView)
        {
            if (textView is null)
            {
                return;
            }

            _ = Interlocked.Increment(ref _attachedViewCount);
            void closedHandler(object sender, EventArgs args)
            {
                textView.Closed -= closedHandler;
                if (Interlocked.Decrement(ref _attachedViewCount) == 0)
                {
                    Dispose();
                }
            }

            textView.Closed += closedHandler;
        }

        private void OnTextBufferChanged(object sender, TextContentChangedEventArgs e)
        {
            if (Volatile.Read(ref _isDisposed) == 1)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_normalizedDocumentPath) || e is null || e.Before is null || e.After is null || e.Changes.Count == 0)
            {
                return;
            }

            ITextSnapshot before = e.Before;
            ITextSnapshot after = e.After;

            _ = ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                await _updateGate.WaitAsync();
                try
                {
                    IReadOnlyList<ManagedBookmark> bookmarks = await BookmarkStudioSession.Current.TryUpdateBookmarksAsync(
                        metadata => UpdateBookmarks(metadata, before, after),
                        CancellationToken.None);

                    if (bookmarks.Any(bookmark => MatchesDocumentPath(bookmark.DocumentPath)))
                    {
                        await BookmarkManagerToolWindow.RefreshIfVisibleAsync(CancellationToken.None);
                    }
                }
                catch (OperationCanceledException ex)
                {
                    await ex.LogAsync();
                }
                finally
                {
                    _ = _updateGate.Release();
                }
            });
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) == 1)
            {
                return;
            }

            _textBuffer.ChangedLowPriority -= OnTextBufferChanged;
            _updateGate.Dispose();
        }

        private bool UpdateBookmarks(List<BookmarkMetadata> metadata, ITextSnapshot beforeSnapshot, ITextSnapshot afterSnapshot)
        {
            var documentBookmarks = metadata.Where(bookmark => MatchesDocumentPath(bookmark.DocumentPath)).ToList();
            if (documentBookmarks.Count == 0)
            {
                return false;
            }

            bool changed = false;
            DateTime seenUtc = DateTime.UtcNow;

            foreach (BookmarkMetadata bookmark in documentBookmarks)
            {
                BookmarkSnapshot translatedBookmark = TranslateBookmark(bookmark, beforeSnapshot, afterSnapshot);
                if (bookmark.LineNumber == translatedBookmark.LineNumber
                    && bookmark.ColumnNumber == translatedBookmark.ColumnNumber
                    && string.Equals(bookmark.LineText, translatedBookmark.LineText, StringComparison.Ordinal)
                    && string.Equals(bookmark.DocumentPath, translatedBookmark.DocumentPath, StringComparison.Ordinal))
                {
                    continue;
                }

                bookmark.UpdateFromSnapshot(translatedBookmark, seenUtc);
                changed = true;
            }

            return changed;
        }

        private BookmarkSnapshot TranslateBookmark(BookmarkMetadata bookmark, ITextSnapshot beforeSnapshot, ITextSnapshot afterSnapshot)
        {
            int beforeLineNumber = Clamp(bookmark.LineNumber - 1, 0, Math.Max(beforeSnapshot.LineCount - 1, 0));
            ITextSnapshotLine beforeLine = beforeSnapshot.GetLineFromLineNumber(beforeLineNumber);
            int beforeColumn = Clamp(bookmark.ColumnNumber - 1, 0, beforeLine.Length);
            var beforePoint = new SnapshotPoint(beforeSnapshot, beforeLine.Start.Position + beforeColumn);
            SnapshotPoint afterPoint = beforePoint.TranslateTo(afterSnapshot, PointTrackingMode.Positive);
            ITextSnapshotLine afterLine = afterPoint.GetContainingLine();

            return new BookmarkSnapshot
            {
                DocumentPath = _documentPath,
                LineNumber = afterLine.LineNumber + 1,
                ColumnNumber = afterPoint.Position - afterLine.Start.Position + 1,
                LineText = afterLine.GetText(),
            };
        }

        private bool MatchesDocumentPath(string documentPath)
            => string.Equals(BookmarkIdentity.NormalizeDocumentPath(documentPath), _normalizedDocumentPath, StringComparison.Ordinal);

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum)
            {
                return minimum;
            }

            return value > maximum ? maximum : value;
        }
    }
}
