using System.IO;
using System.Windows;
using System.Windows.Controls;
using ZupuPaiBan.Models;
using ZupuPaiBan.Services;

namespace ZupuPaiBan.Views;

public partial class CollaborationExportDialog : Window
{
    private readonly List<FamilyMember> _allMembers;
    private int? _selectedMemberId;

    public CollaborationExportDialog(List<FamilyMember> allMembers)
    {
        InitializeComponent();
        _allMembers = allMembers;
        BuildTree();
    }

    private void BuildTree()
    {
        treeMembers.Items.Clear();
        var nodeDict = _allMembers.ToDictionary(m => m.Id, m => new TreeViewItem
        {
            Header = $"{m.Name} (第{m.Generation}代)",
            Tag = m.Id
        });

        foreach (var member in _allMembers)
        {
            var node = nodeDict[member.Id];
            if (member.FatherId != null && nodeDict.ContainsKey(member.FatherId.Value))
                nodeDict[member.FatherId.Value].Items.Add(node);
            else
                treeMembers.Items.Add(node);
        }

        foreach (TreeViewItem item in treeMembers.Items)
            item.IsExpanded = true;
    }

    private void TreeMembers_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is TreeViewItem item && item.Tag is int memberId)
        {
            _selectedMemberId = memberId;
            btnExport.IsEnabled = true;
            var member = _allMembers.FirstOrDefault(m => m.Id == memberId);
            int descendantCount = CountDescendants(memberId);
            txtInfo.Text = $"已选择: {member?.Name}，将导出 {descendantCount + 1} 位成员（含自身）";
        }
    }

    private int CountDescendants(int memberId)
    {
        int count = 0;
        var children = _allMembers.Where(m => m.FatherId == memberId);
        foreach (var child in children)
        {
            count += 1 + CountDescendants(child.Id);
        }
        return count;
    }

    private async void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMemberId == null) return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "协作导出",
            Filter = "协作 JSON 文件|*.json",
            FileName = $"协作导出_{_allMembers.FirstOrDefault(m => m.Id == _selectedMemberId)?.Name}.json"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var service = new CollaborationService();
            await service.ExportSubtreeJson(_selectedMemberId.Value, _allMembers, dialog.FileName);
            MessageBox.Show($"导出成功！\n文件: {dialog.FileName}", "协作导出", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
