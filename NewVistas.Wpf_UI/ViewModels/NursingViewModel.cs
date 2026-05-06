// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class NursingViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<NursingAssessmentIndexEntry> _assessments = new();
    [ObservableProperty] private NursingCarePlanState? _carePlan;
    [ObservableProperty] private NursingAcuityState? _acuity;

    public NursingViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var assessments = await workflow.GetNursingAssessmentsAsync();
        Assessments.Clear();
        foreach (var a in assessments) Assessments.Add(a);

        CarePlan = await workflow.GetNursingCarePlanAsync();
        Acuity = await workflow.GetNursingAcuityAsync();
    }
}
