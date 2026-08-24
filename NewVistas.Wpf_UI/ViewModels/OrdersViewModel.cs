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

public partial class OrdersViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<OrderSummary> _orders = new();
    [ObservableProperty] private string _statusFilter = "Current (Active/Pending/Hold)";
    [ObservableProperty] private OrderSummary? _selectedOrder;

    // History
    [ObservableProperty] private ObservableCollection<OrderSummary> _historyOrders = new();
    [ObservableProperty] private bool _showHistory;
    [ObservableProperty] private DateTime _historyFrom = DateTime.Now.AddDays(-90);
    [ObservableProperty] private DateTime _historyTo = DateTime.Now;

    // Place order form — property names match OrdersView.xaml bindings
    [ObservableProperty] private bool _showPlaceForm;
    [ObservableProperty] private string _newOrderType = "LAB";
    [ObservableProperty] private string _newOrderText = string.Empty;
    [ObservableProperty] private string _newPriority = "ROUTINE";
    [ObservableProperty] private string? _newInstructions;

    // Clinic / location picker
    [ObservableProperty] private ObservableCollection<ClinicEntry> _clinics = new();
    [ObservableProperty] private ClinicEntry? _selectedClinic;
    private bool _clinicsLoaded;

    // Order sets
    [ObservableProperty] private bool _showOrderSets;
    [ObservableProperty] private ObservableCollection<OrderSetEntry> _orderSetList = new();
    [ObservableProperty] private OrderSetEntry? _selectedOrderSet;
    [ObservableProperty] private OrderSetState? _selectedOrderSetDetail;

    public string[] OrderTypes { get; } = ["LAB", "PHARMACY", "RADIOLOGY", "CONSULT", "NURSING", "DIET", "VITALS", "PROCEDURE", "OTHER"];
    public string[] PriorityOptions { get; } = ["ROUTINE", "URGENT", "STAT"];
    public string[] FilterOptions { get; } =
    [
        "Current (Active/Pending/Hold)",
        "All Orders",
        "Discontinued",
        "Completed/Expired",
        "Pending",
        "Unsigned"
    ];

    public OrdersViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext)
    {
        // Check for pending workflow action from Cover Sheet navigation
        var action = patientContext.TakePendingAction();
        if (action != null && action.StartsWith("NEW_ORDER"))
        {
            ShowPlaceForm = true;
            var parts = action.Split(':');
            if (parts.Length > 1)
                NewOrderType = parts[1]; // e.g., "PHARMACY", "LAB", "CONSULT"
        }
    }

    private int FilterIndex => StatusFilter switch
    {
        "All Orders" => 1,
        "Current (Active/Pending/Hold)" => 2,
        "Discontinued" => 3,
        "Completed/Expired" => 4,
        "Pending" => 7,
        "Unsigned" => 11,
        _ => 2
    };

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var list = await workflow.GetOrdersByFilterAsync(FilterIndex);
        Orders.Clear();
        foreach (var o in list) Orders.Add(o);

        // Load clinics on first data load if not yet loaded
        if (!_clinicsLoaded)
            await LoadClinicsAsync();
    }

    [RelayCommand]
    private void TogglePlaceForm()
    {
        ShowPlaceForm = !ShowPlaceForm;
        if (ShowPlaceForm && !_clinicsLoaded)
            _ = LoadClinicsAsync();
    }

    [RelayCommand]
    private void ToggleHistory() => ShowHistory = !ShowHistory;

    [RelayCommand]
    private void ToggleOrderSets()
    {
        ShowOrderSets = !ShowOrderSets;
        if (ShowOrderSets && OrderSetList.Count == 0)
            _ = LoadOrderSetsAsync();
    }

    [RelayCommand]
    private async Task LoadHistory()
    {
        if (!HasPatient) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            var list = await workflow.GetOrderHistoryAsync(HistoryFrom, HistoryTo, 100);
            HistoryOrders.Clear();
            foreach (var o in list) HistoryOrders.Add(o);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task PlaceOrder()
    {
        if (!HasPatient || string.IsNullOrWhiteSpace(NewOrderText)) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.PlaceOrderAsync(
                NewOrderType, NewOrderText,
                null, // orderableItemId
                "PROVIDER-001", "Current Provider",
                SelectedClinic?.ClinicId, SelectedClinic?.Name,
                NewPriority,
                NewInstructions, null);
            ShowPlaceForm = false;
            NewOrderText = string.Empty;
            NewInstructions = null;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    /// <summary>The user's e-signature code, verified by the workflow grain on sign.</summary>
    [ObservableProperty] private string _signatureCode = string.Empty;

    [RelayCommand]
    private async Task SignOrder()
    {
        if (SelectedOrder is null || !HasPatient) return;
        if (string.IsNullOrWhiteSpace(SignatureCode))
        {
            Error = "Enter your electronic signature code to sign.";
            return;
        }
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            // The grain verifies the code against the signed-in user's stored hash — this
            // client previously signed with the literal string "ESIG".
            await workflow.SignOrderAsync(SelectedOrder.OrderId, SignatureCode);
            SignatureCode = string.Empty;
            await LoadDataAsync();
        }
        catch (UnauthorizedAccessException) { Error = "That electronic signature code was not accepted."; }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task DiscontinueOrder()
    {
        if (SelectedOrder is null || !HasPatient) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.DiscontinueOrderAsync(SelectedOrder.OrderId, "Discontinued by provider");
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    private async Task LoadClinicsAsync()
    {
        try
        {
            var clinicIndex = Grains.GetGrain<IClinicIndexGrain>("SD-CLINIC-INDEX");
            var list = await clinicIndex.GetAllClinicsAsync();
            Clinics.Clear();
            foreach (var c in list.Where(c => c.Status == "ACTIVE")) Clinics.Add(c);
            _clinicsLoaded = true;
        }
        catch (Exception ex) { Error = $"Error loading clinics: {ex.Message}"; }
    }

    private async Task LoadOrderSetsAsync()
    {
        try
        {
            var indexGrain = Grains.GetGrain<IOrderSetIndexGrain>("ORDER-SET-INDEX");
            var list = await indexGrain.GetAllOrderSetsAsync();
            OrderSetList.Clear();
            foreach (var os in list.Where(os => os.IsActive)) OrderSetList.Add(os);
        }
        catch (Exception ex) { Error = $"Error loading order sets: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task SelectOrderSet()
    {
        if (SelectedOrderSet is null) return;
        try
        {
            var setGrain = Grains.GetGrain<IOrderSetGrain>(SelectedOrderSet.OrderSetId);
            SelectedOrderSetDetail = await setGrain.GetOrderSetAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task ExecuteOrderSet()
    {
        if (SelectedOrderSet is null || !HasPatient) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            var orderIds = await workflow.ExecuteOrderSetAsync(
                SelectedOrderSet.OrderSetId,
                "PROVIDER-001", "Current Provider",
                SelectedClinic?.ClinicId, SelectedClinic?.Name,
                null);
            ShowOrderSets = false;
            SelectedOrderSetDetail = null;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
