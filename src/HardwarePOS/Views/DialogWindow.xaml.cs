using System.Windows;
using System.Windows.Media;

namespace HardwarePOS.Views;

public partial class DialogWindow : Window
{
    public bool Confirmed { get; private set; }

    public DialogWindow(string title, string message, bool isConfirm, string accentColor)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;
        NoButton.Visibility = isConfirm ? Visibility.Visible : Visibility.Collapsed;
        OkButton.Content = isConfirm ? "Yes" : "OK";

        try
        {
            AccentBar.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(accentColor));
        }
        catch
        {
            AccentBar.Background = new SolidColorBrush(Color.FromRgb(37, 99, 235));
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = true;
        DialogResult = true;
        Close();
    }

    private void No_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = false;
        DialogResult = false;
        Close();
    }
}
