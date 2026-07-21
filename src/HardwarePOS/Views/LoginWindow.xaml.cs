using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using HardwarePOS.Models;
using HardwarePOS.ViewModels;

namespace HardwarePOS.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        if (DataContext is LoginViewModel vm)
        {
            vm.LoginSucceeded += OnLoginSucceeded;
            vm.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm && sender is PasswordBox box && !vm.ShowPassword)
            vm.Password = box.Password;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not LoginViewModel vm) return;
        if (e.PropertyName == nameof(LoginViewModel.ShowPassword) && !vm.ShowPassword)
            PasswordBoxHidden.Password = vm.Password;
    }

    private void OnLoginSucceeded(UserAccount user)
    {
        var main = new MainWindow();
        main.Show();
        Close();
    }
}
