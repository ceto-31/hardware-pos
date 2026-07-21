using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HardwarePOS.Data;
using HardwarePOS.Models;

namespace HardwarePOS.ViewModels;

public partial class SuppliersViewModel : ObservableObject
{
    private readonly SupplierRepository _suppliers = new();

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

    [RelayCommand]
    public void Load() => Search();

    [RelayCommand]
    private void Search()
    {
        Items = new ObservableCollection<Supplier>(_suppliers.GetAll(SearchText));
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
            MessageBox.Show("Company name is required.", "Suppliers", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                _suppliers.Insert(supplier);
            else
                _suppliers.Update(supplier);

            ClearForm();
            Search();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Suppliers", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void Delete()
    {
        if (SelectedItem is null) return;
        if (MessageBox.Show($"Delete '{SelectedItem.CompanyName}'?", "Suppliers",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

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
