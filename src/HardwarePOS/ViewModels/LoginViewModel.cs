using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwarePOS.Data;
using HardwarePOS.Helpers;
using HardwarePOS.Models;

namespace HardwarePOS.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly UserRepository _users = new();

    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private bool _showPassword;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public event Action<UserAccount>? LoginSucceeded;

    [RelayCommand]
    private void Login()
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Enter username and password.";
            return;
        }

        try
        {
            IsBusy = true;
            var user = _users.Authenticate(Username.Trim(), Password);
            if (user is null)
            {
                ErrorMessage = "Invalid username or password.";
                return;
            }

            SessionManager.SignIn(user);
            LoginSucceeded?.Invoke(user);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Login failed: {ex.Message}";
            MessageBox.Show(ErrorMessage, "4KV Hardware", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
