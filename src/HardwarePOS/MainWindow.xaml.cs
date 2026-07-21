using System.Windows;
using HardwarePOS.ViewModels;
using HardwarePOS.Views;

namespace HardwarePOS;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        if (DataContext is MainViewModel vm)
        {
            vm.LogoutRequested += OnLogoutRequested;
            vm.Initialize();
        }
    }

    private void OnLogoutRequested()
    {
        var login = new LoginWindow();
        login.Show();
        Close();
    }
}
