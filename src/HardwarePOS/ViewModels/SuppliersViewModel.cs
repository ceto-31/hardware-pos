using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwarePOS.Data;
using HardwarePOS.Models;
using HardwarePOS.Services;

namespace HardwarePOS.ViewModels;

public partial class SuppliersViewModel : ObservableObject
{
    private readonly SupplierRepository _suppliers = new();
    private readonly ActivityRepository _activity = new();

    [ObservableProperty] private ObservableCollection<Supplier> _items = new();
    [ObservableProperty] private Supplier? _selectedItem;
    [ObservableProperty] private string _searchText = string.Empty;

    [ObservableProperty] private int _editingId;
    [ObservableProperty] private string _companyName = string.Empty;
    [ObservableProperty] private string _contactPerson = string.Empty;
    [ObservableProperty] private string _phone = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _address = string.Empty;
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private string _formTitle = "Add Supplier";
    [ObservableProperty] private bool _hasRows;

    [RelayCommand]
    public void Load() => Search();

    partial void OnSearchTextChanged(string value) => Search();

    [RelayCommand]
    private void Search()
    {
        Items = new ObservableCollection<Supplier>(_suppliers.GetAll(SearchText));
        HasRows = Items.Count > 0;
    }

    [RelayCommand]
    private void New()
    {
        ClearForm();
        FormTitle = "Add Supplier";
    }

    [RelayCommand]
    private void Edit()
    {
        if (SelectedItem is null) return;
        EditingId = SelectedItem.SupplierId;
        CompanyName = SelectedItem.CompanyName;
        ContactPerson = SelectedItem.ContactPerson ?? string.Empty;
        Phone = SelectedItem.Phone ?? string.Empty;
        Email = SelectedItem.Email ?? string.Empty;
        Address = SelectedItem.Address ?? string.Empty;
        IsActive = SelectedItem.IsActive;
        FormTitle = "Edit Supplier";
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(CompanyName))
        {
            DialogService.ShowWarning("Company name is required.", "Suppliers");
            return;
        }

        try
        {
            var supplier = new Supplier
            {
                SupplierId = EditingId,
                CompanyName = CompanyName.Trim(),
                ContactPerson = NullIfEmpty(ContactPerson),
                Phone = NullIfEmpty(Phone),
                Email = NullIfEmpty(Email),
                Address = NullIfEmpty(Address),
                IsActive = IsActive
            };

            if (EditingId == 0)
            {
                _suppliers.Insert(supplier);
                _activity.Log("Supplier", $"Added supplier '{supplier.CompanyName}'");
            }
            else
            {
                _suppliers.Update(supplier);
                _activity.Log("Supplier", $"Updated supplier '{supplier.CompanyName}'");
            }

            ClearForm();
            Search();
        }
        catch (Exception ex)
        {
            DialogService.ShowError(ex.Message, "Suppliers");
        }
    }

    [RelayCommand]
    private void Archive()
    {
        if (SelectedItem is null) return;
        if (!DialogService.Confirm($"Archive '{SelectedItem.CompanyName}'?", "Suppliers")) return;
        _suppliers.Archive(SelectedItem.SupplierId, true);
        _activity.Log("Supplier", $"Archived supplier '{SelectedItem.CompanyName}'");
        Search();
    }

    [RelayCommand]
    private void Delete()
    {
        if (SelectedItem is null) return;
        if (!DialogService.Confirm($"Delete '{SelectedItem.CompanyName}'?", "Suppliers")) return;

        _suppliers.Delete(SelectedItem.SupplierId);
        Search();
    }

    private void ClearForm()
    {
        EditingId = 0;
        CompanyName = string.Empty;
        ContactPerson = string.Empty;
        Phone = string.Empty;
        Email = string.Empty;
        Address = string.Empty;
        IsActive = true;
        FormTitle = "Add Supplier";
    }

    private static string? NullIfEmpty(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
