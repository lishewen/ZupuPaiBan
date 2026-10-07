using ZupuPaiBan.Models;

namespace ZupuPaiBan.Services;

public class LayoutNode
{
    public FamilyMember Member { get; set; } = null!;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public LayoutNode? SpouseNode { get; set; }
    public List<LayoutNode> Children { get; set; } = new();
    public double SubtreeWidth { get; set; }
    public int Generation { get; set; }

    public double PairCenterX
    {
        get
        {
            if (SpouseNode != null)
                return (X + SpouseNode.X + SpouseNode.Width) / 2;
            return X + Width / 2;
        }
    }
}

public class ConnectionLine
{
    public double ParentX { get; set; }
    public double ParentY { get; set; }
    public double ChildX { get; set; }
    public double ChildY { get; set; }
    public bool IsSpouseLine { get; set; }
}

public class PageLayout
{
    public int PageNumber { get; set; }
    public List<LayoutNode> Nodes { get; set; } = new();
    public List<ConnectionLine> Lines { get; set; } = new();
    public double ContentHeight { get; set; }
    public int StartGeneration { get; set; }
    public int EndGeneration { get; set; }
}

public class LayoutResult
{
    public List<LayoutNode> Nodes { get; set; } = new();
    public List<ConnectionLine> Lines { get; set; } = new();
    public List<PageLayout> Pages { get; set; } = new();
    public double TotalWidth { get; set; }
    public double TotalHeight { get; set; }
    public int TotalPages => Pages.Count > 0 ? Pages.Count : 1;
}

public class TreeLayoutService
{
    private readonly LayoutSettings _settings;
    private const double SpouseGap = 10;

    public TreeLayoutService(LayoutSettings settings)
    {
        _settings = settings;
    }

    public LayoutResult CalculateLayout(List<FamilyMember> allMembers)
    {
        var result = new LayoutResult();
        if (allMembers.Count == 0) return result;

        var roots = new List<LayoutNode>();
        var nodeDict = new Dictionary<int, LayoutNode>();

        foreach (var member in allMembers)
        {
            nodeDict[member.Id] = CreateLayoutNode(member);
        }

        foreach (var member in allMembers)
        {
            if (member.FatherId != null && nodeDict.ContainsKey(member.FatherId.Value))
                nodeDict[member.FatherId.Value].Children.Add(nodeDict[member.Id]);
            else
                roots.Add(nodeDict[member.Id]);
        }

        foreach (var node in nodeDict.Values)
            node.Children = node.Children.OrderBy(c => c.Member.BirthOrder).ToList();
        roots = roots.OrderBy(r => r.Member.BirthOrder).ToList();

        // 设置每个节点的代数
        foreach (var root in roots)
            SetGeneration(root, 1);

        // 根据布局模式选择不同的布局算法
        if (_settings.LayoutMode == LayoutMode.Vertical)
        {
            return CalculateVerticalLayout(roots);
        }

        // 水平布局（默认）
        foreach (var root in roots)
            CalculateSubtreeWidth(root);

        double totalWidth = 0;
        foreach (var root in roots)
        {
            totalWidth += root.SubtreeWidth + _settings.HorizontalSpacing;
        }
        if (roots.Count > 0) totalWidth -= _settings.HorizontalSpacing;

        double currentX = 0;
        double titleHeight = 60;
        foreach (var root in roots)
        {
            LayoutSubtree(root, currentX, titleHeight);
            currentX += root.SubtreeWidth + _settings.HorizontalSpacing;
        }

        foreach (var root in roots)
            CollectNodes(root, result);

        result.TotalWidth = Math.Max(totalWidth, _settings.PageWidth);
        int maxDepth = CalculateMaxDepth(roots);
        result.TotalHeight = (maxDepth + 1) * (_settings.NodeHeight + _settings.VerticalSpacing) + titleHeight;

        // 分页处理
        PaginateLayout(result, roots, titleHeight);

        return result;
    }

