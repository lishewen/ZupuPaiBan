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

    /// <summary>
    /// 获取节点对（含配偶）的中心X
    /// </summary>
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

public class LayoutResult
{
    public List<LayoutNode> Nodes { get; set; } = new();
    public List<ConnectionLine> Lines { get; set; } = new();
    public double TotalWidth { get; set; }
    public double TotalHeight { get; set; }
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
        result.TotalHeight = (CalculateMaxDepth(roots) + 1) * (_settings.NodeHeight + _settings.VerticalSpacing) + titleHeight;

        return result;
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

    /// <summary>
    /// 核心布局算法：先居中父节点对，再居中子节点于父节点对下方
    /// </summary>
    private void LayoutSubtree(LayoutNode node, double startX, double startY)
    {
        double pairWidth = GetPairWidth(node);
        node.Y = startY;

        // 第一步：将父节点对（或单个节点）居中于子树宽度内
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

        // 第二步：如果有子节点，将子节点整体居中于父节点对中心下方
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
