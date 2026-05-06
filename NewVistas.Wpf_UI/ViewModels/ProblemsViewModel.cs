// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class ProblemsViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<ProblemSummary> _problems = new();
    [ObservableProperty] private bool _showActiveOnly = true;

    // Add form
    [ObservableProperty] private bool _showAddForm;
    [ObservableProperty] private string _diagnosis = string.Empty;
    [ObservableProperty] private string _diagnosisCode = string.Empty;
    [ObservableProperty] private string _priority = "CHRONIC";
    [ObservableProperty] private DateTime? _dateOfOnset;
    [ObservableProperty] private bool _isServiceConnected;

    public string[] PriorityOptions { get; } = ["ACUTE", "CHRONIC"];

    public ProblemsViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var list = ShowActiveOnly
            ? await workflow.GetActiveProblemsAsync()
            : await workflow.GetAllProblemsAsync();

        Problems.Clear();
        foreach (var p in list) Problems.Add(p);
    }

    [RelayCommand]
    private void ToggleAddForm() => ShowAddForm = !ShowAddForm;

    [RelayCommand]
    private async Task AddProblem()
    {
        if (!HasPatient || string.IsNullOrWhiteSpace(Diagnosis)) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.AddProblemAsync(
                Diagnosis,
                DiagnosisCode.Length > 0 ? DiagnosisCode : null,
                null, // condition
                Priority,
                DateOfOnset,
                null, null, // provider
                null, null, // clinic
                IsServiceConnected,
                null); // comments
            ShowAddForm = false;
            Diagnosis = string.Empty;
            DiagnosisCode = string.Empty;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
