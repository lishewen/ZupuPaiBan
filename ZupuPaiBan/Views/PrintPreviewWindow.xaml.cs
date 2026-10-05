using System.Windows;
using System.Windows.Media;
using ZupuPaiBan.Data;
using ZupuPaiBan.Services;
using ZupuPaiBan.ViewModels;

namespace ZupuPaiBan.Views;

public partial class PrintPreviewWindow : Window
{
    private readonly PreviewViewModel _viewModel;

    public PrintPreviewWindow(DataService dataService, PrintService printService)
    {
        InitializeComponent();
        _viewModel = new PreviewViewModel(dataService, printService);
        DataContext = _viewModel;

        // 监听缩放变化
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(PreviewViewModel.ZoomLevel))
            {
                ApplyZoom();
            }
        };
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
        ApplyZoom();
    }

    private void ApplyZoom()
    {
        if (previewHost != null)
        {
            previewHost.LayoutTransform = new ScaleTransform(
                _viewModel.ZoomLevel, _viewModel.ZoomLevel);
        }
    }
}
