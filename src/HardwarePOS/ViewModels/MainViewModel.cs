using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwarePOS.Helpers;

namespace HardwarePOS.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty] private object? _currentPage;
    [ObservableProperty] private string _headerTitle = "Dashboard";
    [ObservableProperty] private string _userLabel = string.Empty;
    [ObservableProperty] private bool _isAdmin;

    public DashboardViewModel Dashboard { get; } = new();
    public ProductsViewModel Products { get; } = new();
    public SuppliersViewModel Suppliers { get; } = new();
    public InventoryViewModel Inventory { get; } = new();
    public PosViewModel Pos { get; } = new();

    public event Action? LogoutRequested;

    public void Initialize()
    {
        IsAdmin = SessionManager.IsAdmin;
        UserLabel = $"{SessionManager.CurrentUser?.FullName} • {SessionManager.CurrentUser?.RoleName}";
        NavigateDashboard();
    }

    [RelayCommand]
    private void NavigateDashboard()
    {
        HeaderTitle = "Dashboard";
        Dashboard.Load();
        CurrentPage = Dashboard;
    }

    [RelayCommand]
    private void NavigateProducts()
    {
        HeaderTitle = "Product Management";
        Products.Load();
        CurrentPage = Products;
    }

    [RelayCommand]
    private void NavigateSuppliers()
    {
        HeaderTitle = "Supplier Management";
        Suppliers.Load();
        CurrentPage = Suppliers;
    }

    [RelayCommand]
    private void NavigateInventory()
    {
        HeaderTitle = "Inventory & Stocks";
        Inventory.Load();
        CurrentPage = Inventory;
    }

    [RelayCommand]
    private void NavigatePos()
    {
        HeaderTitle = "Point of Sale";
        Pos.Load();
        CurrentPage = Pos;
    }

    [RelayCommand]
    private void Logout()
    {
        if (MessageBox.Show("Log out of HARDWARE?", "Logout",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        SessionManager.SignOut();
        LogoutRequested?.Invoke();
    }
}
