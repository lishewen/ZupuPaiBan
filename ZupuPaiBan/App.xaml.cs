using System.Windows;
using SQLitePCL;

namespace ZupuPaiBan;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // 初始化 SQLite native 库
        Batteries.Init();
    }
}
