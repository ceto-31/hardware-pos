using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwarePOS.Helpers;
using HardwarePOS.Services;

namespace HardwarePOS.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty] private object? _currentPage;
    [ObservableProperty] private string _headerTitle = "Dashboard";
    [ObservableProperty] private string _userLabel = string.Empty;
    [ObservableProperty] private bool _isAdmin;
    [ObservableProperty] private string _activeNav = "Dashboard";

    public DashboardViewModel Dashboard { get; } = new();
    public ProductsViewModel Products { get; } = new();
    public SuppliersViewModel Suppliers { get; } = new();
    public InventoryViewModel Inventory { get; } = new();
    public PosViewModel Pos { get; } = new();
    public CategoriesViewModel Categories { get; } = new();
    public UnitsViewModel Units { get; } = new();
    public UsersViewModel Users { get; } = new();
    public ArchivesViewModel Archives { get; } = new();
    public ReportsViewModel Reports { get; } = new();

    public event Action? LogoutRequested;

    public void Initialize()
    {
        IsAdmin = SessionManager.IsAdmin;
        UserLabel = $"{SessionManager.CurrentUser?.FullName} · {SessionManager.CurrentUser?.RoleName}";
        NavigateDashboard();
    }

    [RelayCommand] private void NavigateDashboard() { ActiveNav = "Dashboard"; HeaderTitle = "Dashboard"; Dashboard.Load(); CurrentPage = Dashboard; }
    [RelayCommand] private void NavigatePos() { ActiveNav = "Pos"; HeaderTitle = "Point of Sale"; Pos.Load(); CurrentPage = Pos; }

    [RelayCommand]
    private void NavigateProducts()
    {
        if (!EnsureAdmin()) return;
        ActiveNav = "Products";
        HeaderTitle = "Product Management";
        Products.Load();
        CurrentPage = Products;
    }

    [RelayCommand]
    private void NavigateSuppliers()
    {
        if (!EnsureAdmin()) return;
        ActiveNav = "Suppliers";
        HeaderTitle = "Supplier Management";
        Suppliers.Load();
        CurrentPage = Suppliers;
    }

    [RelayCommand]
    private void NavigateInventory()
    {
        if (!EnsureAdmin()) return;
        ActiveNav = "Inventory";
        HeaderTitle = "Inventory & Stocks";
        Inventory.Load();
        CurrentPage = Inventory;
    }

    [RelayCommand]
    private void NavigateCategories()
    {
        if (!EnsureAdmin()) return;
        ActiveNav = "Categories";
        HeaderTitle = "Category Master";
        Categories.Load();
        CurrentPage = Categories;
    }

    [RelayCommand]
    private void NavigateUnits()
    {
        if (!EnsureAdmin()) return;
        ActiveNav = "Units";
        HeaderTitle = "Unit Master";
        Units.Load();
        CurrentPage = Units;
    }

    [RelayCommand]
    private void NavigateUsers()
    {
        if (!EnsureAdmin()) return;
        ActiveNav = "Users";
        HeaderTitle = "User Master";
        Users.Load();
        CurrentPage = Users;
    }

    [RelayCommand]
    private void NavigateArchives()
    {
        if (!EnsureAdmin()) return;
        ActiveNav = "Archives";
        HeaderTitle = "Archives";
        Archives.Load();
        CurrentPage = Archives;
    }

    [RelayCommand]
    private void NavigateReports()
    {
        if (!EnsureAdmin()) return;
        ActiveNav = "Reports";
        HeaderTitle = "Reports";
        Reports.Load();
        CurrentPage = Reports;
    }

    [RelayCommand]
    private void Logout()
    {
        if (!DialogService.Confirm("Log out of 4KV Hardware?", "Logout")) return;
        SessionManager.SignOut();
        LogoutRequested?.Invoke();
    }

    private bool EnsureAdmin()
    {
        if (SessionManager.IsAdmin) return true;
        DialogService.ShowWarning("Admin access required.", "4KV Hardware");
        return false;
    }
}
