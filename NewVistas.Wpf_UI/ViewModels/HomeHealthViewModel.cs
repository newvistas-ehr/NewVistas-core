// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Wpf_UI.Services;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class HomeHealthViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private ObservableCollection<HBPCRegistryEntry> _patients = new();
    [ObservableProperty] private HBPCRegistryEntry? _selectedPatient;
    [ObservableProperty] private ObservableCollection<HHCVisitIndexEntry> _visits = new();
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;

    public HomeHealthViewModel(ApiClient api, OrleansGrainService grains)
    {
        _api = api;
        _grains = grains;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true; Error = null;
        try
        {
            var registryGrain = _grains.GetGrain<IHBPCRegistryGrain>("HBPC-REGISTRY");
            List<HBPCRegistryEntry> list = await registryGrain.GetActivePatientsAsync();
            Patients.Clear();
            foreach (HBPCRegistryEntry p in list) Patients.Add(p);
            Visits.Clear();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task SelectPatient(HBPCRegistryEntry entry)
    {
        SelectedPatient = entry;
        IsLoading = true; Error = null;
        try
        {
            var visitIndex = _grains.GetGrain<IHHCVisitIndexGrain>($"HHC-VISIT-IDX:{entry.PatientId}");
            List<HHCVisitIndexEntry> visits = await visitIndex.GetAllVisitsAsync();
            Visits.Clear();
            foreach (HHCVisitIndexEntry v in visits) Visits.Add(v);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
