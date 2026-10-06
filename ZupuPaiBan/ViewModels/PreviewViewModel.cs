using System.Collections.ObjectModel;
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
    private List<FixedPage> _pages = new();

    [ObservableProperty] private LayoutSettings _settings = new();
    [ObservableProperty] private FrameworkElement? _previewContent;
    [ObservableProperty] private double _zoomLevel = 1.0;
    [ObservableProperty] private string _statusText = "预览就绪";
    [ObservableProperty] private int _currentPage = 1;
    [ObservableProperty] private int _totalPages = 1;

    public PreviewViewModel(DataService dataService, PrintService printService)
    {
        _dataService = dataService;
        _printService = printService;

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
            _pages = _printService.CreatePrintPages(layoutResult, Settings);

            TotalPages = _pages.Count;
            CurrentPage = 1;
            ShowPage(1);

            StatusText = $"预览: {allMembers.Count} 位成员, {TotalPages} 页, {Settings.PageSize} {GetOrientationText()}";
        }
        catch (Exception ex)
        {
            StatusText = $"预览失败: {ex.Message}";
        }
    }

    private void ShowPage(int pageNumber)
    {
        if (pageNumber >= 1 && pageNumber <= _pages.Count)
        {
            PreviewContent = _pages[pageNumber - 1];
            CurrentPage = pageNumber;
        }
    }

    [RelayCommand]
    private void FirstPage()
    {
        ShowPage(1);
    }

    [RelayCommand]
    private void PreviousPage()
    {
        if (CurrentPage > 1)
            ShowPage(CurrentPage - 1);
    }

    [RelayCommand]
    private void NextPage()
    {
        if (CurrentPage < TotalPages)
            ShowPage(CurrentPage + 1);
    }

    [RelayCommand]
    private void LastPage()
    {
        ShowPage(TotalPages);
    }

    private void RefreshPreview()
    {
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
        if (_pages.Count > 0)
        {
            var result = _printService.Print(_pages, Settings.Title);
            StatusText = result ? "打印完成" : "打印已取消";
        }
    }

    [RelayCommand]
    private void ExportXps()
    {
        if (_pages.Count == 0) return;

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出为 XPS",
            Filter = "XPS 文件|*.xps",
            FileName = $"{Settings.Title}.xps"
        };

        if (dialog.ShowDialog() == true)
        {
            var result = _printService.ExportToXps(_pages, dialog.FileName);
            StatusText = result ? $"已导出到: {dialog.FileName}" : "导出失败";
        }
    }
}
