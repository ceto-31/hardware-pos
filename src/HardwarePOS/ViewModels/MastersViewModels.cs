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
    [ObservableProperty] private bool _isFormOpen;

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
        IsFormOpen = true;
    }

    [RelayCommand]
    private void CloseForm()
    {
        EditingId = 0;
        CategoryName = string.Empty;
        FormTitle = "Add Category";
        IsFormOpen = false;
    }

    [RelayCommand]
    private void Edit(Category? category)
    {
        var item = category ?? SelectedItem;
        if (item is null) return;
        SelectedItem = item;
        EditingId = item.CategoryId;
        CategoryName = item.CategoryName;
        FormTitle = "Edit Category";
        IsFormOpen = true;
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
            CloseForm();
            Load();
        }
        catch (Exception ex)
        {
            DialogService.ShowError(ex.Message, "Categories");
        }
    }

    [RelayCommand]
    private void Delete(Category? category)
    {
        var item = category ?? SelectedItem;
        if (item is null) return;
        if (!DialogService.Confirm($"Delete '{item.CategoryName}'?", "Categories")) return;
        try
        {
            _repo.Delete(item.CategoryId);
            _activity.Log("Category", $"Deleted category '{item.CategoryName}'");
            if (EditingId == item.CategoryId) CloseForm();
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
    [ObservableProperty] private bool _isFormOpen;

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
        IsFormOpen = true;
    }

    [RelayCommand]
    private void CloseForm()
    {
        EditingId = 0;
        UnitName = string.Empty;
        IsActive = true;
        FormTitle = "Add Unit";
        IsFormOpen = false;
    }

    [RelayCommand]
    private void Edit(UnitOfMeasureItem? unit)
    {
        var item = unit ?? SelectedItem;
        if (item is null) return;
        SelectedItem = item;
        EditingId = item.UnitId;
        UnitName = item.UnitName;
        IsActive = item.IsActive;
        FormTitle = "Edit Unit";
        IsFormOpen = true;
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
            CloseForm();
            Load();
        }
        catch (Exception ex)
        {
            DialogService.ShowError(ex.Message, "Units");
        }
    }

    [RelayCommand]
    private void Delete(UnitOfMeasureItem? unit)
    {
        var item = unit ?? SelectedItem;
        if (item is null) return;
        if (!DialogService.Confirm($"Delete '{item.UnitName}'?", "Units")) return;
        try
        {
            _repo.Delete(item.UnitId);
            _activity.Log("Unit", $"Deleted unit '{item.UnitName}'");
            if (EditingId == item.UnitId) CloseForm();
            Load();
        }
        catch (Exception ex)
        {
            DialogService.ShowError(ex.Message, "Units");
        }
    }
}
