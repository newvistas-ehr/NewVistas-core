// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class PatientRecallViewModel : BasePatientViewModel
{
    [ObservableProperty] private bool _isFeatureEnabled;
    [ObservableProperty] private string? _successMessage;
    [ObservableProperty] private ObservableCollection<PatientRecallIndexEntry> _entries = new();
    [ObservableProperty] private bool _showNewForm;

    // Form fields
    [ObservableProperty] private string _clinicId = string.Empty;
    [ObservableProperty] private string _clinicName = string.Empty;
    [ObservableProperty] private string _recallType = "FOLLOW-UP";
    [ObservableProperty] private DateTime _recallDate = DateTime.Today.AddMonths(3);
    [ObservableProperty] private string? _providerName;
    [ObservableProperty] private string? _diagnosis;
    [ObservableProperty] private string? _instructions;

    public PatientRecallViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        SuccessMessage = null;
        Entries.Clear();

        var siteParams = Grains.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
        IsFeatureEnabled = await siteParams.IsFeatureEnabledAsync("PATIENT_RECALL");

        if (!IsFeatureEnabled) return;

        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var entries = await workflow.GetRecallEntriesAsync();
        foreach (var e in entries) Entries.Add(e);
    }

    [RelayCommand]
    public async Task CreateRecallAsync()
    {
        if (!HasPatient) return;
        IsLoading = true;
        Error = null;
        SuccessMessage = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.CreateRecallEntryAsync(
                ClinicId, ClinicName,
                RecallType, RecallDate,
                null, ProviderName,
                Diagnosis, Instructions,
                "PROVIDER-CURRENT", "Provider, Test");

            SuccessMessage = "Recall created successfully.";
            ShowNewForm = false;

            ClinicId = string.Empty;
            ClinicName = string.Empty;
            RecallType = "FOLLOW-UP";
            RecallDate = DateTime.Today.AddMonths(3);
            ProviderName = null;
            Diagnosis = null;
            Instructions = null;

            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    public void ToggleNewForm()
    {
        ShowNewForm = !ShowNewForm;
        SuccessMessage = null;
    }
}
