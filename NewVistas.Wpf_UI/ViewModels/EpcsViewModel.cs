// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class EpcsViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<EpcsPrescriptionIndexEntry> _prescriptions = new();

    public EpcsViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        List<EpcsPrescriptionIndexEntry> list = await workflow.GetEpcsPrescriptionsAsync();
        Prescriptions.Clear();
        foreach (EpcsPrescriptionIndexEntry e in list) Prescriptions.Add(e);
    }
}
