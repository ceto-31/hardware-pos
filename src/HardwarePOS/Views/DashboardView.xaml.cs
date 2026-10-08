namespace HardwarePOS.Views;

public partial class DashboardView : System.Windows.Controls.UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    private void OverviewGrid_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
    {
        var stacked = e.NewSize.Width < 980;
        System.Windows.Controls.Grid.SetColumnSpan(ChartCard, stacked ? 2 : 1);
        System.Windows.Controls.Grid.SetColumn(TopProductsCard, stacked ? 0 : 1);
        System.Windows.Controls.Grid.SetRow(TopProductsCard, stacked ? 1 : 0);
        System.Windows.Controls.Grid.SetColumnSpan(TopProductsCard, stacked ? 2 : 1);
    }
}
