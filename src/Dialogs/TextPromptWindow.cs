using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.VisualStudio.PlatformUI;
using Microsoft.VisualStudio.Shell;

namespace BookmarkStudio
{
    internal sealed class TextPromptWindow : VsUIDialogWindow
    {
        private const int DwmwaCaptionColor = 35;
        private const int DwmwaTextColor = 36;

        private readonly TextBox _inputTextBox;
        private readonly bool _selectTextOnLoad;

        private TextPromptWindow(string title, string prompt, string initialValue, bool selectTextOnLoad)
        {
            _selectTextOnLoad = selectTextOnLoad;

            Title = title;
            Width = 420;
            Height = 160;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            HasMaximizeButton = false;
            HasMinimizeButton = false;
            HasHelpButton = false;
            SetResourceReference(BackgroundProperty, EnvironmentColors.ToolWindowBackgroundBrushKey);
            SetResourceReference(ForegroundProperty, EnvironmentColors.ToolWindowTextBrushKey);

            ThreadHelper.ThrowIfNotOnUIThread();
            if (Package.GetGlobalService(typeof(Microsoft.VisualStudio.Shell.Interop.SDTE)) is EnvDTE.DTE dte)
            {
                var hwnd = (IntPtr)dte.MainWindow.HWnd;
                if (hwnd != IntPtr.Zero)
                {
                    Owner = HwndSource.FromHwnd(hwnd)?.RootVisual as Window;
                }
            }

            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            Loaded += TextPromptWindow_Loaded;
            SourceInitialized += OnSourceInitialized;

            var grid = new Grid
            {
                Margin = new Thickness(12),
            };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var promptBlock = new Label
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
                Padding = new Thickness(4, 6, 4, 6),
                Text = initialValue ?? string.Empty,
            };
            _inputTextBox.SetResourceReference(StyleProperty, VsResourceKeys.ThemedDialogTextBoxStyleKey);
            Grid.SetRow(_inputTextBox, 1);
            grid.Children.Add(_inputTextBox);

            var buttons = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                Orientation = Orientation.Horizontal,
            };

            var okButton = new Button
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

            var cancelButton = new Button
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
            var window = new TextPromptWindow(title, prompt, initialValue, selectTextOnLoad);

            var result = window.ShowDialog();
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

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            try
            {
                ApplyTitleBarTheme();
            }
            catch (Exception ex)
            {
                _ = ex.LogAsync();
            }
        }

        private void ApplyTitleBarTheme()
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            if (TryGetResourceColor(EnvironmentColors.ToolWindowBackgroundBrushKey, out var captionColor))
            {
                int captionColorRef = ToColorRef(captionColor);
                _ = DwmSetWindowAttribute(handle, DwmwaCaptionColor, ref captionColorRef, sizeof(int));
            }

            if (TryGetResourceColor(EnvironmentColors.ToolWindowTextBrushKey, out var textColor))
            {
                int textColorRef = ToColorRef(textColor);
                _ = DwmSetWindowAttribute(handle, DwmwaTextColor, ref textColorRef, sizeof(int));
            }
        }

        private bool TryGetResourceColor(object key, out Color color)
        {
            if (TryFindResource(key) is SolidColorBrush brush)
            {
                color = brush.Color;
                return true;
            }

            color = default(Color);
            return false;
        }

        private static int ToColorRef(Color color)
            => color.R | (color.G << 8) | (color.B << 16);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int attributeValue, int attributeSize);
    }
}