    /// <summary>
    /// 竖版世系图布局：从左到右展开
    /// </summary>
    private LayoutResult CalculateVerticalLayout(List<LayoutNode> roots)
    {
        var result = new LayoutResult();
        double titleHeight = 60;

        // 计算每个节点的子树高度（竖版中是垂直方向）
        foreach (var root in roots)
            CalculateSubtreeHeight(root);

        // 计算总高度
        double totalHeight = 0;
        foreach (var root in roots)
        {
            totalHeight += root.SubtreeWidth + _settings.VerticalSpacing;
        }
        if (roots.Count > 0) totalHeight -= _settings.VerticalSpacing;

        // 布局每个根节点
        double currentY = titleHeight;
        double columnWidth = _settings.NodeWidth + _settings.HorizontalSpacing;
        int maxGeneration = roots.Count > 0 ? roots.Max(r => GetMaxGeneration(r)) : 1;

        foreach (var root in roots)
        {
            LayoutVerticalSubtree(root, 0, currentY);
            currentY += root.SubtreeWidth + _settings.VerticalSpacing;
        }

        foreach (var root in roots)
            CollectNodes(root, result);

        result.TotalWidth = maxGeneration * columnWidth + _settings.HorizontalSpacing;
        result.TotalHeight = Math.Max(totalHeight + titleHeight, _settings.PageHeight);

        // 分页处理（竖版按列分页）
        PaginateVerticalLayout(result, roots, titleHeight);

        return result;
    }

    private void CalculateSubtreeHeight(LayoutNode node)
    {
        if (node.Children.Count == 0)
        {
            node.SubtreeWidth = GetPairWidth(node);
            return;
        }

        double childrenHeight = 0;
        foreach (var child in node.Children)
        {
            CalculateSubtreeHeight(child);
            childrenHeight += child.SubtreeWidth;
        }
        childrenHeight += (node.Children.Count - 1) * _settings.VerticalSpacing;

        node.SubtreeWidth = Math.Max(childrenHeight, GetPairWidth(node));
    }

    private void LayoutVerticalSubtree(LayoutNode node, double startX, double startY)
    {
        double pairWidth = GetPairWidth(node);
        node.X = startX;
        node.Y = startY + node.SubtreeWidth / 2 - pairWidth / 2;

        if (node.SpouseNode != null)
        {
            node.SpouseNode.X = node.X;
            node.SpouseNode.Y = node.Y + node.Height + SpouseGap;
        }

        if (node.Children.Count > 0)
        {
            double childrenTotalHeight = 0;
            foreach (var child in node.Children)
                childrenTotalHeight += child.SubtreeWidth;
            childrenTotalHeight += (node.Children.Count - 1) * _settings.VerticalSpacing;

            double childStartY = startY + (node.SubtreeWidth - childrenTotalHeight) / 2;
            double childX = startX + _settings.NodeWidth + _settings.HorizontalSpacing;

            foreach (var child in node.Children)
            {
                LayoutVerticalSubtree(child, childX, childStartY);
                childStartY += child.SubtreeWidth + _settings.VerticalSpacing;
            }
        }
    }

    private int GetMaxGeneration(LayoutNode node)
    {
        if (node.Children.Count == 0) return node.Generation;
        return node.Children.Max(GetMaxGeneration);
    }

    private void PaginateVerticalLayout(LayoutResult result, List<LayoutNode> roots, double titleHeight)
    {
        // 竖版布局按列分页
        double pageContentWidth = _settings.PageWidth - 40;
        double columnWidth = _settings.NodeWidth + _settings.HorizontalSpacing;
        int maxColumnsPerPage = Math.Max(1, (int)(pageContentWidth / columnWidth));

        int maxGeneration = result.Nodes.Count > 0 ? result.Nodes.Max(n => n.Generation) : 1;

        if (maxGeneration <= maxColumnsPerPage)
        {
            result.Pages.Add(new PageLayout
            {
                PageNumber = 1,
                Nodes = result.Nodes,
                Lines = result.Lines,
                ContentHeight = result.TotalHeight,
                StartGeneration = 1,
                EndGeneration = maxGeneration
            });
            return;
        }

        // 按代分页
        int currentPage = 1;
        int currentStartGen = 1;

        while (currentStartGen <= maxGeneration)
        {
            int currentEndGen = Math.Min(currentStartGen + maxColumnsPerPage - 1, maxGeneration);

            var pageNodes = result.Nodes.Where(n => n.Generation >= currentStartGen && n.Generation <= currentEndGen).ToList();
            var spouseNodes = new List<LayoutNode>();
            foreach (var node in pageNodes.ToList())
            {
                if (node.SpouseNode != null && !pageNodes.Contains(node.SpouseNode))
                    spouseNodes.Add(node.SpouseNode);
            }
            pageNodes.AddRange(spouseNodes);

            var pageLines = result.Lines.Where(l =>
            {
                if (l.IsSpouseLine)
                    return pageNodes.Any(n =>
                        (Math.Abs(n.X - l.ParentX) < 1 && Math.Abs(n.Y - l.ParentY) < 1) ||
                        (Math.Abs(n.X - l.ChildX) < 1 && Math.Abs(n.Y - l.ChildY) < 1));
                var parentInPage = pageNodes.Any(n => Math.Abs(n.X + n.Width - l.ParentX) < 1);
                var childInPage = pageNodes.Any(n => Math.Abs(n.X - l.ChildX) < 1);
                return parentInPage && childInPage;
            }).ToList();

            // 调整X坐标
            double minX = pageNodes.Count > 0 ? pageNodes.Min(n => n.X) : 0;
            foreach (var node in pageNodes)
            {
                node.X -= minX - 20;
                if (node.SpouseNode != null)
                    node.SpouseNode.X = node.X;
            }
            foreach (var line in pageLines)
            {
                line.ParentX -= minX - 20;
                line.ChildX -= minX - 20;
            }

            result.Pages.Add(new PageLayout
            {
                PageNumber = currentPage,
                Nodes = pageNodes,
                Lines = pageLines,
                ContentHeight = result.TotalHeight,
                StartGeneration = currentStartGen,
                EndGeneration = currentEndGen
            });

            currentStartGen = currentEndGen + 1;
            currentPage++;
        }
    }

