// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Wpf_UI.Services;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class EdiBillingViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private int _selectedTab;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;

    // Claims tab
    [ObservableProperty] private string _patientId = string.Empty;
    [ObservableProperty] private ObservableCollection<EdiClaimIndexEntry> _claims = new();

    // Transmissions tab
    [ObservableProperty] private ObservableCollection<EdiTransmissionIndexEntry> _transmissions = new();
    [ObservableProperty] private ObservableCollection<EdiTransmissionIndexEntry> _openTransmissions = new();

    // ERAs tab
    [ObservableProperty] private ObservableCollection<EraIndexEntry> _eras = new();

    public EdiBillingViewModel(ApiClient api, OrleansGrainService grains)
    {
        _api = api;
        _grains = grains;
        _ = LoadTransmissionsAndErasAsync();
    }

    partial void OnSelectedTabChanged(int value)
    {
        if (value == 1 || value == 2)
            _ = LoadTransmissionsAndErasAsync();
    }

    private async Task LoadTransmissionsAndErasAsync()
    {
        IsLoading = true; Error = null;
        try
        {
            var txIndex = _grains.GetGrain<IEdiTransmissionIndexGrain>("EDI-TX-IDX");
            List<EdiTransmissionIndexEntry> allTx = await txIndex.GetAllAsync();
            Transmissions.Clear();
            foreach (EdiTransmissionIndexEntry t in allTx) Transmissions.Add(t);
            OpenTransmissions.Clear();
            foreach (EdiTransmissionIndexEntry t in allTx.Where(x => x.Status == "Open")) OpenTransmissions.Add(t);

            var eraIndex = _grains.GetGrain<IEraIndexGrain>("ERA-IDX");
            List<EraIndexEntry> allEras = await eraIndex.GetAllAsync();
            Eras.Clear();
            foreach (EraIndexEntry e in allEras) Eras.Add(e);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task LoadClaims()
    {
        if (string.IsNullOrWhiteSpace(PatientId)) return;
        IsLoading = true; Error = null;
        try
        {
            string pid = PatientId.Trim();
            var claimIndex = _grains.GetGrain<IEdiClaimIndexGrain>($"EDI-CLAIM-IDX:{pid}");
            List<EdiClaimIndexEntry> list = await claimIndex.GetAllAsync();
            Claims.Clear();
            foreach (EdiClaimIndexEntry c in list) Claims.Add(c);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task RefreshTransmissions()
    {
        await LoadTransmissionsAndErasAsync();
    }
}
