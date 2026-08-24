// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.PT.GrainInterfaces;
using NewVistas.PT.GrainStates;
using NewVistas.PT.Models;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

/// <summary>
/// PT Session — records ROM and strength measurements for a single body group.
/// Shows a data-entry grid per movement, comparison of last 2 sessions, and history.
/// </summary>
public partial class PTSessionViewModel : BasePatientViewModel
{
    [ObservableProperty] private BodyGroup _bodyGroup;
    [ObservableProperty] private string _bodyGroupDisplayName = string.Empty;
    [ObservableProperty] private Laterality _selectedSide = Laterality.Bilateral;
    [ObservableProperty] private string _therapistName = string.Empty;
    [ObservableProperty] private string _locationName = string.Empty;
    [ObservableProperty] private string _sessionNotes = string.Empty;

    [ObservableProperty] private ObservableCollection<MovementRow> _movementRows = new();
    [ObservableProperty] private ObservableCollection<PTSessionState> _comparisonSessions = new();
    [ObservableProperty] private ObservableCollection<PTSessionState> _historySessions = new();

    [ObservableProperty] private bool _showComparison;
    [ObservableProperty] private bool _showHistory;
    [ObservableProperty] private bool _showRecordForm = true;
    [ObservableProperty] private bool _showExercises;
    [ObservableProperty] private DateTime _historyFrom = DateTime.Now.AddMonths(-6);
    [ObservableProperty] private DateTime _historyTo = DateTime.Now;

    // Exercise tab state
    [ObservableProperty] private ObservableCollection<ClinicExerciseLog> _sessionExercises = new();
    [ObservableProperty] private string _exName = string.Empty;
    [ObservableProperty] private ExerciseCategory _exCategory = ExerciseCategory.Strengthening;
    [ObservableProperty] private int? _exSets;
    [ObservableProperty] private int? _exReps;
    [ObservableProperty] private decimal? _exWeight;
    [ObservableProperty] private int? _exDuration;
    [ObservableProperty] private string _exNotes = string.Empty;
    private string? _latestSessionKey;

    /// <summary>Fired when user clicks Back to return to the PT Hub.</summary>
    public event Action? BackRequested;

