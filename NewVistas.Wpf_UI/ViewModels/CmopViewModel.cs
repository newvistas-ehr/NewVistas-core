// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using System.Net.Http.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class CmopViewModel : ObservableObject
{
    private readonly ApiClient _api;
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private string _siteId = "SITE-1";
    [ObservableProperty] private ObservableCollection<CmopSuspenseEntry> _suspenseQueue = new();
    [ObservableProperty] private ObservableCollection<CmopTransmissionSummary> _transmissions = new();
    [ObservableProperty] private CmopSuspenseEntry? _selectedSuspenseItem;
    [ObservableProperty] private CmopTransmissionSummary? _selectedTransmission;
    [ObservableProperty] private bool _showAddForm;

    // Add to queue form
    [ObservableProperty] private string _rxId = string.Empty;
    [ObservableProperty] private string _addPatientId = string.Empty;
    [ObservableProperty] private string _patientName = string.Empty;
    [ObservableProperty] private string _drugName = string.Empty;
    [ObservableProperty] private string _rxNumber = string.Empty;
    [ObservableProperty] private int _qty = 30;
    [ObservableProperty] private int _daysSupply = 30;
    [ObservableProperty] private string _fillType = "ORIGINAL";
    [ObservableProperty] private string _priority = "ROUTINE";

    public string[] FillTypes { get; } = ["ORIGINAL", "REFILL", "PARTIAL"];
    public string[] Priorities { get; } = ["ROUTINE", "EXPEDITE", "STAT"];

    public CmopViewModel(ApiClient api, OrleansGrainService grains)
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
            string sid = SiteId.Trim();
            var suspenseGrain = _grains.GetGrain<ICmopSuspenseGrain>($"CMOP-SUSPENSE:{sid}");
            List<CmopSuspenseEntry> suspense = await suspenseGrain.GetQueuedPrescriptionsAsync();
            SuspenseQueue.Clear();
            foreach (CmopSuspenseEntry s in suspense) SuspenseQueue.Add(s);

            var txIndex = _grains.GetGrain<ICmopTransmissionIndexGrain>($"CMOP-TX-INDEX:{sid}");
            List<CmopTransmissionSummary> tx = await txIndex.GetTransmissionsAsync();
            Transmissions.Clear();
            foreach (CmopTransmissionSummary t in tx) Transmissions.Add(t);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void ToggleAddForm() => ShowAddForm = !ShowAddForm;

    [RelayCommand]
    private async Task TransmitAsync()
    {
        try
        {
            string sid = SiteId.Trim();
            var suspenseGrain = _grains.GetGrain<ICmopSuspenseGrain>($"CMOP-SUSPENSE:{sid}");
            await suspenseGrain.TransmitQueueAsync("CMOP-FACILITY-1", "CMOP Facility");
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task AddToQueueAsync()
    {
        try
        {
            string sid = SiteId.Trim();
            var suspenseGrain = _grains.GetGrain<ICmopSuspenseGrain>($"CMOP-SUSPENSE:{sid}");
            var entry = new CmopSuspenseEntry
            {
                PrescriptionId = RxId,
                PatientId = AddPatientId,
                PatientName = PatientName,
                DrugName = DrugName,
                RxNumber = RxNumber,
                Quantity = Qty,
                DaysSupply = DaysSupply,
                FillType = FillType,
                Priority = Priority
            };
            await suspenseGrain.AddToSuspenseAsync(entry);
            ShowAddForm = false;
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task RemoveFromQueueAsync()
    {
        if (SelectedSuspenseItem == null) return;
        try
        {
            string sid = SiteId.Trim();
            var suspenseGrain = _grains.GetGrain<ICmopSuspenseGrain>($"CMOP-SUSPENSE:{sid}");
            await suspenseGrain.RemoveFromSuspenseAsync(SelectedSuspenseItem.PrescriptionId);
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task LoadDemoAsync()
    {
        try
        {
            await _api.Http.PostAsJsonAsync("api/cmop/demo/load", new { });
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}
