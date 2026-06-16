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

public partial class DecisionSupportViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<DsiInterventionSummary> _interventions = new();
    [ObservableProperty] private ObservableCollection<DsiEventSummary> _events = new();
    [ObservableProperty] private string _evalPatientId = string.Empty;

    public DecisionSupportViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var index = Grains.GetGrain<IDsiInterventionIndexGrain>("DSI-INDEX");
        List<DsiInterventionSummary> list = await index.GetAllInterventionsAsync();
        Interventions.Clear();
        foreach (var i in list) Interventions.Add(i);
    }

    [RelayCommand]
    private async Task LoadEvents()
    {
        IsLoading = true; Error = null;
        try
        {
            var index = Grains.GetGrain<IDsiEventIndexGrain>("DSI-EVENT-INDEX");
            List<DsiEventSummary> list = await index.GetAllEventsAsync();
            Events.Clear();
            foreach (var e in list) Events.Add(e);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task EvaluatePatient()
    {
        if (string.IsNullOrWhiteSpace(EvalPatientId)) return;
        IsLoading = true; Error = null;
        try
        {
            // Load events for the patient as a proxy for evaluation results
            var index = Grains.GetGrain<IDsiEventIndexGrain>("DSI-EVENT-INDEX");
            List<DsiEventSummary> patientEvents = await index.GetEventsByPatientAsync(EvalPatientId.Trim());
            Events.Clear();
            foreach (var e in patientEvents) Events.Add(e);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