    public PTSessionViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext) { }

    /// <summary>
    /// Initialize with a specific body group. Called by the PT Hub when navigating.
    /// </summary>
    public void SetBodyGroup(BodyGroup bodyGroup)
    {
        BodyGroup = bodyGroup;
        BodyGroupDisplayName = bodyGroup switch
        {
            BodyGroup.Cervical => "Cervical (Neck)",
            BodyGroup.ThoracicSpine => "Thoracic Spine",
            BodyGroup.LumbarSpine => "Lumbar Spine",
            BodyGroup.TMJ => "TMJ",
            _ => bodyGroup.ToString()
        };

        var movements = BodyGroupDefinitions.GetMovements(bodyGroup);
        MovementRows.Clear();
        foreach (var m in movements)
        {
            var normalRange = BodyGroupDefinitions.GetNormalRomRange(bodyGroup, m);
            MovementRows.Add(new MovementRow(m, FormatMovement(m), normalRange));
        }
    }

    protected override Task LoadDataAsync()
    {
        // Nothing to load on initial load — the form is ready for entry
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task RecordSession()
    {
        if (!HasPatient) return;
        IsLoading = true; Error = null;
        try
        {
            var romMeasurements = new List<RomMeasurement>();
            var strengthMeasurements = new List<StrengthMeasurement>();

            foreach (var row in MovementRows)
            {
                decimal? active = ParseDecimal(row.ActiveRom);
                decimal? passive = ParseDecimal(row.PassiveRom);

                if (active.HasValue || passive.HasValue || !string.IsNullOrWhiteSpace(row.Pain))
                {
                    romMeasurements.Add(new RomMeasurement
                    {
                        Movement = row.Movement,
                        ActiveRom = active,
                        PassiveRom = passive,
                        PainOnMotion = row.Pain
                    });
                }

                if (!string.IsNullOrWhiteSpace(row.StrengthGrade))
                {
                    var parsed = BodyGroupDefinitions.ParseMmtGrade(row.StrengthGrade);
                    if (parsed == null) { Error = $"Invalid grade '{row.StrengthGrade}' for {row.DisplayName}."; return; }
                    strengthMeasurements.Add(new StrengthMeasurement
                    {
                        Movement = row.Movement,
                        Grade = parsed.Value.grade,
                        GradeDisplay = parsed.Value.display,
                        Comments = row.StrengthComments
                    });
                }
            }

            if (romMeasurements.Count == 0 && strengthMeasurements.Count == 0)
            { Error = "Enter at least one ROM or strength measurement."; return; }

            var grain = Grains.GetGrain<IPTWorkflowGrain>(PatientId);
            await grain.RecordBodyGroupSessionAsync(
                BodyGroup, DateTime.UtcNow,
                null, TherapistName,
                null, LocationName,
                SelectedSide,
                romMeasurements, strengthMeasurements,
                SessionNotes);

            // Reset form
            foreach (var row in MovementRows) row.Reset();
            SessionNotes = string.Empty;
            Error = null;
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task LoadComparison()
    {
        if (!HasPatient) return;
        ShowComparison = true; ShowHistory = false; ShowRecordForm = false; ShowExercises = false;
        IsLoading = true; Error = null;
        try
        {
            var grain = Grains.GetGrain<IPTWorkflowGrain>(PatientId);
            var sessions = await grain.GetLatestSessionsAsync(BodyGroup, 2);
            ComparisonSessions.Clear();
            foreach (var s in sessions) ComparisonSessions.Add(s);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task LoadHistory()
    {
        if (!HasPatient) return;
        ShowHistory = true; ShowComparison = false; ShowRecordForm = false; ShowExercises = false;
        IsLoading = true; Error = null;
        try
        {
            var grain = Grains.GetGrain<IPTWorkflowGrain>(PatientId);
            var sessions = await grain.GetSessionHistoryAsync(BodyGroup, HistoryFrom, HistoryTo, 50);
            HistorySessions.Clear();
            foreach (var s in sessions) HistorySessions.Add(s);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void ShowRecord()
    {
        ShowRecordForm = true; ShowComparison = false; ShowHistory = false; ShowExercises = false;
    }

    [RelayCommand]
    private async Task LoadExercises()
    {
        ShowExercises = true; ShowRecordForm = false; ShowComparison = false; ShowHistory = false;
        if (!HasPatient) return;
        IsLoading = true; Error = null;
        try
        {
            var grain = Grains.GetGrain<IPTWorkflowGrain>(PatientId);
            var latest = await grain.GetLatestSessionsAsync(BodyGroup, 1);
            SessionExercises.Clear();
            if (latest.Count > 0)
            {
                _latestSessionKey = latest[0].SessionId;
                foreach (var ex in latest[0].Exercises) SessionExercises.Add(ex);
            }
            else { _latestSessionKey = null; }
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task AddExercise()
    {
        if (!HasPatient || _latestSessionKey == null || string.IsNullOrWhiteSpace(ExName)) return;
        IsLoading = true; Error = null;
        try
        {
            var grain = Grains.GetGrain<IPTWorkflowGrain>(PatientId);
            await grain.AddClinicExerciseAsync(_latestSessionKey, new ClinicExerciseLog
            {
                ExerciseName = ExName,
                Category = ExCategory,
                BodyGroup = BodyGroup,
                Sets = ExSets,
                Reps = ExReps,
                WeightLbs = ExWeight,
                DurationSeconds = ExDuration,
                Notes = ExNotes
            });
            ExName = ExNotes = string.Empty; ExSets = ExReps = ExDuration = null; ExWeight = null;
            await LoadExercises();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void GoBack() => BackRequested?.Invoke();

    private static string FormatMovement(Movement m) =>
        System.Text.RegularExpressions.Regex.Replace(m.ToString(), "(\\B[A-Z])", " $1");

    private static decimal? ParseDecimal(string? s) =>
        !string.IsNullOrWhiteSpace(s) && decimal.TryParse(s, out var d) ? d : null;
}

/// <summary>
/// Row in the measurement entry grid — one per movement.
/// </summary>
public partial class MovementRow : ObservableObject
{
    public Movement Movement { get; }
    public string DisplayName { get; }
    public string NormalRange { get; }

    [ObservableProperty] private string? _activeRom;
    [ObservableProperty] private string? _passiveRom;
    [ObservableProperty] private string? _pain;
    [ObservableProperty] private string? _strengthGrade;
    [ObservableProperty] private string? _strengthComments;

    public MovementRow(Movement movement, string displayName, decimal? normalRange)
    {
        Movement = movement;
        DisplayName = displayName;
        NormalRange = normalRange.HasValue ? $"{normalRange.Value}°" : "—";
    }

    public void Reset()
    {
        ActiveRom = PassiveRom = Pain = StrengthGrade = StrengthComments = null;
    }
}
