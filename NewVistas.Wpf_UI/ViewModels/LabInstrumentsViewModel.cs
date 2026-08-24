// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class LabInstrumentsViewModel : ObservableObject
{
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private ObservableCollection<AutoInstrumentState> _instruments = new();
    [ObservableProperty] private AutoInstrumentState? _selectedInstrument;
    [ObservableProperty] private string _hl7Message = string.Empty;

    public LabInstrumentsViewModel(OrleansGrainService grains)
    {
        _grains = grains;
    }

    private IInstrumentIndexGrain Index() => _grains.GetGrain<IInstrumentIndexGrain>("LA-INST-INDEX");

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        Error = null;
        try
        {
            // There IS a singleton index grain (LA-INST-INDEX) — the old comment was stale.
            // The index carries the roster; each instrument's full config comes from its own
            // grain so the detail panel and the grid show the same object. Instrument counts
            // per site are small, so the per-row fetch is cheap.
            List<InstrumentEntry> entries = await Index().GetAllInstrumentsAsync();
            Instruments.Clear();
            foreach (InstrumentEntry e in entries)
            {
                var grain = _grains.GetGrain<IAutoInstrumentGrain>($"LA7-AI:{e.InstrumentId}");
                Instruments.Add(await grain.GetConfigAsync());
            }
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
            // The index grain owns its own demo seed, so this is a single grain call.
            await Index().SeedDemoInstrumentsAsync();
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}
