using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HardwarePOS.Helpers;

public interface IPagedList
{
    void Sort(string property, ListSortDirection direction);
}

public sealed class PagedList<T> : ObservableObject, IPagedList
{
    private List<T> _source = [];
    private string? _sortProperty;
    private ListSortDirection _sortDirection;
    private int _page = 1;
    private int _pageSize = 10;
    private ObservableCollection<T> _items = [];
    public ObservableCollection<T> Items { get => _items; private set => SetProperty(ref _items, value); }
    public int[] PageSizes { get; } = [10, 25, 50];
    public int Page => _page;
    public int TotalCount => _source.Count;
    public int TotalPages => Math.Max(1, (TotalCount + PageSize - 1) / PageSize);
    public string PageLabel => $"Page {Page} of {TotalPages}";
    public string RangeLabel => TotalCount == 0 ? "No records" : $"{(Page - 1) * PageSize + 1:N0}–{Math.Min(Page * PageSize, TotalCount):N0} of {TotalCount:N0} records";
    public bool IsEmpty => TotalCount == 0;
    public int PageSize
    {
        get => _pageSize;
        set { if (PageSizes.Contains(value) && SetProperty(ref _pageSize, value)) { _page = 1; Refresh(); } }
    }
    public RelayCommand FirstCommand { get; }
    public RelayCommand PreviousCommand { get; }
    public RelayCommand NextCommand { get; }
    public RelayCommand LastCommand { get; }
    public event Action? PageChanged;

    public PagedList()
    {
        FirstCommand = new(() => Move(1), () => Page > 1);
        PreviousCommand = new(() => Move(Page - 1), () => Page > 1);
        NextCommand = new(() => Move(Page + 1), () => Page < TotalPages);
        LastCommand = new(() => Move(TotalPages), () => Page < TotalPages);
    }
    public void SetSource(IEnumerable<T> items, bool resetPage = true)
    {
        _source = items.ToList();
        if (resetPage) _page = 1;
        Refresh();
    }
    public void Sort(string property, ListSortDirection direction)
    {
        _sortProperty = property; _sortDirection = direction; _page = 1; Refresh();
    }
    private void Move(int page) { _page = Math.Clamp(page, 1, TotalPages); Refresh(); }
    private void Refresh()
    {
        _page = Math.Clamp(_page, 1, TotalPages);
        IEnumerable<T> ordered = _source;
        if (!string.IsNullOrEmpty(_sortProperty))
        {
            var view = new ListCollectionView(_source);
            view.SortDescriptions.Add(new SortDescription(_sortProperty, _sortDirection));
            ordered = view.Cast<T>();
        }
        Items = new(ordered.Skip((Page - 1) * PageSize).Take(PageSize));
        foreach (var name in new[] { nameof(Page), nameof(TotalCount), nameof(TotalPages), nameof(PageLabel), nameof(RangeLabel), nameof(IsEmpty) }) OnPropertyChanged(name);
        FirstCommand.NotifyCanExecuteChanged(); PreviousCommand.NotifyCanExecuteChanged(); NextCommand.NotifyCanExecuteChanged(); LastCommand.NotifyCanExecuteChanged();
        PageChanged?.Invoke();
    }
}
