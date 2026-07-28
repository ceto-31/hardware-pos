using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwarePOS.Data;
using HardwarePOS.Models;
using HardwarePOS.Services;

namespace HardwarePOS.ViewModels;

public partial class UsersViewModel : ObservableObject
{
    private readonly UserRepository _repo = new();
    private readonly ActivityRepository _activity = new();

    [ObservableProperty] private ObservableCollection<UserAccount> _items = new();
    [ObservableProperty] private ObservableCollection<RoleOption> _roles = new();
    [ObservableProperty] private UserAccount? _selectedItem;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private int _editingId;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _fullName = string.Empty;
    [ObservableProperty] private int _roleId;
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _securityColor = string.Empty;
    [ObservableProperty] private string _securityNumber = string.Empty;
    [ObservableProperty] private string _securityHobby = string.Empty;
    [ObservableProperty] private string _formTitle = "Add User";
    [ObservableProperty] private bool _hasRows;
    [ObservableProperty] private bool _isNewUser = true;

    [RelayCommand]
    public void Load()
    {
        Roles = new ObservableCollection<RoleOption>(_repo.GetRoles().Select(r => new RoleOption(r.RoleId, r.RoleName)));
        Items = new ObservableCollection<UserAccount>(_repo.GetAll(SearchText));
        HasRows = Items.Count > 0;
        if (RoleId == 0 && Roles.Count > 0) RoleId = Roles[0].RoleId;
    }

    partial void OnSearchTextChanged(string value)
    {
        Items = new ObservableCollection<UserAccount>(_repo.GetAll(SearchText));
        HasRows = Items.Count > 0;
    }

    [RelayCommand]
    private void Search() => Load();

    [RelayCommand]
    private void New()
    {
        EditingId = 0;
        IsNewUser = true;
        Username = FullName = Password = SecurityColor = SecurityNumber = SecurityHobby = string.Empty;
        IsActive = true;
        if (Roles.Count > 0) RoleId = Roles[0].RoleId;
        FormTitle = "Add User";
    }

    [RelayCommand]
    private void Edit()
    {
        if (SelectedItem is null) return;
        if (SelectedItem.IsProtected)
        {
            DialogService.ShowWarning("The built-in admin account is protected and cannot be edited.", "Users");
            return;
        }
        EditingId = SelectedItem.UserId;
        IsNewUser = false;
        Username = SelectedItem.Username;
        FullName = SelectedItem.FullName;
        RoleId = SelectedItem.RoleId;
        IsActive = SelectedItem.IsActive;
        Password = SecurityColor = SecurityNumber = SecurityHobby = string.Empty;
        FormTitle = "Edit User";
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(FullName) || (IsNewUser && string.IsNullOrWhiteSpace(Username)))
        {
            DialogService.ShowWarning("Username and full name are required.", "Users");
            return;
        }
        if (!IsNewUser && string.Equals(Username, "admin", StringComparison.OrdinalIgnoreCase))
        {
            DialogService.ShowWarning("The built-in admin account is protected and cannot be edited.", "Users");
            return;
        }
        try
        {
            if (IsNewUser)
            {
                if (string.IsNullOrWhiteSpace(Password) || string.IsNullOrWhiteSpace(SecurityColor)
                    || string.IsNullOrWhiteSpace(SecurityNumber) || string.IsNullOrWhiteSpace(SecurityHobby))
                {
                    DialogService.ShowWarning("Password and all security answers are required for new users.", "Users");
                    return;
                }
                _repo.Insert(Username, FullName, RoleId, Password, SecurityColor, SecurityNumber, SecurityHobby);
                _activity.Log("User", $"Added user '{Username.Trim()}'");
            }
            else
            {
                _repo.Update(EditingId, FullName, RoleId, IsActive,
                    string.IsNullOrWhiteSpace(Password) ? null : Password,
                    string.IsNullOrWhiteSpace(SecurityColor) ? null : SecurityColor,
                    string.IsNullOrWhiteSpace(SecurityNumber) ? null : SecurityNumber,
                    string.IsNullOrWhiteSpace(SecurityHobby) ? null : SecurityHobby);
                _activity.Log("User", $"Updated user '{Username}'");
            }
            New();
            Load();
        }
        catch (Exception ex)
        {
            DialogService.ShowError(ex.Message, "Users");
        }
    }

    [RelayCommand]
    private void Deactivate()
    {
        if (SelectedItem is null) return;
        if (SelectedItem.IsProtected)
        {
            DialogService.ShowWarning("The built-in admin account is protected and cannot be deactivated.", "Users");
            return;
        }
        if (!DialogService.Confirm($"Deactivate '{SelectedItem.Username}'?", "Users")) return;
        try
        {
            _repo.Deactivate(SelectedItem.UserId);
            _activity.Log("User", $"Deactivated user '{SelectedItem.Username}'");
            Load();
        }
        catch (Exception ex)
        {
            DialogService.ShowError(ex.Message, "Users");
        }
    }
}

public record RoleOption(int RoleId, string RoleName);
