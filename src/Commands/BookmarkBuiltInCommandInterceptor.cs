using System.Threading;
using Microsoft.VisualStudio;

namespace BookmarkStudio
{
    internal static class BookmarkBuiltInCommandInterceptor
    {
        private static readonly Guid StandardCommandSet2K = new Guid("1496A755-94DE-11D0-8C3F-00C04FC2AAE2");
        private static readonly System.Collections.Generic.List<IDisposable> _registrations = new System.Collections.Generic.List<IDisposable>();
        private static bool _isInitialized;

        public static async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            _isInitialized = true;
            _registrations.Add(await VS.Commands.InterceptAsync(VSConstants.VSStd2KCmdID.TOGGLETEMPBOOKMARK, () => Execute(cancellationToken => BookmarkCommandActions.ToggleBookmarkAsync(cancellationToken))));
            _registrations.Add(await VS.Commands.InterceptAsync(StandardCommandSet2K, 77, () => Execute(cancellationToken => BookmarkCommandActions.GoToNextBookmarkAsync(cancellationToken))));
            _registrations.Add(await VS.Commands.InterceptAsync(StandardCommandSet2K, 78, () => Execute(cancellationToken => BookmarkCommandActions.GoToPreviousBookmarkAsync(cancellationToken))));
        }

        private static CommandProgression Execute(Func<CancellationToken, Task> action)
        {
            _ = ThreadHelper.JoinableTaskFactory.RunAsync(() => ExecuteAsync(action));

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
