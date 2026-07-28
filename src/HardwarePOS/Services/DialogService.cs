using System.Windows;
using HardwarePOS.Views;

namespace HardwarePOS.Services;

public static class DialogService
{
    public static void ShowInfo(string message, string title = "4KV Hardware")
        => Show(title, message, false, "#2563EB");

    public static void ShowWarning(string message, string title = "4KV Hardware")
        => Show(title, message, false, "#CA8A04");

    public static void ShowError(string message, string title = "4KV Hardware")
        => Show(title, message, false, "#DC2626");

    public static bool Confirm(string message, string title = "Confirm")
        => Show(title, message, true, "#2563EB");

    private static bool Show(string title, string message, bool isConfirm, string accent)
    {
        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                    ?? Application.Current?.MainWindow;

        var dialog = new DialogWindow(title, message, isConfirm, accent);
        if (owner is not null && owner.IsLoaded)
            dialog.Owner = owner;

        // Full-screen dim overlay feel: stretch to owner size when possible
        if (owner is not null)
        {
            dialog.Width = owner.ActualWidth > 0 ? owner.ActualWidth : SystemParameters.PrimaryScreenWidth;
            dialog.Height = owner.ActualHeight > 0 ? owner.ActualHeight : SystemParameters.PrimaryScreenHeight;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dialog.WindowState = WindowState.Maximized;
        }

        return dialog.ShowDialog() == true;
    }
}
