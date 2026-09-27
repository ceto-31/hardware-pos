using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using HardwarePOS.Services;

namespace HardwarePOS.Views;

public partial class VoidSaleWindow : Window
{
    private const string OtherOption = "Other";

    private static readonly string[] Reasons =
    [
        "Wrong item",
        "Wrong quantity",
        "Wrong price",
        "Customer changed mind",
        OtherOption
    ];

    public string? Reason { get; private set; }

    public VoidSaleWindow(string invoiceNo)
    {
        InitializeComponent();
        MessageText.Inlines.Add(new Run("Void the entire sale "));
        MessageText.Inlines.Add(new Run(invoiceNo)
        {
            FontWeight = FontWeights.Bold,
            FontFamily = new FontFamily("Consolas")
        });
        MessageText.Inlines.Add(new Run("? Choose a reason before confirming. This cannot be undone."));
        foreach (var reason in Reasons)
            ReasonCombo.Items.Add(reason);
    }

    public static string? Prompt(string invoiceNo)
    {
        var owner = DialogService.GetVisibleOwner();
        var dialog = new VoidSaleWindow(invoiceNo);
        if (owner is not null)
        {
            dialog.Owner = owner;
            dialog.Width = owner.ActualWidth > 0 ? owner.ActualWidth : SystemParameters.PrimaryScreenWidth;
            dialog.Height = owner.ActualHeight > 0 ? owner.ActualHeight : SystemParameters.PrimaryScreenHeight;
            dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        else
        {
            dialog.WindowState = WindowState.Maximized;
        }

        return dialog.ShowDialog() == true ? dialog.Reason : null;
    }

    private void ReasonCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var hasSelection = ReasonCombo.SelectedItem is string;
        ReasonPlaceholder.Visibility = hasSelection ? Visibility.Collapsed : Visibility.Visible;
        OtherPanel.Visibility = ReasonCombo.SelectedItem as string == OtherOption
            ? Visibility.Visible
            : Visibility.Collapsed;
        UpdateConfirm();
    }

    private void OtherText_TextChanged(object sender, TextChangedEventArgs e) => UpdateConfirm();

    private void UpdateConfirm()
    {
        var selected = ReasonCombo.SelectedItem as string;
        if (string.IsNullOrEmpty(selected))
        {
            VoidButton.IsEnabled = false;
            return;
        }

        if (selected == OtherOption)
        {
            VoidButton.IsEnabled = OtherText.Text.Trim().Length > 0;
            return;
        }

        VoidButton.IsEnabled = true;
    }

    private void Void_Click(object sender, RoutedEventArgs e)
    {
        var selected = ReasonCombo.SelectedItem as string;
        if (string.IsNullOrEmpty(selected))
            return;

        string stored;
        if (selected == OtherOption)
        {
            var text = OtherText.Text.Trim();
            if (text.Length == 0)
                return;
            stored = text;
        }
        else
        {
            stored = selected;
        }

        if (stored.Length > 200)
            return;

        Reason = stored;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
