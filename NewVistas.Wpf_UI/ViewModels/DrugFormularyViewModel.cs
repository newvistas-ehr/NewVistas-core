// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Wpf_UI.Services;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class DrugFormularyViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private string _searchTerm = string.Empty;
    [ObservableProperty] private ObservableCollection<VaProductIndexEntry> _results = new();
    [ObservableProperty] private VaProductState? _selectedProduct;
    [ObservableProperty] private bool _formularyOnly;
    [ObservableProperty] private bool _activeOnly = true;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;

    public DrugFormularyViewModel(ApiClient api, OrleansGrainService grains)
    {
        _api = api;
        _grains = grains;
    }

    [RelayCommand]
    private async Task Search()
    {
        if (string.IsNullOrWhiteSpace(SearchTerm)) return;
        IsLoading = true; Error = null;
        try
        {
            var indexGrain = _grains.GetGrain<IVaProductIndexGrain>("NDF-PRODUCT-INDEX");
            List<VaProductIndexEntry> resultList = await indexGrain.SearchAsync(
                SearchTerm.Trim(), FormularyOnly, null, ActiveOnly, 50);
            Results.Clear();
            foreach (VaProductIndexEntry r in resultList) Results.Add(r);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task SelectProduct(VaProductIndexEntry entry)
    {
        try
        {
            var productGrain = _grains.GetGrain<IVaProductGrain>(entry.Ien);
            SelectedProduct = await productGrain.GetProductAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}
