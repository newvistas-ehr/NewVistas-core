// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class LabEdiViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<LabEdiLabSummary> _referenceLabs = new();
    [ObservableProperty] private ObservableCollection<LabEdiOrderSummary> _orders = new();
    [ObservableProperty] private LabEdiLabSummary? _selectedLab;
    [ObservableProperty] private LabEdiOrderSummary? _selectedOrder;
    [ObservableProperty] private LabEdiOrderState? _orderDetail;

    public LabEdiViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var index = Grains.GetGrain<ILabEdiIndexGrain>("LAB-EDI-INDEX");
        var labs = await index.GetReferenceLabsAsync();
        ReferenceLabs.Clear();
        foreach (var l in labs) ReferenceLabs.Add(l);

        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var orders = await workflow.GetLabEdiOrdersAsync(50);
        Orders.Clear();
        foreach (var o in orders) Orders.Add(o);
    }

    [RelayCommand]
    private async Task LoadOrderDetailAsync()
    {
        if (SelectedOrder == null) return;
        try
        {
            var order = Grains.GetGrain<ILabEdiOrderGrain>(SelectedOrder.OrderId);
            OrderDetail = await order.GetOrderAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task LoadDemoAsync()
    {
        try
        {
            var index = Grains.GetGrain<ILabEdiIndexGrain>("LAB-EDI-INDEX");
            await index.SeedDemoDataAsync();
            await LoadAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}
