using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell.FindAllReferences;
using Microsoft.VisualStudio.Shell.FindResults;
using Microsoft.VisualStudio.Shell.TableControl;
using Microsoft.VisualStudio.Shell.TableManager;

namespace BookmarkStudio
{
    [Command(PackageIds.AddResultAsBookmarkCommand)]
    internal sealed class AddResultAsBookmarkCommand : BookmarkCommandBase<AddResultAsBookmarkCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            try
            {
                IReadOnlyList<BookmarkSnapshot> snapshots = await GetSelectedResultSnapshotsAsync(CancellationToken.None);
                IReadOnlyList<ManagedBookmark> createdBookmarks = await BookmarkOperationsService.Current.AddBookmarksAsync(snapshots, CancellationToken.None);
                string? selectedBookmarkId = createdBookmarks.Count == 1 ? createdBookmarks[0].BookmarkId : null;

                await BookmarkManagerToolWindow.RefreshIfVisibleAsync(selectedBookmarkId, CancellationToken.None);
                await ShowStatusAsync(createdBookmarks.Count, snapshots.Count);
            }
            catch (OperationCanceledException ex)
            {
                await ex.LogAsync();
                await VS.StatusBar.ShowMessageAsync("The operation was canceled.");
            }
            catch (ArgumentException ex)
            {
                await ex.LogAsync();
                await VS.StatusBar.ShowMessageAsync(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                await ex.LogAsync();
                await VS.StatusBar.ShowMessageAsync(ex.Message);
            }
        }

        protected override void BeforeQueryStatus(EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            Command.Enabled = true;
        }

        private static async Task<IReadOnlyList<BookmarkSnapshot>> GetSelectedResultSnapshotsAsync(CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            ITableEntry[] selectedEntries = [.. GetSelectedResultEntries()];

            List<BookmarkSnapshot> snapshots = [];
            foreach (ITableEntry entry in selectedEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!TryGetBookmarkLocation(entry, out string documentPath, out int lineNumber))
                {
                    continue;
                }

                snapshots.Add(new BookmarkSnapshot
                {
                    DocumentPath = documentPath,
                    LineNumber = lineNumber,
                    LineText = await GetLineTextAsync(documentPath, lineNumber),
                });
            }

            return snapshots.Count > 0
                ? snapshots
                : throw new InvalidOperationException("The selected result does not point to a bookmarkable source location.");
        }

        private static IEnumerable<ITableEntry> GetSelectedResultEntries()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            IWpfTableControl? tableControl = GetActiveResultTable();
            if (tableControl is not null)
            {
                ITableEntryHandle[] selectedEntries = [.. tableControl.SelectedEntries ?? []];
                if (selectedEntries.Length > 0)
                {
                    return selectedEntries;
                }

                if (tableControl.SelectedEntry is not null)
                {
                    return [tableControl.SelectedEntry];
                }

                if (tableControl.SelectedOrFirstEntry is not null)
                {
                    return [tableControl.SelectedOrFirstEntry];
                }
            }

            return GetFocusedResultEntries();
        }

        private static IWpfTableControl? GetActiveResultTable()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var dte = Microsoft.VisualStudio.Shell.Package.GetGlobalService(typeof(DTE)) as DTE2;
            object? activeWindowObject = dte?.ActiveWindow?.Object;

            if (activeWindowObject is IFindAllReferencesWindow findAllReferencesWindow)
            {
                return findAllReferencesWindow.TableControl;
            }

            if (activeWindowObject is IFindResultsWindow findResultsWindow)
            {
                return findResultsWindow.TableControl;
            }

            return null;
        }

        private static IEnumerable<ITableEntry> GetFocusedResultEntries()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            DependencyObject? current = Keyboard.FocusedElement as DependencyObject;
            if (current is null)
            {
                return [];
            }

            while (current is not null)
            {
                if (current is MultiSelector multiSelector)
                {
                    ITableEntry[] selectedEntries = [.. multiSelector.SelectedItems.OfType<ITableEntry>()];
                    if (selectedEntries.Length > 0)
                    {
                        return selectedEntries;
                    }
                }
                else if (current is Selector selector && selector.SelectedItem is ITableEntry selectedEntry)
                {
                    return [selectedEntry];
                }

                if (current is FrameworkElement { DataContext: ITableEntry entry })
                {
                    return [entry];
                }

                current = GetParent(current);
            }

            return [];
        }

        private static DependencyObject? GetParent(DependencyObject current)
        {
            return current is Visual or Visual3D
                ? VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        private static bool TryGetBookmarkLocation(ITableEntry entry, out string documentPath, out int lineNumber)
        {
            documentPath = string.Empty;
            lineNumber = 0;

            if (!entry.TryGetValue(StandardTableKeyNames.DocumentName, out documentPath) || string.IsNullOrWhiteSpace(documentPath))
            {
                return false;
            }

            if (!entry.TryGetValue(StandardTableKeyNames.Line, out int zeroBasedLineNumber) || zeroBasedLineNumber < 0)
            {
                return false;
            }

            lineNumber = zeroBasedLineNumber + 1;
            return true;
        }

        private static async Task<string> GetLineTextAsync(string documentPath, int lineNumber)
        {
            try
            {
                return File.ReadLines(documentPath).Skip(lineNumber - 1).FirstOrDefault()?.TrimEnd('\r', '\n') ?? string.Empty;
            }
            catch (Exception ex) when (ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)
            {
                await ex.LogAsync();
                return string.Empty;
            }
        }

        private static async Task ShowStatusAsync(int createdCount, int requestedCount)
        {
            if (createdCount == 0)
            {
                await VS.StatusBar.ShowMessageAsync("The selected result is already bookmarked.");
            }
            else if (createdCount == 1 && requestedCount == 1)
            {
                await VS.StatusBar.ShowMessageAsync("Bookmark added.");
            }
            else
            {
                await VS.StatusBar.ShowMessageAsync($"Added {createdCount} bookmark(s). Skipped {requestedCount - createdCount} existing bookmark(s).");
            }
        }
    }
}
