using System.IO;
using Microsoft.Win32;

namespace BookmarkStudio
{
    [Command(PackageIds.ExportBookmarksCommand)]
    internal sealed class ExportBookmarksCommand : BookmarkCommandBase<ExportBookmarksCommand>
    {
        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                Filter = "Text Files|*.txt|Markdown Files|*.md|CSV Files|*.csv|All Files|*.*",
                DefaultExt = ".txt",
                AddExtension = true,
                FileName = "bookmarks.txt",
            };

            bool? result = dialog.ShowDialog();
            if (result != true || string.IsNullOrWhiteSpace(dialog.FileName))
            {
                return;
            }

            BookmarkExportFormat format = GetFormat(dialog.FileName);
            string exportText = await BookmarkOperationsService.Current.ExportAsync(format, System.Threading.CancellationToken.None);
            File.WriteAllText(dialog.FileName, exportText);
            await VS.MessageBox.ShowAsync("BookmarkStudio", string.Concat("Bookmarks exported to ", dialog.FileName, "."));
        }

        private static BookmarkExportFormat GetFormat(string filePath)
        {
            string extension = Path.GetExtension(filePath);
            return extension.ToLowerInvariant() switch
            {
                ".md" => BookmarkExportFormat.Markdown,
                ".csv" => BookmarkExportFormat.Csv,
                _ => BookmarkExportFormat.PlainText,
            };
        }
    }
}
