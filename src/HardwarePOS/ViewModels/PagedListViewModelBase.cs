using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HardwarePOS.ViewModels;

public abstract partial class PagedListViewModelBase<T> : ObservableObject
{
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _sortColumn = string.Empty;
    [ObservableProperty] private bool _sortAscending = true;
    [ObservableProperty] private int _pageIndex = 1;
    [ObservableProperty] private int _pageSize = 15;
    [ObservableProperty] private int _totalCount;
    [ObservableProperty] private ObservableCollection<T> _pageItems = new();
    [ObservableProperty] private string _emptyMessage = "No records found.";
    [ObservableProperty] private bool _hasRows;

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
    public string PageInfo => $"Page {PageIndex} of {TotalPages} ({TotalCount} items)";

    protected List<T> AllItems { get; set; } = new();

    protected abstract IEnumerable<T> ApplySearch(IEnumerable<T> source, string search);
    protected abstract IEnumerable<T> ApplySort(IEnumerable<T> source, string column, bool ascending);

    protected void RefreshPage()
    {
        IEnumerable<T> query = AllItems;
        query = ApplySearch(query, SearchText);
        query = ApplySort(query, SortColumn, SortAscending);
        var filtered = query.ToList();
        TotalCount = filtered.Count;
        if (PageIndex > TotalPages) PageIndex = TotalPages;
        if (PageIndex < 1) PageIndex = 1;
        PageItems = new ObservableCollection<T>(filtered.Skip((PageIndex - 1) * PageSize).Take(PageSize));
        HasRows = PageItems.Count > 0;
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(PageInfo));
    }

    partial void OnSearchTextChanged(string value)
    {
        PageIndex = 1;
        RefreshPage();
    }

    [RelayCommand]
    protected void NextPage()
    {
        if (PageIndex < TotalPages)
        {
            PageIndex++;
            RefreshPage();
        }
    }

    [RelayCommand]
    protected void PrevPage()
    {
        if (PageIndex > 1)
        {
            PageIndex--;
            RefreshPage();
        }
    }

    [RelayCommand]
    protected void SortBy(string? column)
    {
        if (string.IsNullOrWhiteSpace(column)) return;
        if (string.Equals(SortColumn, column, StringComparison.OrdinalIgnoreCase))
            SortAscending = !SortAscending;
        else
        {
            SortColumn = column;
            SortAscending = true;
        }
        RefreshPage();
    }
}
