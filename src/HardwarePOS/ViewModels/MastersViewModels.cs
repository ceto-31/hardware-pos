using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwarePOS.Data;
using HardwarePOS.Models;
using HardwarePOS.Services;

namespace HardwarePOS.ViewModels;

public partial class CategoriesViewModel : ObservableObject
{
    private readonly CategoryRepository _repo = new();
    private readonly ActivityRepository _activity = new();

    [ObservableProperty] private ObservableCollection<Category> _items = new();
    [ObservableProperty] private Category? _selectedItem;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private int _editingId;
    [ObservableProperty] private string _categoryName = string.Empty;
    [ObservableProperty] private string _formTitle = "Add Category";
    [ObservableProperty] private string _emptyMessage = "No categories found.";
    [ObservableProperty] private bool _hasRows;

    [RelayCommand]
    public void Load()
    {
        Items = new ObservableCollection<Category>(_repo.GetAll(SearchText));
        HasRows = Items.Count > 0;
    }

    partial void OnSearchTextChanged(string value) => Load();

    [RelayCommand]
    private void Search() => Load();

    [RelayCommand]
    private void New()
    {
        EditingId = 0;
        CategoryName = string.Empty;
        FormTitle = "Add Category";
    }

    [RelayCommand]
    private void Edit()
    {
        if (SelectedItem is null) return;
        EditingId = SelectedItem.CategoryId;
        CategoryName = SelectedItem.CategoryName;
        FormTitle = "Edit Category";
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(CategoryName))
        {
            DialogService.ShowWarning("Category name is required.", "Categories");
            return;
        }
        try
        {
            if (EditingId == 0)
            {
                _repo.Insert(CategoryName);
                _activity.Log("Category", $"Added category '{CategoryName.Trim()}'");
            }
            else
            {
                _repo.Update(EditingId, CategoryName);
                _activity.Log("Category", $"Updated category '{CategoryName.Trim()}'");
            }
            New();
            Load();
        }
        catch (Exception ex)
        {
            DialogService.ShowError(ex.Message, "Categories");
        }
    }

    [RelayCommand]
    private void Delete()
    {
        if (SelectedItem is null) return;
        if (!DialogService.Confirm($"Delete '{SelectedItem.CategoryName}'?", "Categories")) return;
        try
        {
            _repo.Delete(SelectedItem.CategoryId);
            _activity.Log("Category", $"Deleted category '{SelectedItem.CategoryName}'");
            Load();
        }
        catch (Exception ex)
        {
            DialogService.ShowError(ex.Message, "Categories");
        }
    }
}

public partial class UnitsViewModel : ObservableObject
{
    private readonly UnitRepository _repo = new();
    private readonly ActivityRepository _activity = new();

    [ObservableProperty] private ObservableCollection<UnitOfMeasureItem> _items = new();
    [ObservableProperty] private UnitOfMeasureItem? _selectedItem;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private int _editingId;
    [ObservableProperty] private string _unitName = string.Empty;
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private string _formTitle = "Add Unit";
    [ObservableProperty] private bool _hasRows;

    [RelayCommand]
    public void Load()
    {
        Items = new ObservableCollection<UnitOfMeasureItem>(_repo.GetAll(SearchText));
        HasRows = Items.Count > 0;
    }

    partial void OnSearchTextChanged(string value) => Load();

    [RelayCommand]
    private void Search() => Load();

    [RelayCommand]
    private void New()
    {
        EditingId = 0;
        UnitName = string.Empty;
        IsActive = true;
        FormTitle = "Add Unit";
    }

    [RelayCommand]
    private void Edit()
    {
        if (SelectedItem is null) return;
        EditingId = SelectedItem.UnitId;
        UnitName = SelectedItem.UnitName;
        IsActive = SelectedItem.IsActive;
        FormTitle = "Edit Unit";
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(UnitName))
        {
            DialogService.ShowWarning("Unit name is required.", "Units");
            return;
        }
        try
        {
            if (EditingId == 0)
            {
                _repo.Insert(UnitName);
                _activity.Log("Unit", $"Added unit '{UnitName.Trim()}'");
            }
            else
            {
                _repo.Update(EditingId, UnitName, IsActive);
                _activity.Log("Unit", $"Updated unit '{UnitName.Trim()}'");
            }
            New();
            Load();
        }
        catch (Exception ex)
        {
            DialogService.ShowError(ex.Message, "Units");
        }
    }

    [RelayCommand]
    private void Delete()
    {
        if (SelectedItem is null) return;
        if (!DialogService.Confirm($"Delete '{SelectedItem.UnitName}'?", "Units")) return;
        try
        {
            _repo.Delete(SelectedItem.UnitId);
            _activity.Log("Unit", $"Deleted unit '{SelectedItem.UnitName}'");
            Load();
        }
        catch (Exception ex)
        {
            DialogService.ShowError(ex.Message, "Units");
        }
    }
}
