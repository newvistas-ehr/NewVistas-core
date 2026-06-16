// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.ObjectModel;
using System.Net.Http.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class LabInstrumentsViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private ObservableCollection<AutoInstrumentState> _instruments = new();
    [ObservableProperty] private AutoInstrumentState? _selectedInstrument;
    [ObservableProperty] private string _hl7Message = string.Empty;

    public LabInstrumentsViewModel(ApiClient api, OrleansGrainService grains)
    {
        _api = api;
        _grains = grains;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        Error = null;
        try
        {
            // Lab instruments uses the API for listing since there's no singleton index grain
            var list = await _api.Http.GetFromJsonAsync<List<AutoInstrumentState>>(
                "api/labinstruments", ApiClient.Json) ?? [];
            Instruments.Clear();
            foreach (AutoInstrumentState i in list) Instruments.Add(i);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task LoadDetailAsync()
    {
        if (SelectedInstrument == null) return;
        try
        {
            var grain = _grains.GetGrain<IAutoInstrumentGrain>($"LA7-AI:{SelectedInstrument.InstrumentId}");
            SelectedInstrument = await grain.GetConfigAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task LoadDemoAsync()
    {
        try
        {
            await _api.Http.PostAsJsonAsync("api/labinstruments/demo/load", new { });
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}
