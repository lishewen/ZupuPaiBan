using CommunityToolkit.Mvvm.ComponentModel;

namespace ZupuPaiBan.Models;

public enum PageSizeOption
{
    A2,
    A3,
    A4
}

public enum PageOrientation
{
    Portrait,
    Landscape
}

public partial class LayoutSettings : ObservableObject
{
    [ObservableProperty] private PageSizeOption _pageSize = PageSizeOption.A3;
    [ObservableProperty] private PageOrientation _orientation = PageOrientation.Landscape;
    [ObservableProperty] private double _nodeWidth = 140;
    [ObservableProperty] private double _nodeHeight = 100;
    [ObservableProperty] private double _horizontalSpacing = 30;
    [ObservableProperty] private double _verticalSpacing = 80;
    [ObservableProperty] private double _fontSize = 12;
    [ObservableProperty] private bool _showPhoto = true;
    [ObservableProperty] private bool _showSpouse = true;
    [ObservableProperty] private bool _showBiography;
    [ObservableProperty] private string _title = "族谱";
    [ObservableProperty] private double _titleFontSize = 28;

    /// <summary>
    /// 获取页面宽度（像素，96dpi）
    /// </summary>
    public double PageWidth => PageSize switch
    {
        PageSizeOption.A2 => Orientation == PageOrientation.Landscape ? 1587 : 1123,
        PageSizeOption.A3 => Orientation == PageOrientation.Landscape ? 1123 : 794,
        PageSizeOption.A4 => Orientation == PageOrientation.Landscape ? 794 : 559,
        _ => 1123
    };

    /// <summary>
    /// 获取页面高度（像素，96dpi）
    /// </summary>
    public double PageHeight => PageSize switch
    {
        PageSizeOption.A2 => Orientation == PageOrientation.Landscape ? 1123 : 1587,
        PageSizeOption.A3 => Orientation == PageOrientation.Landscape ? 794 : 1123,
        PageSizeOption.A4 => Orientation == PageOrientation.Landscape ? 559 : 794,
        _ => 794
    };
}
