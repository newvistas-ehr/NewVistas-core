// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class NursingCarePlanViewModel : BasePatientViewModel
{
    [ObservableProperty] private NursingCarePlanState? _carePlan;
    [ObservableProperty] private string? _actionMessage;

    // Add diagnosis fields
    [ObservableProperty] private string _newDiagnosis = string.Empty;
    [ObservableProperty] private string _newRelatedTo = string.Empty;
    [ObservableProperty] private string _newEvidencedBy = string.Empty;

    public NursingCarePlanViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        CarePlan = await workflow.GetNursingCarePlanAsync();
    }

    [RelayCommand]
    private async Task AddDiagnosis()
    {
        if (string.IsNullOrWhiteSpace(NewDiagnosis)) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            string id = await workflow.AddNursingDiagnosisAsync(NewDiagnosis, NewRelatedTo, NewEvidencedBy, null, null, null);
            ActionMessage = $"Diagnosis added: {id}";
            NewDiagnosis = string.Empty; NewRelatedTo = string.Empty; NewEvidencedBy = string.Empty;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task ResolveDiagnosis(NursingCarePlanProblem problem)
    {
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.ResolveNursingDiagnosisAsync(problem.ProblemId, "Resolved via WPF");
            ActionMessage = "Diagnosis resolved.";
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
