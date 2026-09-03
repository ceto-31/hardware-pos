using System.Windows;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using QuestPDF.Infrastructure;
using SkiaSharp;

namespace HardwarePOS;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        LiveCharts.Configure(config => config.AddSkiaSharp());
        LiveChartsSkiaSharp.DefaultSKTypeface = SKTypeface.FromFamilyName("Segoe UI");
        base.OnStartup(e);
    }
}
