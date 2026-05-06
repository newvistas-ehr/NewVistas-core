// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class HomeTelehealthViewModel : BasePatientViewModel
{
    [ObservableProperty] private HomeTelehealthPatientState? _patient;
    [ObservableProperty] private ObservableCollection<HtReadingIndexEntry> _readings = new();
    [ObservableProperty] private ObservableCollection<HtAlertIndexEntry> _alerts = new();

    public HomeTelehealthViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        Patient = await workflow.GetHtPatientAsync();

        var readings = await workflow.GetHtReadingsAsync(null, null, 50);
        Readings.Clear();
        foreach (var r in readings) Readings.Add(r);

        var alerts = await workflow.GetHtAlertsAsync(null);
        Alerts.Clear();
        foreach (var a in alerts) Alerts.Add(a);
    }
}
