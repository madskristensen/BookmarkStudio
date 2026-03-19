using System;
using System.Windows;
using System.Windows.Controls;

namespace BookmarkStudio
{
    internal sealed class TextPromptWindow : Window
    {
        private readonly TextBox _inputTextBox;

        private TextPromptWindow(string title, string prompt, string initialValue)
        {
            Title = title;
            Width = 420;
            Height = 160;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;

            Grid grid = new Grid
            {
                Margin = new Thickness(12),
            };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            TextBlock promptBlock = new TextBlock
            {
                Margin = new Thickness(0, 0, 0, 8),
                Text = prompt,
            };
            Grid.SetRow(promptBlock, 0);
            grid.Children.Add(promptBlock);

            _inputTextBox = new TextBox
            {
                Margin = new Thickness(0, 0, 0, 12),
                Text = initialValue ?? string.Empty,
            };
            Grid.SetRow(_inputTextBox, 1);
            grid.Children.Add(_inputTextBox);

            StackPanel buttons = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                Orientation = Orientation.Horizontal,
            };

            Button okButton = new Button
            {
                Content = "OK",
                IsDefault = true,
                Margin = new Thickness(0, 0, 8, 0),
                MinWidth = 80,
            };
            okButton.Click += OkButton_Click;
            buttons.Children.Add(okButton);

            Button cancelButton = new Button
            {
                Content = "Cancel",
                IsCancel = true,
                MinWidth = 80,
            };
            buttons.Children.Add(cancelButton);

            Grid.SetRow(buttons, 2);
            grid.Children.Add(buttons);

            Content = grid;
        }

        public static string? Show(string title, string prompt, string initialValue)
        {
            TextPromptWindow window = new TextPromptWindow(title, prompt, initialValue)
            {
                Owner = Application.Current?.MainWindow,
            };

            bool? result = window.ShowDialog();
            return result == true ? window._inputTextBox.Text : null;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}