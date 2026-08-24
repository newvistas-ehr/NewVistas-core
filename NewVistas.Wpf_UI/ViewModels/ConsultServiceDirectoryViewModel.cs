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

public partial class ConsultServiceDirectoryViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<ConsultServiceEntry> _services = new();
    [ObservableProperty] private ObservableCollection<ConsultServiceEntry> _searchResults = new();
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private string? _actionMessage;

    public ConsultServiceDirectoryViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var dir = Grains.GetGrain<IConsultServiceDirectoryGrain>("CONSULT-SVC-DIR");
        List<ConsultServiceEntry> list = await dir.GetAllServicesAsync();
        Services.Clear();
        foreach (var s in list) Services.Add(s);
    }

    [RelayCommand]
    private async Task SearchServices()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery)) return;
        IsLoading = true; Error = null;
        try
        {
            var dir = Grains.GetGrain<IConsultServiceDirectoryGrain>("CONSULT-SVC-DIR");
            List<ConsultServiceEntry> results = await dir.SearchServicesAsync(SearchQuery.Trim(), 50);
            SearchResults.Clear();
            foreach (var s in results) SearchResults.Add(s);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task RemoveService(ConsultServiceEntry entry)
    {
        IsLoading = true; Error = null;
        try
        {
            var dir = Grains.GetGrain<IConsultServiceDirectoryGrain>("CONSULT-SVC-DIR");
            await dir.RemoveServiceAsync(entry.ServiceId);
            ActionMessage = $"Service {entry.ServiceId} removed.";
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
