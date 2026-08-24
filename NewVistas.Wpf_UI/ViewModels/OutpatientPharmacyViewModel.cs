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

public partial class OutpatientPharmacyViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<PrescriptionIndexEntry> _prescriptions = new();
    [ObservableProperty] private PharmacyState? _selectedPrescription;
    [ObservableProperty] private List<RefillRecord> _refillHistory = new();

    /// <summary>Grid selection; loads the full prescription into <see cref="SelectedPrescription"/>.</summary>
    [ObservableProperty] private PrescriptionIndexEntry? _selectedEntry;

    partial void OnSelectedEntryChanged(PrescriptionIndexEntry? value)
    {
        // Actions gate on the detail object; clear it before the async fetch so they can never target the previously selected record.
        SelectedPrescription = null;
        if (value is not null) _ = SelectPrescription(value);
    }

    // Action state
    [ObservableProperty] private bool _showDiscontinueForm;
    [ObservableProperty] private string _discontinueReason = string.Empty;
    [ObservableProperty] private string? _actionMessage;

    public OutpatientPharmacyViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var index = Grains.GetGrain<IPatientPrescriptionIndexGrain>($"PSO-INDEX:{PatientId}");
        var list = await index.GetAllAsync();
        Prescriptions.Clear();
        foreach (var p in list) Prescriptions.Add(p);
        SelectedPrescription = null;
        RefillHistory = new();
    }

    [RelayCommand]
    private async Task SelectPrescription(PrescriptionIndexEntry entry)
    {
        ActionMessage = null;
        ShowDiscontinueForm = false;
        try
        {
            var rx = Grains.GetGrain<IPharmacyGrain>(entry.PrescriptionId);
            SelectedPrescription = await rx.GetPrescriptionAsync();
            RefillHistory = await rx.GetRefillHistoryAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task FillRx()
    {
        if (SelectedPrescription is null) return;
        await PerformAction(async () =>
        {
            var rx = Grains.GetGrain<IPharmacyGrain>(SelectedPrescription.PrescriptionId);
            await rx.FillPrescriptionAsync(DateTime.UtcNow);
            ActionMessage = "Prescription filled.";
        });
    }

    [RelayCommand]
    private async Task RefillRx()
    {
        if (SelectedPrescription is null) return;
        await PerformAction(async () =>
        {
            var rx = Grains.GetGrain<IPharmacyGrain>(SelectedPrescription.PrescriptionId);
            await rx.RefillAsync(DateTime.UtcNow);
            ActionMessage = "Prescription refilled.";
        });
    }

    [RelayCommand]
    private async Task HoldRx()
    {
        if (SelectedPrescription is null) return;
        await PerformAction(async () =>
        {
            var rx = Grains.GetGrain<IPharmacyGrain>(SelectedPrescription.PrescriptionId);
            await rx.PlaceOnHoldAsync("Placed on hold.");
            ActionMessage = "Prescription placed on hold.";
        });
    }

    [RelayCommand]
    private async Task ResumeRx()
    {
        if (SelectedPrescription is null) return;
        await PerformAction(async () =>
        {
            var rx = Grains.GetGrain<IPharmacyGrain>(SelectedPrescription.PrescriptionId);
            await rx.ResumeAsync();
            ActionMessage = "Prescription resumed.";
        });
    }

    [RelayCommand]
    private async Task VerifyRx()
    {
        if (SelectedPrescription is null) return;
        await PerformAction(async () =>
        {
            var rx = Grains.GetGrain<IPharmacyGrain>(SelectedPrescription.PrescriptionId);
            await rx.VerifyAsync("RPH-CURRENT");
            ActionMessage = "Prescription verified.";
        });
    }

    [RelayCommand]
    private void ShowDiscontinue() { ShowDiscontinueForm = true; DiscontinueReason = string.Empty; }

    [RelayCommand]
    private async Task ConfirmDiscontinue()
    {
        if (SelectedPrescription is null) return;
        await PerformAction(async () =>
        {
            var rx = Grains.GetGrain<IPharmacyGrain>(SelectedPrescription.PrescriptionId);
            await rx.DiscontinueAsync(DiscontinueReason);
            ShowDiscontinueForm = false;
            ActionMessage = "Prescription discontinued.";
        });
    }

    private async Task PerformAction(Func<Task> action)
    {
        IsLoading = true; Error = null;
        try
        {
            await action();
            if (SelectedPrescription is not null)
            {
                var rx = Grains.GetGrain<IPharmacyGrain>(SelectedPrescription.PrescriptionId);
                SelectedPrescription = await rx.GetPrescriptionAsync();
                RefillHistory = await rx.GetRefillHistoryAsync();
            }
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
