// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class EventCaptureViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<EventCaptureIndexEntry> _encounters = new();
    [ObservableProperty] private EventCaptureIndexEntry? _selectedEncounter;

    public EventCaptureViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var list = await workflow.GetEventCaptureEncountersAsync(100);
        Encounters.Clear();
        foreach (var e in list) Encounters.Add(e);
    }
}
