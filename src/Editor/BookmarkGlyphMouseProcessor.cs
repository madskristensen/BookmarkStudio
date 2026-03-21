using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Editor;

namespace BookmarkStudio
{
    internal sealed class BookmarkGlyphMouseProcessor : MouseProcessorBase
    {
        private readonly IWpfTextViewHost _textViewHost;
        private readonly IWpfTextViewMargin _margin;

        public BookmarkGlyphMouseProcessor(IWpfTextViewHost textViewHost, IWpfTextViewMargin margin)
        {
            _textViewHost = textViewHost;
            _margin = margin;
        }

        public override void PreprocessMouseRightButtonUp(MouseButtonEventArgs e)
        {
            FrameworkElement marginElement = _margin.VisualElement;
            Point position = e.GetPosition(marginElement);

            // Hit test to find the glyph element at the click position
            HitTestResult hitResult = VisualTreeHelper.HitTest(marginElement, position);

            if (hitResult?.VisualHit is not null)
            {
                // Walk up the visual tree to find an element with a ContextMenu
                DependencyObject current = hitResult.VisualHit;

                while (current is not null)
                {
                    if (current is FrameworkElement element && element.ContextMenu is not null)
                    {
                        element.ContextMenu.PlacementTarget = element;
                        element.ContextMenu.IsOpen = true;
                        e.Handled = true;
                        return;
                    }

                    current = VisualTreeHelper.GetParent(current);
                }
            }

            base.PreprocessMouseRightButtonUp(e);
        }
    }
}
