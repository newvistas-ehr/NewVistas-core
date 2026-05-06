// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.PT.GrainInterfaces;
using NewVistas.PT.GrainStates;
using NewVistas.PT.Models;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

/// <summary>
/// Home Exercise Program — prescribe exercises, log completions.
/// Tabs: Active Program, Prescribe, Completion Log.
/// </summary>
public partial class PTHomeExercisesViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<HepPrescription> _activePrescriptions = new();
    [ObservableProperty] private ObservableCollection<HepCompletionLog> _completionLogs = new();

    // Tab visibility
    [ObservableProperty] private bool _showProgram = true;
    [ObservableProperty] private bool _showPrescribe;
    [ObservableProperty] private bool _showLog;

    // Prescribe form
    [ObservableProperty] private string _rxName = string.Empty;
    [ObservableProperty] private string _rxInstructions = string.Empty;
    [ObservableProperty] private string _rxFrequency = string.Empty;
    [ObservableProperty] private ExerciseCategory _rxCategory = ExerciseCategory.Strengthening;
    [ObservableProperty] private BodyGroup _rxBodyGroup = BodyGroup.Knee;
    [ObservableProperty] private Laterality _rxSide = Laterality.Bilateral;
    [ObservableProperty] private int? _rxSets;
    [ObservableProperty] private int? _rxReps;
    [ObservableProperty] private int? _rxDuration;
    [ObservableProperty] private string _rxPrescribedBy = string.Empty;
    [ObservableProperty] private string _rxNotes = string.Empty;

    // Completion log filter
    [ObservableProperty] private DateTime _logFrom = DateTime.Now.AddMonths(-1);
    [ObservableProperty] private DateTime _logTo = DateTime.Now;

    public event Action? BackRequested;

    public PTHomeExercisesViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var grain = Grains.GetGrain<IPTWorkflowGrain>(PatientId);
        var prescriptions = await grain.GetActiveHepPrescriptionsAsync();
        ActivePrescriptions.Clear();
        foreach (var rx in prescriptions) ActivePrescriptions.Add(rx);
    }

    [RelayCommand]
    private void ShowProgramTab() { ShowProgram = true; ShowPrescribe = false; ShowLog = false; }

    [RelayCommand]
    private void ShowPrescribeTab() { ShowProgram = false; ShowPrescribe = true; ShowLog = false; }

    [RelayCommand]
    private void ShowLogTab() { ShowProgram = false; ShowPrescribe = false; ShowLog = true; }

    [RelayCommand]
    private async Task Prescribe()
    {
        if (!HasPatient || string.IsNullOrWhiteSpace(RxName)) return;
        IsLoading = true; Error = null;
        try
        {
            var grain = Grains.GetGrain<IPTWorkflowGrain>(PatientId);
            await grain.AddHepPrescriptionAsync(new HepPrescription
            {
                ExerciseName = RxName,
                Instructions = RxInstructions,
                Frequency = RxFrequency,
                Sets = RxSets,
                Reps = RxReps,
                DurationSeconds = RxDuration,
                BodyGroup = RxBodyGroup,
                Side = RxSide,
                Category = RxCategory,
                PrescribedBy = RxPrescribedBy,
                Notes = RxNotes
            });

            RxName = RxInstructions = RxFrequency = RxPrescribedBy = RxNotes = string.Empty;
            RxSets = RxReps = RxDuration = null;
            ShowProgramTab();
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task LogDone(HepPrescription rx)
    {
        if (!HasPatient) return;
        Error = null;
        try
        {
            var grain = Grains.GetGrain<IPTWorkflowGrain>(PatientId);
            await grain.LogHepCompletionAsync(new HepCompletionLog
            {
                PrescriptionId = rx.PrescriptionId,
                CompletedBy = "Patient",
                SetsCompleted = rx.Sets,
                RepsCompleted = rx.Reps,
                DurationSecondsCompleted = rx.DurationSeconds
            });
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task Discontinue(HepPrescription rx)
    {
        if (!HasPatient) return;
        Error = null;
        try
        {
            var grain = Grains.GetGrain<IPTWorkflowGrain>(PatientId);
            await grain.UpdateHepPrescriptionStatusAsync(rx.PrescriptionId, HepStatus.Discontinued);
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task LoadCompletionLogs()
    {
        if (!HasPatient) return;
        IsLoading = true; Error = null;
        try
        {
            var grain = Grains.GetGrain<IPTWorkflowGrain>(PatientId);
            var logs = await grain.GetHepCompletionLogsAsync(null, LogFrom, LogTo);
            CompletionLogs.Clear();
            foreach (var log in logs) CompletionLogs.Add(log);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void GoBack() => BackRequested?.Invoke();
}