    private void SetGeneration(LayoutNode node, int generation)
    {
        node.Generation = generation;
        foreach (var child in node.Children)
        {
            SetGeneration(child, generation + 1);
        }
    }

    private void PaginateLayout(LayoutResult result, List<LayoutNode> roots, double titleHeight)
    {
        double pageContentHeight = _settings.PageHeight - titleHeight - 20; // 减去标题和边距
        double rowHeight = _settings.NodeHeight + _settings.VerticalSpacing;
        
        // 计算每页能容纳多少代
        int maxGenerationsPerPage = Math.Max(1, (int)(pageContentHeight / rowHeight));
        
        // 获取所有代数
        int maxGeneration = result.Nodes.Count > 0 ? result.Nodes.Max(n => n.Generation) : 1;
        
        // 如果总代数小于等于每页可容纳的代数，则不需要分页
        if (maxGeneration <= maxGenerationsPerPage)
        {
            result.Pages.Add(new PageLayout
            {
                PageNumber = 1,
                Nodes = result.Nodes,
                Lines = result.Lines,
                ContentHeight = result.TotalHeight,
                StartGeneration = 1,
                EndGeneration = maxGeneration
            });
            return;
        }

        // 分页
        int currentPage = 1;
        int currentStartGen = 1;
        
        while (currentStartGen <= maxGeneration)
        {
            int currentEndGen = Math.Min(currentStartGen + maxGenerationsPerPage - 1, maxGeneration);
            
            var pageNodes = result.Nodes.Where(n => n.Generation >= currentStartGen && n.Generation <= currentEndGen).ToList();
            
            // 包含配偶节点
            var spouseNodes = new List<LayoutNode>();
            foreach (var node in pageNodes.ToList())
            {
                if (node.SpouseNode != null && !pageNodes.Contains(node.SpouseNode))
                {
                    spouseNodes.Add(node.SpouseNode);
                }
            }
            pageNodes.AddRange(spouseNodes);
            
            // 过滤连线
            var pageLines = result.Lines.Where(l =>
            {
                // 配偶连线：只要一方在当前页就显示
                if (l.IsSpouseLine)
                {
                    return pageNodes.Any(n => 
                        (Math.Abs(n.X - l.ParentX) < 1 && Math.Abs(n.Y + n.Height / 2 - l.ParentY) < 1) ||
                        (Math.Abs(n.X - l.ChildX) < 1 && Math.Abs(n.Y + n.Height / 2 - l.ChildY) < 1));
                }
                // 父子连线：两端都在当前页才显示
                var parentInPage = pageNodes.Any(n => Math.Abs(n.PairCenterX - l.ParentX) < 1 && Math.Abs(n.Y + n.Height - l.ParentY) < 1);
                var childInPage = pageNodes.Any(n => Math.Abs(n.PairCenterX - l.ChildX) < 1 && Math.Abs(n.Y - l.ChildY) < 1);
                return parentInPage && childInPage;
            }).ToList();

            // 调整Y坐标，让每页从顶部开始
            double minY = pageNodes.Count > 0 ? pageNodes.Min(n => n.Y) : 0;
            foreach (var node in pageNodes)
            {
                node.Y -= minY - titleHeight;
                if (node.SpouseNode != null)
                {
                    node.SpouseNode.Y = node.Y;
                }
            }
            
            // 调整连线的Y坐标
            foreach (var line in pageLines)
            {
                line.ParentY -= minY - titleHeight;
                line.ChildY -= minY - titleHeight;
            }

            double pageHeight = pageNodes.Count > 0 ? pageNodes.Max(n => n.Y + n.Height) - titleHeight + 20 : 0;

            result.Pages.Add(new PageLayout
            {
                PageNumber = currentPage,
                Nodes = pageNodes,
                Lines = pageLines,
                ContentHeight = pageHeight,
                StartGeneration = currentStartGen,
                EndGeneration = currentEndGen
            });

            currentStartGen = currentEndGen + 1;
            currentPage++;
        }
    }

