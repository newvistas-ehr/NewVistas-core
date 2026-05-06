// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Wpf_UI.Services;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class Icd10BrowserViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private string _searchTerm = string.Empty;
    [ObservableProperty] private ObservableCollection<Icd10IndexEntry> _results = new();
    [ObservableProperty] private Icd10State? _selectedCode;
    [ObservableProperty] private bool _billableOnly;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;

    public Icd10BrowserViewModel(ApiClient api, OrleansGrainService grains)
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
            var indexGrain = _grains.GetGrain<IIcd10IndexGrain>("ICD10-INDEX");
            List<Icd10IndexEntry> resultList = await indexGrain.SearchAsync(SearchTerm.Trim(), BillableOnly, 50);
            Results.Clear();
            foreach (Icd10IndexEntry r in resultList) Results.Add(r);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task SelectCode(Icd10IndexEntry entry)
    {
        try
        {
            var grain = _grains.GetGrain<IIcd10Grain>(entry.Code);
            SelectedCode = await grain.GetCodeAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}
