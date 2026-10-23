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
    private UIElement? _draggedElement;
    private int _draggedMemberId;
    private Point _dragStart;
    private double _dragStartLeft, _dragStartTop;

    // Node + line tracking for live connection updates
    private readonly Dictionary<int, (double left, double top, double w, double h)> _nodeRects = new();
    private readonly Dictionary<int, UIElement> _nodeElements = new();
    private readonly List<LineRecord> _lineRecords = new();
    private double _scale = 1.0, _offsetX = 0, _offsetY = 60;

    private class LineRecord
    {
        public FrameworkElement Element = null!;
        public int ParentId;
        public int ChildId;
        public bool IsSpouse;
    }

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
        _nodeRects.Clear();
        _nodeElements.Clear();
        _lineRecords.Clear();

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

        var nodes = layout.Nodes;
        double contentWidth = nodes.Count > 0 ? nodes.Max(n => n.X + n.Width + (n.SpouseNode?.Width ?? 0) + 20) : 400;
        double contentHeight = nodes.Count > 0 ? nodes.Max(n => n.Y + n.Height) : 400;
        _scale = Math.Min((pageWidth - 40) / Math.Max(contentWidth, 1), (pageHeight - 100) / Math.Max(contentHeight, 1));
        _scale = Math.Min(_scale, 1.0);
        _offsetX = (pageWidth - contentWidth * _scale) / 2;
        _offsetY = 60;

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

        // 1st pass: compute node rects (including drag offsets) so lines can reference them
        foreach (var node in nodes)
        {
            if (node.SpouseNode != null) continue; // spouse handled below
            RegisterNodeRect(node.Member.Id, node.X, node.Y, node.Width, node.Height);
            if (node.SpouseNode != null)
            {
                // spouse moves with main node offset
                RegisterNodeRect(node.SpouseNode.Member.Id, node.SpouseNode.X, node.SpouseNode.Y,
                    node.SpouseNode.Width, node.SpouseNode.Height, node.Member.Id);
            }
        }

        // 2nd pass: create line elements
        foreach (var line in layout.Lines)
        {
            if (line.IsSpouseLine)
            {
                var spouseLine = new Line
                {
                    Stroke = new SolidColorBrush(Color.FromRgb(180, 50, 50)),
                    StrokeThickness = 1.5,
                    Opacity = 0.6
                };
                previewCanvas.Children.Add(spouseLine);
                _lineRecords.Add(new LineRecord
                {
                    Element = spouseLine,
                    ParentId = line.ParentMemberId,
                    ChildId = line.ChildMemberId,
                    IsSpouse = true
                });
            }
            else
            {
                var pathShape = new Path
                {
                    Data = new PathGeometry(new[] { new PathFigure() }),
                    Stroke = new SolidColorBrush(Color.FromRgb(100, 70, 40)),
                    StrokeThickness = 1.2,
                    Opacity = 0.6
                };
                previewCanvas.Children.Add(pathShape);
                _lineRecords.Add(new LineRecord
                {
                    Element = pathShape,
                    ParentId = line.ParentMemberId,
                    ChildId = line.ChildMemberId,
                    IsSpouse = false
                });
            }
        }

        UpdateAllLines();

        // 3rd pass: create node visuals
        foreach (var node in nodes)
        {
            if (node.SpouseNode != null) continue;

            if (!_nodeRects.TryGetValue(node.Member.Id, out var rect)) continue;
            bool isExcluded = _viewModel.ExcludedMemberIds.Contains(node.Member.Id);

            var nodePanel = new Grid { Width = rect.w, Height = rect.h };

            var border = new Border
            {
                Width = rect.w,
                Height = rect.h,
                CornerRadius = new CornerRadius(3),
                Background = _viewModel.Settings.ShowNodeBorder
                    ? (node.Member.Gender == Gender.Male
                        ? new SolidColorBrush(Color.FromRgb(245, 245, 250))
                        : new SolidColorBrush(Color.FromRgb(255, 240, 245)))
                    : Brushes.Transparent,
                Opacity = isExcluded ? 0.3 : 1.0,
                Cursor = Cursors.Hand,
                Tag = node.Member.Id
            };

            if (_viewModel.Settings.ShowNodeBorder)
            {
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(120, 80, 40));
                border.BorderThickness = new Thickness(1);
            }

            var contentStack = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            string displayName = _viewModel.Settings.VerticalText
                ? string.Join("\n", node.Member.Name.ToCharArray())
                : node.Member.Name;

            contentStack.Children.Add(new TextBlock
            {
                Text = displayName,
                FontSize = Math.Max(8, (_viewModel.Settings.FontSize + 2) * _scale),
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(50, 30, 10))
            });

            string lifeText = node.Member.BirthDate;
            if (!string.IsNullOrEmpty(node.Member.DeathDate))
                lifeText += $" - {node.Member.DeathDate}";
            if (!string.IsNullOrEmpty(lifeText))
            {
                contentStack.Children.Add(new TextBlock
                {
                    Text = lifeText,
                    FontSize = Math.Max(6, (_viewModel.Settings.FontSize - 1) * _scale),
                    Foreground = new SolidColorBrush(Colors.DimGray),
                    HorizontalAlignment = HorizontalAlignment.Center
                });
            }

            border.Child = contentStack;

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

            Canvas.SetLeft(nodePanel, rect.left);
            Canvas.SetTop(nodePanel, rect.top);
            previewCanvas.Children.Add(nodePanel);
            _nodeElements[node.Member.Id] = nodePanel;

            // Spouse visual
            if (node.SpouseNode != null && _nodeRects.TryGetValue(node.SpouseNode.Member.Id, out var srect))
            {
                var spouse = node.SpouseNode;
                var spouseBorder = new Border
                {
                    Width = srect.w,
                    Height = srect.h,
                    CornerRadius = new CornerRadius(3),
                    Background = _viewModel.Settings.ShowNodeBorder
                        ? new SolidColorBrush(Color.FromRgb(255, 240, 245))
                        : Brushes.Transparent,
                    Opacity = isExcluded ? 0.3 : 1.0,
                    Cursor = Cursors.Hand,
                    Tag = node.Member.Id // spouse drags with main node
                };
                if (_viewModel.Settings.ShowNodeBorder)
                {
                    spouseBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(180, 100, 100));
                    spouseBorder.BorderThickness = new Thickness(0.8);
                }

                spouseBorder.Child = new TextBlock
                {
                    Text = spouse.Member.Name,
                    FontSize = Math.Max(8, _viewModel.Settings.FontSize * _scale),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromRgb(100, 50, 50))
                };

                Canvas.SetLeft(spouseBorder, srect.left);
                Canvas.SetTop(spouseBorder, srect.top);
                previewCanvas.Children.Add(spouseBorder);
                _nodeElements[spouse.Member.Id] = spouseBorder;
            }
        }
    }

    private void RegisterNodeRect(int memberId, double x, double y, double w, double h, int? offsetSourceId = null)
    {
        double nx = x * _scale + _offsetX;
        double ny = y * _scale + _offsetY;
        int offsetKey = offsetSourceId ?? memberId;
        if (_viewModel.PositionOffsets.TryGetValue(offsetKey, out var offset))
        {
            nx += offset.dx;
            ny += offset.dy;
        }
        _nodeRects[memberId] = (nx, ny, w * _scale, h * _scale);
    }

    /// <summary>
    /// 根据当前节点位置重新计算所有连接线
    /// </summary>
    private void UpdateAllLines()
    {
        foreach (var rec in _lineRecords)
        {
            if (!_nodeRects.TryGetValue(rec.ParentId, out var pr) || !_nodeRects.TryGetValue(rec.ChildId, out var cr))
                continue;

            if (rec.IsSpouse)
            {
                if (rec.Element is Line ln)
                {
                    var (sx1, sy1, sx2, sy2) = EdgeAnchors(pr, cr);
                    ln.X1 = sx1; ln.Y1 = sy1; ln.X2 = sx2; ln.Y2 = sy2;
                }
            }
            else
            {
                if (rec.Element is Path path)
                {
                    // parent bottom-center -> child top-center (elbow)
                    double px = pr.left + pr.w / 2;
                    double py = pr.top + pr.h;
                    double cx = cr.left + cr.w / 2;
                    double cy = cr.top;
                    double midY = (py + cy) / 2;

                    var figure = new PathFigure { StartPoint = new Point(px, py) };
                    figure.Segments = new PathSegmentCollection
                    {
                        new LineSegment(new Point(px, midY), true),
                        new LineSegment(new Point(cx, midY), true),
                        new LineSegment(new Point(cx, cy), true)
                    };
                    path.Data = new PathGeometry(new[] { figure });
                }
            }
        }
    }

    /// <summary>
    /// 计算两个矩形相对边缘的连接锚点（用于配偶线）
    /// </summary>
    private static (double, double, double, double) EdgeAnchors(
        (double left, double top, double w, double h) a,
        (double left, double top, double w, double h) b)
    {
        double acx = a.left + a.w / 2, acy = a.top + a.h / 2;
        double bcx = b.left + b.w / 2, bcy = b.top + b.h / 2;

        double ax, ay, bx, by;
        if (Math.Abs(a.left + a.w - b.left) < 1 || Math.Abs(b.left + b.w - a.left) < 1)
        {
            // horizontal neighbors: connect facing edges
            if (a.left < b.left)
            {
                ax = a.left + a.w; bx = b.left;
            }
            else
            {
                ax = a.left; bx = b.left + b.w;
            }
            ay = acy; by = bcy;
        }
        else
        {
            ax = acx; ay = acy; bx = bcx; by = bcy;
        }
        return (ax, ay, bx, by);
    }

    // Drag handlers
    private void PreviewCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var element = FindNodeBorder(e.OriginalSource as DependencyObject);
        if (element?.Tag is int memberId && _nodeElements.TryGetValue(memberId, out var topEl))
        {
            _isDragging = true;
            _draggedElement = topEl;
            _draggedMemberId = memberId;
            _dragStart = e.GetPosition(previewCanvas);
            _dragStartLeft = Canvas.GetLeft(topEl);
            _dragStartTop = Canvas.GetTop(topEl);

            // 记录配偶节点初始位置（随主节点一起移动）
            _spouseEl = null;
            var node = _viewModel.LastLayoutResult.Nodes
                .FirstOrDefault(n => n.Member.Id == memberId && n.SpouseNode != null);
            if (node?.SpouseNode != null && _nodeElements.TryGetValue(node.SpouseNode.Member.Id, out var sel)
                && sel != topEl)
            {
                _spouseEl = sel;
                _spouseStartLeft = Canvas.GetLeft(sel);
                _spouseStartTop = Canvas.GetTop(sel);
            }

            previewCanvas.CaptureMouse();
        }
    }

    private UIElement? _spouseEl;
    private double _spouseStartLeft, _spouseStartTop;

    private void PreviewCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || _draggedElement == null) return;

        var current = e.GetPosition(previewCanvas);
        double dx = current.X - _dragStart.X;
        double dy = current.Y - _dragStart.Y;

        double newLeft = _dragStartLeft + dx;
        double newTop = _dragStartTop + dy;
        Canvas.SetLeft(_draggedElement, newLeft);
        Canvas.SetTop(_draggedElement, newTop);

        if (_nodeRects.TryGetValue(_draggedMemberId, out var r))
            _nodeRects[_draggedMemberId] = (newLeft, newTop, r.w, r.h);

        // 配偶同步移动
        if (_spouseEl != null)
        {
            double sLeft = _spouseStartLeft + dx;
            double sTop = _spouseStartTop + dy;
            Canvas.SetLeft(_spouseEl, sLeft);
            Canvas.SetTop(_spouseEl, sTop);
            // 找到配偶 memberId
            var spouseKvp = _nodeElements.FirstOrDefault(kvp => kvp.Value == _spouseEl);
            if (spouseKvp.Key != 0 && _nodeRects.TryGetValue(spouseKvp.Key, out var sr))
                _nodeRects[spouseKvp.Key] = (sLeft, sTop, sr.w, sr.h);
        }

        UpdateAllLines();
    }

    private void PreviewCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging || _draggedElement == null) { _isDragging = false; return; }

        var node = _viewModel.LastLayoutResult.Nodes.FirstOrDefault(n => n.Member.Id == _draggedMemberId);
        if (node != null && _scale > 0)
        {
            double origX = node.X * _scale + _offsetX;
            double origY = node.Y * _scale + _offsetY;
            double currentLeft = Canvas.GetLeft(_draggedElement);
            double currentTop = Canvas.GetTop(_draggedElement);
            _viewModel.UpdatePositionOffset(_draggedMemberId, (currentLeft - origX) / _scale, (currentTop - origY) / _scale);
        }

        _isDragging = false;
        _draggedElement = null;
        _spouseEl = null;
        previewCanvas.ReleaseMouseCapture();
    }

    private Border? FindNodeBorder(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is Border b && b.Tag is int) return b;
            if (source is CheckBox) return null;
            source = VisualTreeHelper.GetParent(source);
        }
        return null;
    }

    private void NodeCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox chk && chk.Tag is int memberId)
        {
            if (chk.IsChecked == true)
            {
                _viewModel.ExcludedMemberIds.Remove(memberId);
                foreach (var id in GetDescendantIds(memberId))
                    _viewModel.ExcludedMemberIds.Remove(id);
            }
            else
            {
                _viewModel.ExcludedMemberIds.Add(memberId);
                foreach (var id in GetDescendantIds(memberId))
                    _viewModel.ExcludedMemberIds.Add(id);
            }

            _viewModel.RefreshPreviewCommand.Execute(null);
        }
    }

    private IEnumerable<int> GetDescendantIds(int parentId)
    {
        var result = new List<int>();
        var allMembers = _viewModel.AllMembers;
        var queue = new Queue<int>();
        queue.Enqueue(parentId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var child in allMembers.Where(m => m.FatherId == current))
            {
                result.Add(child.Id);
                queue.Enqueue(child.Id);
            }
        }
        return result;
    }

    private void BtnPrintRange_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var allMembers = _viewModel.AllMembers;
            if (allMembers == null || allMembers.Count == 0) return;

            var dialog = new PrintRangeDialog(allMembers) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                if (dialog.ShowAllMembers)
                    _viewModel.SetPrintRange(null, 20);
                else
                    _viewModel.SetPrintRange(dialog.SelectedRootMemberId, dialog.SelectedGenerations);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"打开打印范围对话框失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
