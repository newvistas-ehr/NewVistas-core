// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using System.Net.Http.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class IncompleteRecordsViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string _providerId = string.Empty;
    [ObservableProperty] private string _statusFilter = string.Empty;
    [ObservableProperty] private ObservableCollection<IncompleteRecordEntry> _deficiencies = new();
    [ObservableProperty] private IncompleteRecordEntry? _selectedDeficiency;

    public string[] StatusOptions { get; } = [string.Empty, "OPEN", "DELINQUENT"];

    public IncompleteRecordsViewModel(ApiClient api, OrleansGrainService grains)
    {
        _api = api;
        _grains = grains;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(ProviderId)) { Error = "Enter a Provider ID"; return; }
        IsLoading = true;
        Error = null;
        try
        {
            var indexGrain = _grains.GetGrain<IIncompleteRecordIndexGrain>($"DGPT-INDEX:{ProviderId.Trim()}");
            List<IncompleteRecordEntry> list = StatusFilter switch
            {
                "DELINQUENT" => await indexGrain.GetDelinquentDeficienciesAsync(),
                "OPEN" => await indexGrain.GetOpenDeficienciesAsync(),
                _ => await indexGrain.GetAllDeficienciesAsync()
            };
            Deficiencies.Clear();
            foreach (IncompleteRecordEntry d in list) Deficiencies.Add(d);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task CompleteAsync()
    {
        if (SelectedDeficiency == null) return;
        try
        {
            var grain = _grains.GetGrain<IIncompleteRecordGrain>($"DGPT:{SelectedDeficiency.DeficiencyId}");
            await grain.CompleteAsync("ADMIN");
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task WaiveAsync()
    {
        if (SelectedDeficiency == null) return;
        try
        {
            var grain = _grains.GetGrain<IIncompleteRecordGrain>($"DGPT:{SelectedDeficiency.DeficiencyId}");
            await grain.WaiveAsync("Waived via admin UI", "ADMIN");
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task LoadDemoAsync()
    {
        try
        {
            await _api.Http.PostAsJsonAsync("api/incomplete-records/demo/load", new { });
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}