    private LayoutNode CreateLayoutNode(FamilyMember member)
    {
        var node = new LayoutNode
        {
            Member = member,
            Width = _settings.NodeWidth,
            Height = _settings.NodeHeight
        };

        if (!string.IsNullOrEmpty(member.SpouseName) && _settings.ShowSpouse)
        {
            node.SpouseNode = new LayoutNode
            {
                Member = new FamilyMember
                {
                    Id = -member.Id,
                    Name = member.SpouseName,
                    Gender = member.Gender == Gender.Male ? Gender.Female : Gender.Male,
                    Generation = member.Generation
                },
                Width = _settings.NodeWidth,
                Height = _settings.NodeHeight
            };
        }

        return node;
    }

    private double GetPairWidth(LayoutNode node)
    {
        if (node.SpouseNode != null)
            return node.Width + SpouseGap + node.SpouseNode.Width;
        return node.Width;
    }

    private void CalculateSubtreeWidth(LayoutNode node)
    {
        if (node.Children.Count == 0)
        {
            node.SubtreeWidth = GetPairWidth(node);
            return;
        }

        double childrenWidth = 0;
        foreach (var child in node.Children)
        {
            CalculateSubtreeWidth(child);
            childrenWidth += child.SubtreeWidth;
        }
        childrenWidth += (node.Children.Count - 1) * _settings.HorizontalSpacing;

        node.SubtreeWidth = Math.Max(childrenWidth, GetPairWidth(node));
    }

    private void LayoutSubtree(LayoutNode node, double startX, double startY)
    {
        double pairWidth = GetPairWidth(node);
        node.Y = startY;

        double subtreeCenter = startX + node.SubtreeWidth / 2;

        if (node.SpouseNode != null)
        {
            node.X = subtreeCenter - pairWidth / 2;
            node.SpouseNode.X = node.X + node.Width + SpouseGap;
            node.SpouseNode.Y = startY;
        }
        else
        {
            node.X = subtreeCenter - node.Width / 2;
        }

        if (node.Children.Count > 0)
        {
            double childrenTotalWidth = 0;
            foreach (var child in node.Children)
                childrenTotalWidth += child.SubtreeWidth;
            childrenTotalWidth += (node.Children.Count - 1) * _settings.HorizontalSpacing;

            double pairCenter = node.PairCenterX;
            double childrenStartX = pairCenter - childrenTotalWidth / 2;
            double childX = childrenStartX;
            double childY = startY + _settings.NodeHeight + _settings.VerticalSpacing;

            foreach (var child in node.Children)
            {
                LayoutSubtree(child, childX, childY);
                childX += child.SubtreeWidth + _settings.HorizontalSpacing;
            }
        }
    }

    private void CollectNodes(LayoutNode node, LayoutResult result)
    {
        result.Nodes.Add(node);

        if (node.SpouseNode != null)
        {
            result.Nodes.Add(node.SpouseNode);
            result.Lines.Add(new ConnectionLine
            {
                ParentX = node.X + node.Width,
                ParentY = node.Y + node.Height / 2,
                ChildX = node.SpouseNode.X,
                ChildY = node.SpouseNode.Y + node.SpouseNode.Height / 2,
                IsSpouseLine = true
            });
        }

        foreach (var child in node.Children)
        {
            result.Lines.Add(new ConnectionLine
            {
                ParentX = node.PairCenterX,
                ParentY = node.Y + node.Height,
                ChildX = child.PairCenterX,
                ChildY = child.Y,
                IsSpouseLine = false
            });
            CollectNodes(child, result);
        }
    }

    private int CalculateMaxDepth(List<LayoutNode> roots)
    {
        int max = 0;
        foreach (var root in roots)
            max = Math.Max(max, GetDepth(root));
        return max;
    }

    private int GetDepth(LayoutNode node)
    {
        if (node.Children.Count == 0) return 0;
        return 1 + node.Children.Max(GetDepth);
    }
}
