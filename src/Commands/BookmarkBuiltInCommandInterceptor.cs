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

            General.Saved += OnSettingsSaved;

            // Always register interceptions initially - we'll handle the Ask/No cases in Execute
            await RegisterInterceptionsAsync();
        }

        private static void OnSettingsSaved(General settings)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                if (settings.InterceptBuiltInCommands == CommandInterceptionMode.No && _registrations.Count > 0)
                {
                    UnregisterInterceptions();
                }
                else if (settings.InterceptBuiltInCommands != CommandInterceptionMode.No && _registrations.Count == 0)
                {
                    await RegisterInterceptionsAsync();
                }
            }).FireAndForget();
        }

        private static async Task RegisterInterceptionsAsync()
        {
            _registrations.Add(await VS.Commands.InterceptAsync(VSConstants.VSStd2KCmdID.TOGGLETEMPBOOKMARK, () => ExecuteWithPrompt(cancellationToken => BookmarkCommandActions.ToggleBookmarkAsync(cancellationToken))));
            _registrations.Add(await VS.Commands.InterceptAsync(VSConstants.VSStd2KCmdID.GOTONEXTBOOKMARK, () => Execute(cancellationToken => BookmarkCommandActions.GoToNextBookmarkAsync(cancellationToken))));
            _registrations.Add(await VS.Commands.InterceptAsync(VSConstants.VSStd2KCmdID.GOTOPREVBOOKMARK, () => Execute(cancellationToken => BookmarkCommandActions.GoToPreviousBookmarkAsync(cancellationToken))));
            _registrations.Add(await VS.Commands.InterceptAsync(VSConstants.VSStd2KCmdID.ECMD_GOTONEXTBOOKMARKINDOC, () => Execute(cancellationToken => BookmarkCommandActions.GoToNextBookmarkInDocumentAsync(cancellationToken))));
            _registrations.Add(await VS.Commands.InterceptAsync(VSConstants.VSStd2KCmdID.ECMD_GOTOPREVBOOKMARKINDOC, () => Execute(cancellationToken => BookmarkCommandActions.GoToPreviousBookmarkInDocumentAsync(cancellationToken))));
            _registrations.Add(await VS.Commands.InterceptAsync(VSConstants.VSStd2KCmdID.ECMD_DELETEALLBOOKMARKSINDOC, () => Execute(cancellationToken => BookmarkCommandActions.ClearBookmarksInDocumentAsync(cancellationToken))));
        }

        private static void UnregisterInterceptions()
        {
            foreach (IDisposable registration in _registrations)
            {
                registration.Dispose();
            }

            _registrations.Clear();
        }

        private static CommandProgression ExecuteWithPrompt(Func<CancellationToken, Task> action)
        {
            CommandInterceptionMode mode = General.Instance.InterceptBuiltInCommands;

            if (mode == CommandInterceptionMode.Ask)
            {
                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    bool shouldIntercept = await PromptUserForInterceptionChoiceAsync();

                    if (shouldIntercept)
                    {
                        await ExecuteAsync(action);
                    }
                }).FireAndForget();

                // Stop the original command while we prompt - if user says No, the bookmark won't be created
                // but they can use Ctrl+K,K again and it will pass through
                return CommandProgression.Stop;
            }

            return Execute(action);
        }

        private static CommandProgression Execute(Func<CancellationToken, Task> action)
        {
            if (General.Instance.InterceptBuiltInCommands == CommandInterceptionMode.No)
            {
                return CommandProgression.Continue;
            }

            ThreadHelper.JoinableTaskFactory.RunAsync(() => ExecuteAsync(action)).FireAndForget();

            return CommandProgression.Stop;
        }

        private static async Task<bool> PromptUserForInterceptionChoiceAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            bool userChoseYes = await VS.MessageBox.ShowConfirmAsync(
                "Bookmark Studio",
                "Do you want Ctrl+K,K and other built-in bookmark shortcuts to use Bookmark Studio?\n\n" +
                "You can change this anytime in Tools > Options > Bookmark Studio.");

            General.Instance.InterceptBuiltInCommands = userChoseYes
                ? CommandInterceptionMode.Yes
                : CommandInterceptionMode.No;

            await General.Instance.SaveAsync();

            if (!userChoseYes)
            {
                UnregisterInterceptions();
            }

            return userChoseYes;
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
