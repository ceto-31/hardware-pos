using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwarePOS.Data;
using HardwarePOS.Helpers;
using HardwarePOS.Models;
using HardwarePOS.Services;

namespace HardwarePOS.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly UserRepository _users = new();

    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private bool _showPassword;
    [ObservableProperty] private string _showPasswordLabel = "Show Password";
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _isBusy;

    // Forgot password wizard
    [ObservableProperty] private bool _isForgotMode;
    [ObservableProperty] private int _forgotStep = 1; // 1 username, 2 questions, 3 new password
    [ObservableProperty] private string _forgotUsername = string.Empty;
    [ObservableProperty] private string _securityColor = string.Empty;
    [ObservableProperty] private string _securityNumber = string.Empty;
    [ObservableProperty] private string _securityHobby = string.Empty;
    [ObservableProperty] private string _newPassword = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;
    [ObservableProperty] private string _forgotError = string.Empty;

    public bool IsForgotStep1 => ForgotStep == 1;
    public bool IsForgotStep2 => ForgotStep == 2;
    public bool IsForgotStep3 => ForgotStep == 3;

    partial void OnForgotStepChanged(int value)
    {
        OnPropertyChanged(nameof(IsForgotStep1));
        OnPropertyChanged(nameof(IsForgotStep2));
        OnPropertyChanged(nameof(IsForgotStep3));
    }

    public event Action<UserAccount>? LoginSucceeded;

    partial void OnShowPasswordChanged(bool value) =>
        ShowPasswordLabel = value ? "Hide Password" : "Show Password";

    [RelayCommand]
    private void ToggleShowPassword() => ShowPassword = !ShowPassword;

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
            DialogService.ShowError(ErrorMessage, "4KV Hardware");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenForgot()
    {
        IsForgotMode = true;
        ForgotStep = 1;
        ForgotError = string.Empty;
        ForgotUsername = Username;
        SecurityColor = SecurityNumber = SecurityHobby = NewPassword = ConfirmPassword = string.Empty;
    }

    [RelayCommand]
    private void CancelForgot()
    {
        IsForgotMode = false;
        ForgotError = string.Empty;
    }

    [RelayCommand]
    private void ForgotNext()
    {
        ForgotError = string.Empty;
        if (ForgotStep == 1)
        {
            if (string.IsNullOrWhiteSpace(ForgotUsername))
            {
                ForgotError = "Enter your username.";
                return;
            }
            var user = _users.GetByUsername(ForgotUsername.Trim());
            if (user is null || !user.IsActive)
            {
                ForgotError = "Username not found or inactive.";
                return;
            }
            ForgotStep = 2;
            return;
        }

        if (ForgotStep == 2)
        {
            var (colorOk, numberOk, hobbyOk) = _users.VerifySecurityAnswers(
                ForgotUsername.Trim(), SecurityColor, SecurityNumber, SecurityHobby);

            if (!colorOk || !numberOk || !hobbyOk)
            {
                ForgotError = "One or more answers are incorrect.";
                DialogService.ShowWarning(ForgotError, "Security Questions");
                return;
            }

            ForgotStep = 3;
            return;
        }

        if (ForgotStep == 3)
        {
            if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 6)
            {
                ForgotError = "Password must be at least 6 characters.";
                return;
            }
            if (NewPassword != ConfirmPassword)
            {
                ForgotError = "Passwords do not match.";
                return;
            }

            _users.ResetPassword(ForgotUsername.Trim(), NewPassword);
            DialogService.ShowInfo("Password updated. You can log in now.", "4KV Hardware");
            Username = ForgotUsername.Trim();
            Password = string.Empty;
            IsForgotMode = false;
        }
    }
}
