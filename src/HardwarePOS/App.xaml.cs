using System.Windows;
using HardwarePOS.Data;
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
        try
        {
            DatabaseSchemaUpdater.EnsureDiscountsSchema();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Database setup failed: {ex.Message}\n\nEnsure SQL Server is running and run Database/05_UpgradeSchema.sql if needed.",
                "4KV Hardware",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        base.OnStartup(e);
    }
}
