// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class AutoEligibilityViewModel : BasePatientViewModel
{
    [ObservableProperty] private AutoEligibilityDeterminationState? _determination;
    [ObservableProperty] private string? _actionMessage;

    public AutoEligibilityViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        Determination = await workflow.GetAutoEligibilityDeterminationAsync();
    }

    [RelayCommand]
    private async Task RunDetermination()
    {
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            Determination = await workflow.RunAutoEligibilityDeterminationAsync(null, null);
            ActionMessage = "Determination completed.";
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
