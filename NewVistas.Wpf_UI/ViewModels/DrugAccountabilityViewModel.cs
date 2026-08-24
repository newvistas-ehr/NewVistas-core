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

public partial class DrugAccountabilityViewModel : ObservableObject
{
    private readonly OrleansGrainService _grains;

    [ObservableProperty] private string _locationId = "DA-LOCATION-001";
    [ObservableProperty] private ObservableCollection<DrugBalanceSummary> _drugs = new();
    [ObservableProperty] private DrugBalanceSummary? _selectedDrug;
    [ObservableProperty] private List<DrugAccountabilityTransaction>? _transactionHistory;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _error;

    // Transaction form
    [ObservableProperty] private bool _showTransactionForm;
    [ObservableProperty] private string _transactionType = "RECEIPT";
    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private string _performedByName = "Staff, Test";
    [ObservableProperty] private string _notes = string.Empty;

    public string[] TransactionTypes { get; } = ["RECEIPT", "DISPENSE", "WASTE", "RETURN", "TRANSFER", "INVENTORY COUNT"];

    public DrugAccountabilityViewModel(OrleansGrainService grains)
    {
        _grains = grains;
    }

    [RelayCommand]
    private async Task LoadInventory()
    {
        if (string.IsNullOrWhiteSpace(LocationId)) return;
        IsLoading = true; Error = null;
        try
        {
            string loc = LocationId.Trim();
            var locGrain = _grains.GetGrain<IDrugAccountabilityLocationGrain>($"DA-LOC:{loc}");
            List<DrugBalanceSummary> drugs = await locGrain.GetAllDrugsAsync();
            Drugs.Clear();
            foreach (DrugBalanceSummary d in drugs) Drugs.Add(d);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task SelectDrug(DrugBalanceSummary drug)
    {
        SelectedDrug = drug;
        TransactionHistory = null;
        try
        {
            string loc = LocationId.Trim();
            var daGrain = _grains.GetGrain<IDrugAccountabilityGrain>($"DA:{loc}:{drug.DrugId}");
            TransactionHistory = await daGrain.GetTransactionHistoryAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private void ToggleTransactionForm() => ShowTransactionForm = !ShowTransactionForm;

    [RelayCommand]
    private async Task RecordTransaction()
    {
        if (SelectedDrug is null || Quantity <= 0) return;
        IsLoading = true; Error = null;
        try
        {
            string loc = LocationId.Trim();
            var daGrain = _grains.GetGrain<IDrugAccountabilityGrain>($"DA:{loc}:{SelectedDrug.DrugId}");
            string? notesVal = Notes.Length > 0 ? Notes : null;

            switch (TransactionType)
            {
                case "RECEIPT":
                    await daGrain.ReceiveStockAsync(Quantity, null, null, null, PerformedByName, notesVal);
                    break;
                case "DISPENSE":
                    await daGrain.DispenseToPatientAsync(Quantity, null, null, null, PerformedByName);
                    break;
                case "WASTE":
                    await daGrain.RecordWasteAsync(Quantity, null, null, null, PerformedByName, notesVal);
                    break;
                case "RETURN":
                    await daGrain.RecordReturnAsync(Quantity, null, null, null, PerformedByName, notesVal);
                    break;
                case "TRANSFER":
                    await daGrain.TransferToAsync(Quantity, "UNKNOWN", null, PerformedByName, notesVal);
                    break;
                case "INVENTORY COUNT":
                    await daGrain.RecordInventoryCountAsync(Quantity, null, PerformedByName, notesVal);
                    break;
            }

            ShowTransactionForm = false;
            Quantity = 0;
            Notes = string.Empty;
            await SelectDrug(SelectedDrug);
            await LoadInventory();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
