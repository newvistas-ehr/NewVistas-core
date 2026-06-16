// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.WpfDelphiUI.Services;

namespace NewVistas.WpfDelphiUI.ViewModels;

public sealed partial class OrdersViewModel : ChartTabViewModelBase
{
    public ObservableCollection<OrderDto> Orders { get; } = new();
    public ObservableCollection<OrderDto> HistoryOrders { get; } = new();

    [ObservableProperty] private OrderDto? _selectedOrder;
    [ObservableProperty] private bool _showPlaceForm;
    [ObservableProperty] private bool _showHistory;
    [ObservableProperty] private DateTime _historyFrom = DateTime.Now.AddDays(-90);
    [ObservableProperty] private DateTime _historyTo = DateTime.Now;
    [ObservableProperty] private string _newOrderText = string.Empty;
    [ObservableProperty] private string _newOrderType = "LAB";
    [ObservableProperty] private string _newPriority = "ROUTINE";

    public string[] OrderTypes { get; } = ["LAB", "PHARMACY", "RADIOLOGY", "CONSULT", "NURSING", "DIET"];
    public string[] Priorities { get; } = ["ROUTINE", "STAT", "ASAP"];

    public OrdersViewModel(ApiClient api, PatientContext context) : base(api, context) { }

    protected override async Task LoadAsync()
    {
        var items = await Api.GetOrdersAsync(PatientId);
        Orders.Clear();
        foreach (var o in items) Orders.Add(o);
    }

    protected override void ClearData() { Orders.Clear(); HistoryOrders.Clear(); SelectedOrder = null; }

    [RelayCommand]
    private void TogglePlaceForm() => ShowPlaceForm = !ShowPlaceForm;

    [RelayCommand]
    private void ToggleHistory() => ShowHistory = !ShowHistory;

    [RelayCommand]
    private async Task LoadHistoryAsync()
    {
        ErrorText = string.Empty;
        try
        {
            var items = await Api.GetOrderHistoryAsync(PatientId, HistoryFrom, HistoryTo);
            HistoryOrders.Clear();
            foreach (var o in items) HistoryOrders.Add(o);
        }
        catch (Exception ex) { ErrorText = ex.Message; }
    }

    [RelayCommand]
    private async Task PlaceOrderAsync()
    {
        if (string.IsNullOrWhiteSpace(NewOrderText)) return;
        ErrorText = string.Empty;
        try
        {
            await Api.PlaceOrderAsync(PatientId, new
            {
                OrderText = NewOrderText,
                OrderType = NewOrderType,
                Priority = NewPriority
            });
            NewOrderText = string.Empty;
            ShowPlaceForm = false;
            await ReloadAsync();
        }
        catch (Exception ex) { ErrorText = ex.Message; }
    }

    [RelayCommand]
    private async Task SignOrderAsync()
    {
        if (SelectedOrder == null) return;
        ErrorText = string.Empty;
        try
        {
            await Api.SignOrderAsync(PatientId, SelectedOrder.OrderId, new
            {
                ElectronicSignature = "CPRS-ES"
            });
            await ReloadAsync();
        }
        catch (Exception ex) { ErrorText = ex.Message; }
    }

    [RelayCommand]
    private async Task DiscontinueOrderAsync()
    {
        if (SelectedOrder == null) return;
        ErrorText = string.Empty;
        try
        {
            await Api.DiscontinueOrderAsync(PatientId, SelectedOrder.OrderId);
            await ReloadAsync();
        }
        catch (Exception ex) { ErrorText = ex.Message; }
    }
}
