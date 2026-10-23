using System.Windows;
using System.Windows.Controls;
using ZupuPaiBan.Data;
using ZupuPaiBan.Models;

namespace ZupuPaiBan.Views;

public partial class PrintRangeDialog : Window
{
    public int? SelectedRootMemberId { get; private set; }
    public int SelectedGenerations { get; private set; } = 5;
    public bool ShowAllMembers { get; private set; } = true;

    private readonly List<FamilyMember> _allMembers;

    public PrintRangeDialog(List<FamilyMember> allMembers)
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
            Header = m.Name,
            Tag = m.Id
        });

        foreach (var member in _allMembers)
        {
            var node = nodeDict[member.Id];
            if (member.FatherId != null && nodeDict.ContainsKey(member.FatherId.Value))
            {
                nodeDict[member.FatherId.Value].Items.Add(node);
            }
            else
            {
                treeMembers.Items.Add(node);
            }
        }

        // Expand all
        foreach (var item in treeMembers.Items)
        {
            if (item is TreeViewItem tvi) tvi.IsExpanded = true;
        }
    }

    private void TreeMembers_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is TreeViewItem item && item.Tag is int memberId)
        {
            SelectedRootMemberId = memberId;
            ShowAllMembers = false;
        }
    }

    private void SliderGenerations_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        SelectedGenerations = (int)e.NewValue;
        txtGenCount.Text = $"{SelectedGenerations} 代";
    }

    private void BtnAllMembers_Click(object sender, RoutedEventArgs e)
    {
        ShowAllMembers = true;
        SelectedRootMemberId = null;
        DialogResult = true;
        Close();
    }

    private void BtnConfirm_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRootMemberId == null)
        {
            MessageBox.Show("请选择一个起始成员", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        ShowAllMembers = false;
        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
