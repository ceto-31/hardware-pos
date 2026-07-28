using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HardwarePOS.ViewModels;

namespace HardwarePOS.Views;

public partial class PosView : UserControl
{
    public PosView()
    {
        InitializeComponent();
    }

    private void ProductGrid_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;

        var row = FindAncestor<DataGridRow>(source);
        if (row?.Item is null) return;

        if (DataContext is PosViewModel vm && vm.AddSelectedCommand.CanExecute(null))
            vm.AddSelectedCommand.Execute(null);
    }

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match) return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
