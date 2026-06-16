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

public partial class InpatientPharmacyViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<MarEntry> _marEntries = new();
    [ObservableProperty] private InpatientOrderState? _selectedOrder;

    // New order form
    [ObservableProperty] private bool _showOrderForm;
    [ObservableProperty] private string _drugName = string.Empty;
    [ObservableProperty] private string _orderType = "UNIT_DOSE";
    [ObservableProperty] private string _dosage = string.Empty;
    [ObservableProperty] private string _route = "PO";
    [ObservableProperty] private string _schedule = "QD";
    [ObservableProperty] private string _priority = "ROUTINE";
    [ObservableProperty] private string _wardId = "WARD-MAIN";
    [ObservableProperty] private string _wardName = "Main Ward";
    [ObservableProperty] private string _providerName = "Provider, Test";

    public string[] OrderTypes { get; } = ["UNIT_DOSE", "IV", "LVP"];
    public string[] RouteOptions { get; } = ["PO", "IV", "IM", "SC", "SL", "TOP", "INH", "PR"];
    public string[] PriorityOptions { get; } = ["ROUTINE", "STAT", "ASAP"];

    public InpatientPharmacyViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var mar = await workflow.GetPatientMARAsync();
        MarEntries.Clear();
        foreach (var m in mar) MarEntries.Add(m);
    }

    [RelayCommand]
    private async Task SelectOrder(MarEntry entry)
    {
        try
        {
            var order = Grains.GetGrain<IInpatientOrderGrain>(entry.OrderId);
            SelectedOrder = await order.GetOrderAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private void ToggleOrderForm() => ShowOrderForm = !ShowOrderForm;

    [RelayCommand]
    private async Task PlaceInpatientOrder()
    {
        if (!HasPatient || string.IsNullOrWhiteSpace(DrugName)) return;
        IsLoading = true; Error = null;
        try
        {
            string orderId = $"PSJ-ORDER-{Guid.NewGuid()}";
            var order = Grains.GetGrain<IInpatientOrderGrain>(orderId);
            await order.CreateOrderAsync(
                PatientId, WardId, WardName, null,
                OrderType, DrugName, null,
                Dosage.Length > 0 ? Dosage : null, null,
                Route, Schedule, Priority,
                DateTime.UtcNow, null, null, null,
                null, ProviderName, null,
                null, null, null);

            ShowOrderForm = false;
            DrugName = string.Empty;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task VerifyOrder()
    {
        if (SelectedOrder is null) return;
        IsLoading = true; Error = null;
        try
        {
            var order = Grains.GetGrain<IInpatientOrderGrain>(SelectedOrder.OrderId);
            await order.VerifyAsync("RPH-CURRENT", "Pharmacist, Test");
            SelectedOrder = await order.GetOrderAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
