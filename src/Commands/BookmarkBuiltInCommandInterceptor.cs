using System.Threading;
using Microsoft.VisualStudio;

namespace BookmarkStudio
{
    internal static class BookmarkBuiltInCommandInterceptor
    {
        private static readonly System.Collections.Generic.List<IDisposable> _registrations = new();
        private static bool _isInitialized;

        public static async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            _isInitialized = true;
            _registrations.Add(await VS.Commands.InterceptAsync(VSConstants.VSStd2KCmdID.TOGGLETEMPBOOKMARK, () => Execute(cancellationToken => BookmarkCommandActions.ToggleBookmarkAsync(cancellationToken))));
            _registrations.Add(await VS.Commands.InterceptAsync(VSConstants.VSStd2KCmdID.GOTONEXTBOOKMARK, () => Execute(cancellationToken => BookmarkCommandActions.GoToNextBookmarkAsync(cancellationToken))));
            _registrations.Add(await VS.Commands.InterceptAsync(VSConstants.VSStd2KCmdID.GOTOPREVBOOKMARK, () => Execute(cancellationToken => BookmarkCommandActions.GoToPreviousBookmarkAsync(cancellationToken))));
            _registrations.Add(await VS.Commands.InterceptAsync(VSConstants.VSStd2KCmdID.ECMD_GOTONEXTBOOKMARKINDOC, () => Execute(cancellationToken => BookmarkCommandActions.GoToNextBookmarkInDocumentAsync(cancellationToken))));
            _registrations.Add(await VS.Commands.InterceptAsync(VSConstants.VSStd2KCmdID.ECMD_GOTOPREVBOOKMARKINDOC, () => Execute(cancellationToken => BookmarkCommandActions.GoToPreviousBookmarkInDocumentAsync(cancellationToken))));
            _registrations.Add(await VS.Commands.InterceptAsync(VSConstants.VSStd2KCmdID.ECMD_DELETEALLBOOKMARKSINDOC, () => Execute(cancellationToken => BookmarkCommandActions.ClearBookmarksInDocumentAsync(cancellationToken))));
        }

        private static CommandProgression Execute(Func<CancellationToken, Task> action)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(() => ExecuteAsync(action)).FireAndForget();

            return CommandProgression.Stop;
        }

        private static async Task ExecuteAsync(Func<CancellationToken, Task> action)
        {
            try
            {
                await action(CancellationToken.None);
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
    }
}
