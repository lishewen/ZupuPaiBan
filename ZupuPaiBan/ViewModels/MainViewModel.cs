using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZupuPaiBan.Data;
using ZupuPaiBan.Models;

namespace ZupuPaiBan.ViewModels;

public partial class MemberTreeNode : ObservableObject
{
    [ObservableProperty] private FamilyMember _member = null!;
    [ObservableProperty] private bool _isExpanded = true;
    public ObservableCollection<MemberTreeNode> Children { get; } = new();
}

public partial class MainViewModel : ObservableObject
{
    private readonly DataService _dataService;

    [ObservableProperty]
    private ObservableCollection<MemberTreeNode> _treeNodes = new();

    [ObservableProperty]
    private MemberTreeNode? _selectedNode;

    [ObservableProperty]
    private string _statusText = "就绪";

    public MainViewModel(DataService dataService)
    {
        _dataService = dataService;
    }

    public async Task LoadDataAsync()
    {
        try
        {
            var allMembers = await _dataService.GetAllMembersAsync();
            var memberDict = allMembers.ToDictionary(m => m.Id);
            var nodeDict = new Dictionary<int, MemberTreeNode>();

            // 创建所有节点
            foreach (var member in allMembers)
            {
                nodeDict[member.Id] = new MemberTreeNode { Member = member };
            }

            // 构建树
            var roots = new ObservableCollection<MemberTreeNode>();
            foreach (var member in allMembers)
            {
                if (member.FatherId != null && nodeDict.ContainsKey(member.FatherId.Value))
                {
                    nodeDict[member.FatherId.Value].Children.Add(nodeDict[member.Id]);
                }
                else
                {
                    roots.Add(nodeDict[member.Id]);
                }
            }

            TreeNodes = roots;
            StatusText = $"已加载 {allMembers.Count} 位成员";
        }
        catch (Exception ex)
        {
            StatusText = $"加载失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task AddRootMemberAsync()
    {
        var member = new FamilyMember
        {
            Name = "新成员",
            Generation = 1,
            BirthOrder = await _dataService.GetNextBirthOrderAsync(null)
        };

        await _dataService.AddMemberAsync(member);
        await LoadDataAsync();
        StatusText = $"已添加根成员: {member.Name}";
    }

    [RelayCommand]
    private async Task AddChildMemberAsync()
    {
        if (SelectedNode == null)
        {
            MessageBox.Show("请先选择一个成员", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var parent = SelectedNode.Member;
        var member = new FamilyMember
        {
            Name = "新成员",
            FatherId = parent.Id,
            Generation = parent.Generation + 1,
            BirthOrder = await _dataService.GetNextBirthOrderAsync(parent.Id)
        };

        await _dataService.AddMemberAsync(member);
        SelectedNode.IsExpanded = true;
        await LoadDataAsync();
        StatusText = $"已添加子成员: {member.Name}";
    }

    [RelayCommand]
    private async Task EditMemberAsync()
    {
        if (SelectedNode == null)
        {
            MessageBox.Show("请先选择一个成员", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // 实际编辑在 View 层通过对话框完成
        OnEditMemberRequested?.Invoke(this, SelectedNode.Member);
    }

    [RelayCommand]
    private async Task DeleteMemberAsync()
    {
        if (SelectedNode == null)
        {
            MessageBox.Show("请先选择一个成员", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            $"确定要删除成员 \"{SelectedNode.Member.Name}\" 吗？\n其子成员将变为根成员。",
            "确认删除",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            await _dataService.DeleteMemberAsync(SelectedNode.Member.Id);
            await LoadDataAsync();
            StatusText = "已删除成员";
        }
    }

    // 事件：请求编辑成员（由 View 层处理对话框显示）
    public event EventHandler<FamilyMember>? OnEditMemberRequested;

    [RelayCommand]
    private void OpenPreview()
    {
        OnOpenPreviewRequested?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? OnOpenPreviewRequested;
}
