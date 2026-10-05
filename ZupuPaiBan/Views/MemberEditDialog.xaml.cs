using System.Windows;
using ZupuPaiBan.ViewModels;

namespace ZupuPaiBan.Views;

public partial class MemberEditDialog : Window
{
    private readonly MemberEditViewModel _viewModel;

    public MemberEditDialog(MemberEditViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_viewModel.Name))
        {
            MessageBox.Show("请输入姓名", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
