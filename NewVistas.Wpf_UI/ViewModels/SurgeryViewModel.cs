// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class SurgeryViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<SurgerySummary> _surgeries = new();
    [ObservableProperty] private SurgeryState? _selectedSurgery;

    // Schedule form
    [ObservableProperty] private bool _showScheduleForm;
    [ObservableProperty] private string _principalProcedure = string.Empty;
    [ObservableProperty] private DateTime _dateOfOperation = DateTime.Today.AddDays(7);
    [ObservableProperty] private string _surgeonName = "Surgeon, Test";
    [ObservableProperty] private string _anesthesiaTechnique = "GENERAL";
    [ObservableProperty] private string _surgicalSpecialty = "GENERAL SURGERY";
    [ObservableProperty] private string _preOpDiagnosis = string.Empty;

    public string[] AnesthesiaOptions { get; } = ["GENERAL", "REGIONAL", "LOCAL", "MAC", "SPINAL", "EPIDURAL"];

    public SurgeryViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var list = await workflow.GetSurgeriesAsync(50);
        Surgeries.Clear();
        foreach (var s in list) Surgeries.Add(s);
    }

    [RelayCommand]
    private async Task SelectSurgery(SurgerySummary s)
    {
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            SelectedSurgery = await workflow.GetSurgeryAsync(s.SurgeryId);
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private void ToggleScheduleForm() => ShowScheduleForm = !ShowScheduleForm;

    [RelayCommand]
    private async Task ScheduleSurgery()
    {
        if (!HasPatient || string.IsNullOrWhiteSpace(PrincipalProcedure)) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.ScheduleSurgeryAsync(
                PrincipalProcedure,
                null, // cptCode
                DateOfOperation,
                null, SurgeonName, // surgeon
                AnesthesiaTechnique,
                SurgicalSpecialty,
                PreOpDiagnosis.Length > 0 ? PreOpDiagnosis : null,
                null, null, // location
                null); // comments
            ShowScheduleForm = false;
            PrincipalProcedure = string.Empty;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
