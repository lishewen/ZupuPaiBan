using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ZupuPaiBan.Data;
using ZupuPaiBan.Models;
using ZupuPaiBan.Services;
using ZupuPaiBan.ViewModels;

namespace ZupuPaiBan.Views;

public partial class PrintPreviewWindow : Window
{
    private readonly PreviewViewModel _viewModel;
    private readonly DataService _dataService;

    // Drag state
    private bool _isDragging;
    private Border? _draggedElement;
    private int _draggedMemberId;
    private Point _dragStart;
    private double _dragStartLeft, _dragStartTop;

    public PrintPreviewWindow(DataService dataService, PrintService printService)
    {
        InitializeComponent();
        _dataService = dataService;
        _viewModel = new PreviewViewModel(dataService, printService);
        DataContext = _viewModel;

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
        RenderInteractiveCanvas();
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PreviewViewModel.PreviewContent))
        {
            Dispatcher.BeginInvoke(() => RenderInteractiveCanvas());
        }
        if (e.PropertyName == nameof(PreviewViewModel.ZoomLevel))
        {
            canvasScale.ScaleX = _viewModel.ZoomLevel;
            canvasScale.ScaleY = _viewModel.ZoomLevel;
        }
    }

    private void RenderInteractiveCanvas()
    {
        previewCanvas.Children.Clear();

        var layout = _viewModel.LastLayoutResult;
        if (layout.Nodes.Count == 0)
        {
            previewCanvas.Width = 800;
            previewCanvas.Height = 600;
            return;
        }

        double pageWidth = _viewModel.Settings.PageWidth;
        double pageHeight = _viewModel.Settings.PageHeight;
        previewCanvas.Width = pageWidth;
        previewCanvas.Height = pageHeight;
        pageBorder.Width = pageWidth;
        pageBorder.Height = pageHeight;

        // Calculate scale for fitting content
        var nodes = layout.Nodes;
        double contentWidth = nodes.Count > 0 ? nodes.Max(n => n.X + n.Width + (n.SpouseNode?.Width ?? 0) + 20) : 400;
        double contentHeight = nodes.Count > 0 ? nodes.Max(n => n.Y + n.Height) : 400;
        double scale = Math.Min((pageWidth - 40) / Math.Max(contentWidth, 1), (pageHeight - 100) / Math.Max(contentHeight, 1));
        scale = Math.Min(scale, 1.0);
        double offsetX = (pageWidth - contentWidth * scale) / 2;
        double offsetY = 60;

        // Draw title
        var titleBlock = new TextBlock
        {
            Text = _viewModel.Settings.Title,
            FontSize = _viewModel.Settings.TitleFontSize,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(80, 40, 10))
        };
        titleBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(titleBlock, (pageWidth - titleBlock.DesiredSize.Width) / 2);
        Canvas.SetTop(titleBlock, 15);
        previewCanvas.Children.Add(titleBlock);

        // Draw lines
        foreach (var line in layout.Lines)
        {
            double px1 = line.ParentX * scale + offsetX;
            double py1 = line.ParentY * scale + offsetY;
            double px2 = line.ChildX * scale + offsetX;
            double py2 = line.ChildY * scale + offsetY;

            // Apply offsets to lines
            var parentNode = nodes.FirstOrDefault(n => Math.Abs(n.X + n.Width - line.ParentX) < 5);
            var childNode = nodes.FirstOrDefault(n => Math.Abs(n.X - line.ChildX) < 5);
            if (parentNode != null && _viewModel.PositionOffsets.TryGetValue(parentNode.Member.Id, out var pOff))
            {
                px1 += pOff.dx * scale;
                py1 += pOff.dy * scale;
            }
            if (childNode != null && _viewModel.PositionOffsets.TryGetValue(childNode.Member.Id, out var cOff))
            {
                px2 += cOff.dx * scale;
                py2 += cOff.dy * scale;
            }

            if (line.IsSpouseLine)
            {
                var spouseLine = new Line
                {
                    X1 = px1, Y1 = py1, X2 = px2, Y2 = py2,
                    Stroke = new SolidColorBrush(Color.FromRgb(180, 50, 50)),
                    StrokeThickness = 1.5,
                    Opacity = 0.6
                };
                previewCanvas.Children.Add(spouseLine);
            }
            else
            {
                double midY = (py1 + py2) / 2;
                var pathFigure = new PathFigure { StartPoint = new Point(px1, py1) };
                pathFigure.Segments = new PathSegmentCollection
                {
                    new LineSegment(new Point(px1, midY), true),
                    new LineSegment(new Point(px2, midY), true),
                    new LineSegment(new Point(px2, py2), true)
                };
                var pathGeom = new PathGeometry(new[] { pathFigure });
                var pathShape = new Path
                {
                    Data = pathGeom,
                    Stroke = new SolidColorBrush(Color.FromRgb(100, 70, 40)),
                    StrokeThickness = 1.2,
                    Opacity = 0.6
                };
                previewCanvas.Children.Add(pathShape);
            }
        }

        // Draw nodes with checkboxes
        foreach (var node in nodes)
        {
            if (node.SpouseNode != null) continue; // Skip spouse nodes, rendered as part of main

            double nx = node.X * scale + offsetX;
            double ny = node.Y * scale + offsetY;
            double nw = node.Width * scale;
            double nh = node.Height * scale;

            // Apply drag offset
            if (_viewModel.PositionOffsets.TryGetValue(node.Member.Id, out var offset))
            {
                nx += offset.dx;
                ny += offset.dy;
            }

            bool isExcluded = _viewModel.ExcludedMemberIds.Contains(node.Member.Id);

            // Node container
            var nodePanel = new Grid
            {
                Width = nw,
                Height = nh
            };

            // Border (background + optional border)
            var border = new Border
            {
                Width = nw,
                Height = nh,
                CornerRadius = new CornerRadius(3),
                Background = node.Member.Gender == Gender.Male
                    ? new SolidColorBrush(Color.FromRgb(245, 245, 250))
                    : new SolidColorBrush(Color.FromRgb(255, 240, 245)),
                Opacity = isExcluded ? 0.3 : 1.0,
                Cursor = Cursors.Hand,
                Tag = node.Member.Id
            };

            if (_viewModel.Settings.ShowNodeBorder)
            {
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(120, 80, 40));
                border.BorderThickness = new Thickness(1);
            }

            // Content
            var contentStack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            string displayName = _viewModel.Settings.VerticalText
                ? string.Join("\n", node.Member.Name.ToCharArray())
                : node.Member.Name;

            var nameText = new TextBlock
            {
                Text = displayName,
                FontSize = Math.Max(8, (_viewModel.Settings.FontSize + 2) * scale),
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(50, 30, 10))
            };
            contentStack.Children.Add(nameText);

            string lifeText = node.Member.BirthDate;
            if (!string.IsNullOrEmpty(node.Member.DeathDate))
                lifeText += $" - {node.Member.DeathDate}";
            if (!string.IsNullOrEmpty(lifeText))
            {
                var lifeTextBlock = new TextBlock
                {
                    Text = lifeText,
                    FontSize = Math.Max(6, (_viewModel.Settings.FontSize - 1) * scale),
                    Foreground = new SolidColorBrush(Colors.DimGray),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                contentStack.Children.Add(lifeTextBlock);
            }

            border.Child = contentStack;

            // Checkbox overlay (top-left corner)
            var chk = new CheckBox
            {
                IsChecked = !isExcluded,
                Width = 16,
                Height = 16,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(2),
                Tag = node.Member.Id
            };
            chk.Checked += NodeCheckBox_Changed;
            chk.Unchecked += NodeCheckBox_Changed;

            nodePanel.Children.Add(border);
            nodePanel.Children.Add(chk);

            Canvas.SetLeft(nodePanel, nx);
            Canvas.SetTop(nodePanel, ny);
            previewCanvas.Children.Add(nodePanel);

            // Also render spouse if exists
            if (node.SpouseNode != null)
            {
                var spouse = node.SpouseNode;
                double sx = spouse.X * scale + offsetX;
                double sy = spouse.Y * scale + offsetY;
                double sw = spouse.Width * scale;
                double sh = spouse.Height * scale;

                if (_viewModel.PositionOffsets.TryGetValue(node.Member.Id, out var soffset))
                {
                    sx += soffset.dx;
                    sy += soffset.dy;
                }

                var spouseBorder = new Border
                {
                    Width = sw,
                    Height = sh,
                    CornerRadius = new CornerRadius(3),
                    Background = new SolidColorBrush(Color.FromRgb(255, 240, 245)),
                    Opacity = isExcluded ? 0.3 : 1.0
                };
                if (_viewModel.Settings.ShowNodeBorder)
                {
                    spouseBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(180, 100, 100));
                    spouseBorder.BorderThickness = new Thickness(0.8);
                }

                var spouseText = new TextBlock
                {
                    Text = spouse.Member.Name,
                    FontSize = Math.Max(8, _viewModel.Settings.FontSize * scale),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 50, 50))
                };
                spouseBorder.Child = spouseText;

                Canvas.SetLeft(spouseBorder, sx);
                Canvas.SetTop(spouseBorder, sy);
                previewCanvas.Children.Add(spouseBorder);
            }
        }
    }

    // Drag handlers
    private void PreviewCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var element = FindNodeBorder(e.OriginalSource as DependencyObject);
        if (element?.Tag is int memberId)
        {
            _isDragging = true;
            _draggedElement = element;
            _draggedMemberId = memberId;
            _dragStart = e.GetPosition(previewCanvas);
            _dragStartLeft = Canvas.GetLeft(element.Parent is Grid g ? g : element);
            _dragStartTop = Canvas.GetTop(element.Parent is Grid g2 ? g2 : element);
            element.CaptureMouse();
            previewCanvas.CaptureMouse();
        }
    }

    private void PreviewCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || _draggedElement == null) return;

        var current = e.GetPosition(previewCanvas);
        double dx = current.X - _dragStart.X;
        double dy = current.Y - _dragStart.Y;

        var parentEl = _draggedElement.Parent is Grid g ? (UIElement)g : _draggedElement;
        Canvas.SetLeft(parentEl, _dragStartLeft + dx);
        Canvas.SetTop(parentEl, _dragStartTop + dy);
    }

    private void PreviewCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging || _draggedElement == null) { _isDragging = false; return; }

        var parentEl = _draggedElement.Parent is Grid g ? (UIElement)g : _draggedElement;
        double currentLeft = Canvas.GetLeft(parentEl);
        double currentTop = Canvas.GetTop(parentEl);

        // Calculate new offset relative to original position
        var node = _viewModel.LastLayoutResult.Nodes.FirstOrDefault(n => n.Member.Id == _draggedMemberId);
        if (node != null)
        {
            double pageWidth = _viewModel.Settings.PageWidth;
            double pageHeight = _viewModel.Settings.PageHeight;
            double contentWidth = _viewModel.LastLayoutResult.Nodes.Max(n2 => n2.X + n2.Width + (n2.SpouseNode?.Width ?? 0) + 20);
            double contentHeight = _viewModel.LastLayoutResult.Nodes.Max(n2 => n2.Y + n2.Height);
            double scale = Math.Min((pageWidth - 40) / Math.Max(contentWidth, 1), (pageHeight - 100) / Math.Max(contentHeight, 1));
            scale = Math.Min(scale, 1.0);
            double offsetX = (pageWidth - contentWidth * scale) / 2;
            double offsetY = 60;

            double origX = node.X * scale + offsetX;
            double origY = node.Y * scale + offsetY;

            double ddx = currentLeft - origX;
            double ddy = currentTop - origY;

            // Store in raw (unscaled) coordinates
            if (scale > 0)
            {
                _viewModel.UpdatePositionOffset(_draggedMemberId, ddx / scale, ddy / scale);
            }
        }

        _isDragging = false;
        _draggedElement?.ReleaseMouseCapture();
        _draggedElement = null;
        previewCanvas.ReleaseMouseCapture();
    }

    private Border? FindNodeBorder(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is Border b && b.Tag is int) return b;
            if (source is CheckBox) return null; // Don't drag if clicking checkbox
            source = VisualTreeHelper.GetParent(source);
        }
        return null;
    }

    private void NodeCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox chk && chk.Tag is int memberId)
        {
            if (chk.IsChecked == true)
                _viewModel.ExcludedMemberIds.Remove(memberId);
            else
                _viewModel.ExcludedMemberIds.Add(memberId);

            _viewModel.RefreshPreviewCommand.Execute(null);
        }
    }

    private void BtnPrintRange_Click(object sender, RoutedEventArgs e)
    {
        var allMembers = _viewModel.AllMembers;
        if (allMembers.Count == 0) return;

        var dialog = new PrintRangeDialog(allMembers) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            if (dialog.ShowAllMembers)
            {
                _viewModel.SetPrintRange(null, 20);
            }
            else
            {
                _viewModel.SetPrintRange(dialog.SelectedRootMemberId, dialog.SelectedGenerations);
            }
            RenderInteractiveCanvas();
        }
    }

    private void BtnResetOffsets_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ClearPositionOffsets();
        RenderInteractiveCanvas();
    }

    private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ExcludedMemberIds.Clear();
        _viewModel.RefreshPreviewCommand.Execute(null);
        RenderInteractiveCanvas();
    }
}
