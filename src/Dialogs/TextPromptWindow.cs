using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.PlatformUI;

namespace BookmarkStudio
{
    internal sealed class TextPromptWindow : VsUIDialogWindow
    {
        private readonly TextBox _inputTextBox;
        private readonly bool _selectTextOnLoad;

        private TextPromptWindow(string title, string prompt, string initialValue, bool selectTextOnLoad)
        {
            _selectTextOnLoad = selectTextOnLoad;

            Title = title;
            Width = 420;
            Height = 160;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            HasMaximizeButton = false;
            HasMinimizeButton = false;
            HasHelpButton = false;
            SetResourceReference(BackgroundProperty, EnvironmentColors.ToolWindowBackgroundBrushKey);
            SetResourceReference(ForegroundProperty, EnvironmentColors.ToolWindowTextBrushKey);
            Loaded += TextPromptWindow_Loaded;

            Grid grid = new Grid
            {
                Margin = new Thickness(12),
            };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Label promptBlock = new Label
            {
                Margin = new Thickness(0, 0, 0, 8),
                Content = prompt,
                Padding = new Thickness(0),
            };
            promptBlock.SetResourceReference(StyleProperty, VsResourceKeys.ThemedDialogLabelStyleKey);
            Grid.SetRow(promptBlock, 0);
            grid.Children.Add(promptBlock);

            _inputTextBox = new TextBox
            {
                Margin = new Thickness(0, 0, 0, 12),
                Text = initialValue ?? string.Empty,
            };
            _inputTextBox.SetResourceReference(StyleProperty, VsResourceKeys.ThemedDialogTextBoxStyleKey);
            Grid.SetRow(_inputTextBox, 1);
            grid.Children.Add(_inputTextBox);

            StackPanel buttons = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                Orientation = Orientation.Horizontal,
            };

            DialogButton okButton = new DialogButton
            {
                Content = "OK",
                IsDefault = true,
                Margin = new Thickness(0, 0, 8, 0),
                MinWidth = 75,
                Height = 23,
                Padding = new Thickness(8, 2, 8, 2),
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            okButton.SetResourceReference(StyleProperty, VsResourceKeys.ThemedDialogButtonStyleKey);
            okButton.Click += OkButton_Click;
            buttons.Children.Add(okButton);

            DialogButton cancelButton = new DialogButton
            {
                Content = "Cancel",
                IsCancel = true,
                MinWidth = 75,
                Height = 23,
                Padding = new Thickness(8, 2, 8, 2),
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            cancelButton.SetResourceReference(StyleProperty, VsResourceKeys.ThemedDialogButtonStyleKey);
            buttons.Children.Add(cancelButton);

            Grid.SetRow(buttons, 2);
            grid.Children.Add(buttons);

            Content = grid;
        }

        public static string? Show(string title, string prompt, string initialValue, bool selectTextOnLoad = false)
        {
            TextPromptWindow window = new TextPromptWindow(title, prompt, initialValue, selectTextOnLoad);

            bool? result = window.ShowDialog();
            return result == true ? window._inputTextBox.Text : null;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void TextPromptWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _inputTextBox.Focus();

            if (_selectTextOnLoad)
            {
                _inputTextBox.SelectAll();
                return;
            }

            _inputTextBox.CaretIndex = _inputTextBox.Text?.Length ?? 0;
        }
    }
}