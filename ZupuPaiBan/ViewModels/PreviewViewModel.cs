using System.Windows;
using System.Windows.Documents;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ZupuPaiBan.Data;
using ZupuPaiBan.Models;
using ZupuPaiBan.Services;

namespace ZupuPaiBan.ViewModels;

public partial class PreviewViewModel : ObservableObject
{
    private readonly DataService _dataService;
    private readonly PrintService _printService;

    [ObservableProperty] private LayoutSettings _settings = new();
    [ObservableProperty] private FrameworkElement? _previewContent;
    [ObservableProperty] private double _zoomLevel = 1.0;
    [ObservableProperty] private string _statusText = "预览就绪";

    public PreviewViewModel(DataService dataService, PrintService printService)
    {
        _dataService = dataService;
        _printService = printService;

        // 监听设置变化，自动刷新
        Settings.PropertyChanged += (s, e) => RefreshPreview();
    }

    public async Task InitializeAsync()
    {
        await RefreshPreviewAsync();
    }

    [RelayCommand]
    private async Task RefreshPreviewAsync()
    {
        try
        {
            var allMembers = await _dataService.GetAllMembersAsync();
            if (allMembers.Count == 0)
            {
                StatusText = "没有成员数据";
                PreviewContent = null;
                return;
            }

            var layoutService = new TreeLayoutService(Settings);
            var layoutResult = layoutService.CalculateLayout(allMembers);
            var page = _printService.CreatePrintPage(layoutResult, Settings);

            PreviewContent = page;
            StatusText = $"预览: {allMembers.Count} 位成员, {Settings.PageSize} {GetOrientationText()}";
        }
        catch (Exception ex)
        {
            StatusText = $"预览失败: {ex.Message}";
        }
    }

    private void RefreshPreview()
    {
        // 异步刷新（不等待）
        _ = RefreshPreviewAsync();
    }

    private string GetOrientationText()
    {
        return Settings.Orientation == PageOrientation.Landscape ? "横向" : "纵向";
    }

    [RelayCommand]
    private void ZoomIn()
    {
        ZoomLevel = Math.Min(ZoomLevel + 0.1, 3.0);
    }

    [RelayCommand]
    private void ZoomOut()
    {
        ZoomLevel = Math.Max(ZoomLevel - 0.1, 0.2);
    }

    [RelayCommand]
    private void ZoomFit()
    {
        ZoomLevel = 1.0;
    }

    [RelayCommand]
    private void Print()
    {
        if (PreviewContent is FixedPage page)
        {
            var result = _printService.Print(page, Settings.Title);
            StatusText = result ? "打印完成" : "打印已取消";
        }
    }

    [RelayCommand]
    private void ExportXps()
    {
        if (PreviewContent is not FixedPage page) return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出为 XPS",
            Filter = "XPS 文件|*.xps",
            FileName = $"{Settings.Title}.xps"
        };

        if (dialog.ShowDialog() == true)
        {
            var result = _printService.ExportToXps(page, dialog.FileName);
            StatusText = result ? $"已导出到: {dialog.FileName}" : "导出失败";
        }
    }
}
