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
    [ObservableProperty] private bool _isFormOpen;

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
        IsFormOpen = true;
    }

    [RelayCommand]
    private void CloseForm()
    {
        ClearForm();
        IsFormOpen = false;
    }

    [RelayCommand]
    private void Edit(Supplier? supplier)
    {
        var item = supplier ?? SelectedItem;
        if (item is null) return;
        SelectedItem = item;
        EditingId = item.SupplierId;
        CompanyName = item.CompanyName;
        ContactPerson = item.ContactPerson ?? string.Empty;
        Phone = item.Phone ?? string.Empty;
        Email = item.Email ?? string.Empty;
        Address = item.Address ?? string.Empty;
        IsActive = item.IsActive;
        FormTitle = "Edit Supplier";
        IsFormOpen = true;
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
            IsFormOpen = false;
            Search();
        }
        catch (Exception ex)
        {
            DialogService.ShowError(ex.Message, "Suppliers");
        }
    }

    [RelayCommand]
    private void Archive(Supplier? supplier)
    {
        var item = supplier ?? SelectedItem;
        if (item is null) return;
        if (!DialogService.Confirm($"Archive '{item.CompanyName}'?", "Suppliers")) return;
        _suppliers.Archive(item.SupplierId, true);
        _activity.Log("Supplier", $"Archived supplier '{item.CompanyName}'");
        if (EditingId == item.SupplierId) CloseForm();
        Search();
    }

    [RelayCommand]
    private void Delete(Supplier? supplier)
    {
        var item = supplier ?? SelectedItem;
        if (item is null) return;
        if (!DialogService.Confirm($"Delete '{item.CompanyName}'?", "Suppliers")) return;

        _suppliers.Delete(item.SupplierId);
        if (EditingId == item.SupplierId) CloseForm();
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
