using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ZupuPaiBan.Data;
using ZupuPaiBan.Models;
using ZupuPaiBan.ViewModels;
using ZupuPaiBan.Views;

namespace ZupuPaiBan;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        var context = new ZupuDbContext();
        var dataService = new DataService(context);
        _viewModel = new MainViewModel(dataService);
        DataContext = _viewModel;

        _viewModel.OnEditMemberRequested += OnEditMemberRequested;
        _viewModel.OnOpenPreviewRequested += OnOpenPreviewRequested;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var context = new ZupuDbContext();
        await context.Database.EnsureCreatedAsync();
        await _viewModel.LoadDataAsync();
    }

    private void TreeMembers_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (treeMembers.SelectedItem is MemberTreeNode node)
        {
            _viewModel.SelectedNode = node;
        }
    }

    private async void TreeMembers_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.SelectedNode != null)
        {
            await ShowEditDialog(_viewModel.SelectedNode.Member);
        }
    }

    private async void OnEditMemberRequested(object? sender, FamilyMember member)
    {
        await ShowEditDialog(member);
    }

    private async Task ShowEditDialog(FamilyMember member)
    {
        var editVm = new MemberEditViewModel(member);
        var dialog = new MemberEditDialog(editVm)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            editVm.ApplyToMember(member);
            var context = new ZupuDbContext();
            var dataService = new DataService(context);
            await dataService.UpdateMemberAsync(member);
            await _viewModel.LoadDataAsync();
            _viewModel.StatusText = $"已更新成员: {member.Name}";
        }
    }

    private async void OnOpenPreviewRequested(object? sender, EventArgs e)
    {
        var context = new ZupuDbContext();
        var dataService = new DataService(context);
        var printService = new Services.PrintService();
        var previewWindow = new PrintPreviewWindow(dataService, printService);
        previewWindow.Owner = this;
        previewWindow.Show();
    }

    private void BtnAbout_Click(object sender, RoutedEventArgs e)
    {
        var aboutDialog = new AboutDialog
        {
            Owner = this
        };
        aboutDialog.ShowDialog();
    }
}
