using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Formatting;

namespace BookmarkStudio
{
    internal sealed class BookmarkGlyphMouseProcessor : MouseProcessorBase
    {
        private const double DragThreshold = 5.0;

        private readonly IWpfTextViewHost _textViewHost;
        private readonly IWpfTextViewMargin _margin;

        private string? _draggedBookmarkId;
        private Point _dragStartPosition;
        private bool _isDragging;
        private DropIndicatorAdorner? _dropIndicatorAdorner;
        private int _originalLineNumber;

        public BookmarkGlyphMouseProcessor(IWpfTextViewHost textViewHost, IWpfTextViewMargin margin)
        {
            _textViewHost = textViewHost;
            _margin = margin;
        }

        public override void PreprocessMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            FrameworkElement marginElement = _margin.VisualElement;
            Point position = e.GetPosition(marginElement);

            string? bookmarkId = FindBookmarkIdAtPosition(position);
            if (!string.IsNullOrEmpty(bookmarkId))
            {
                _draggedBookmarkId = bookmarkId;
                _dragStartPosition = position;
                _isDragging = false;
                _originalLineNumber = GetLineNumberFromYPosition(position.Y);

                marginElement.CaptureMouse();
                e.Handled = true;
            }

            base.PreprocessMouseLeftButtonDown(e);
        }

        public override void PreprocessMouseMove(MouseEventArgs e)
        {
            if (_draggedBookmarkId is null)
            {
                base.PreprocessMouseMove(e);
                return;
            }

            FrameworkElement marginElement = _margin.VisualElement;
            Point currentPosition = e.GetPosition(marginElement);

            if (!_isDragging)
            {
                double distance = Math.Sqrt(
                    Math.Pow(currentPosition.X - _dragStartPosition.X, 2) +
                    Math.Pow(currentPosition.Y - _dragStartPosition.Y, 2));

                if (distance >= DragThreshold)
                {
                    _isDragging = true;
                    CreateDropIndicator();
                }
            }

            if (_isDragging)
            {
                UpdateDropIndicator(currentPosition.Y);
                e.Handled = true;
            }

            base.PreprocessMouseMove(e);
        }

        public override void PreprocessMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            FrameworkElement marginElement = _margin.VisualElement;

            if (_draggedBookmarkId is not null && _isDragging)
            {
                Point position = e.GetPosition(marginElement);
                int targetLineNumber = GetLineNumberFromYPosition(position.Y);

                if (targetLineNumber > 0 && targetLineNumber != _originalLineNumber)
                {
                    string bookmarkId = _draggedBookmarkId;
                    string? lineText = GetLineText(targetLineNumber);

                    ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                    {
                        try
                        {
                            await BookmarkOperationsService.Current.MoveBookmarkToLineAsync(
                                bookmarkId,
                                targetLineNumber,
                                lineText,
                                CancellationToken.None);
                        }
                        catch (Exception ex)
                        {
                            await ex.LogAsync();
                        }
                    }).FireAndForget();
                }

                e.Handled = true;
            }

            CleanupDrag();
            marginElement.ReleaseMouseCapture();

            base.PreprocessMouseLeftButtonUp(e);
        }

        public override void PreprocessMouseRightButtonUp(MouseButtonEventArgs e)
        {
            FrameworkElement marginElement = _margin.VisualElement;
            Point position = e.GetPosition(marginElement);

            HitTestResult hitResult = VisualTreeHelper.HitTest(marginElement, position);

            if (hitResult?.VisualHit is not null)
            {
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

        private string? FindBookmarkIdAtPosition(Point position)
        {
            FrameworkElement marginElement = _margin.VisualElement;
            HitTestResult hitResult = VisualTreeHelper.HitTest(marginElement, position);

            if (hitResult?.VisualHit is null)
            {
                return null;
            }

            DependencyObject current = hitResult.VisualHit;

            while (current is not null)
            {
                if (current is FrameworkElement element && element.Tag is string bookmarkId)
                {
                    return bookmarkId;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private int GetLineNumberFromYPosition(double y)
        {
            IWpfTextView textView = _textViewHost.TextView;
            double viewY = y + textView.ViewportTop;

            ITextViewLine? textViewLine = textView.TextViewLines.GetTextViewLineContainingYCoordinate(viewY);
            if (textViewLine is null)
            {
                return -1;
            }

            ITextSnapshotLine snapshotLine = textViewLine.Start.GetContainingLine();
            return snapshotLine.LineNumber + 1;
        }

        private string? GetLineText(int lineNumber)
        {
            IWpfTextView textView = _textViewHost.TextView;
            ITextSnapshot snapshot = textView.TextSnapshot;

            int lineIndex = lineNumber - 1;
            if (lineIndex < 0 || lineIndex >= snapshot.LineCount)
            {
                return null;
            }

            ITextSnapshotLine line = snapshot.GetLineFromLineNumber(lineIndex);
            return line.GetText();
        }

        private void CreateDropIndicator()
        {
            FrameworkElement marginElement = _margin.VisualElement;
            AdornerLayer? adornerLayer = AdornerLayer.GetAdornerLayer(marginElement);
            if (adornerLayer is null)
            {
                return;
            }

            _dropIndicatorAdorner = new DropIndicatorAdorner(marginElement);
            adornerLayer.Add(_dropIndicatorAdorner);
        }

        private void UpdateDropIndicator(double mouseY)
        {
            if (_dropIndicatorAdorner is null)
            {
                return;
            }

            IWpfTextView textView = _textViewHost.TextView;
            double viewY = mouseY + textView.ViewportTop;

            ITextViewLine? textViewLine = textView.TextViewLines.GetTextViewLineContainingYCoordinate(viewY);
            if (textViewLine is null)
            {
                return;
            }

            double indicatorY = textViewLine.Top - textView.ViewportTop;
            _dropIndicatorAdorner.UpdatePosition(indicatorY);
        }

        private void CleanupDrag()
        {
            if (_dropIndicatorAdorner is not null)
            {
                AdornerLayer? adornerLayer = AdornerLayer.GetAdornerLayer(_margin.VisualElement);
                adornerLayer?.Remove(_dropIndicatorAdorner);
                _dropIndicatorAdorner = null;
            }

            _draggedBookmarkId = null;
            _isDragging = false;
            _originalLineNumber = 0;
        }

        private sealed class DropIndicatorAdorner : Adorner
        {
            private double _indicatorY;

            public DropIndicatorAdorner(UIElement adornedElement)
                : base(adornedElement)
            {
                IsHitTestVisible = false;
            }

            public void UpdatePosition(double y)
            {
                _indicatorY = y;
                InvalidateVisual();
            }

            protected override void OnRender(DrawingContext drawingContext)
            {
                Brush brush = new SolidColorBrush(Color.FromArgb(200, 0, 122, 204));
                brush.Freeze();

                Rect rect = new Rect(0, _indicatorY, AdornedElement.RenderSize.Width, 2);
                drawingContext.DrawRectangle(brush, null, rect);
            }
        }
    }
}
