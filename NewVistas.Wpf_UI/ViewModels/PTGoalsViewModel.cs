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
/// PT Goals — manage therapeutic goals across body groups.
/// Tabs: Active Goals, Add Goal, All Goals.
/// </summary>
public partial class PTGoalsViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<PTGoal> _activeGoals = new();
    [ObservableProperty] private ObservableCollection<PTGoal> _allGoals = new();

    // Tab visibility
    [ObservableProperty] private bool _showActiveGoals = true;
    [ObservableProperty] private bool _showAddGoal;
    [ObservableProperty] private bool _showAllGoals;

    // Add goal form
    [ObservableProperty] private BodyGroup _newGoalBodyGroup = BodyGroup.Shoulder;
    [ObservableProperty] private GoalType _newGoalType = GoalType.ROM;
    [ObservableProperty] private Laterality _newGoalSide = Laterality.Bilateral;
    [ObservableProperty] private string _newGoalDescription = string.Empty;
    [ObservableProperty] private decimal _newGoalBaseline;
    [ObservableProperty] private decimal _newGoalTarget;
    [ObservableProperty] private DateTime? _newGoalTargetDate;
    [ObservableProperty] private string _newGoalNotes = string.Empty;

    // Progress update form
    [ObservableProperty] private bool _showProgressForm;
    [ObservableProperty] private PTGoal? _selectedGoal;
    [ObservableProperty] private decimal _progressValue;
    [ObservableProperty] private string _progressNotes = string.Empty;

    // All goals filter
    [ObservableProperty] private BodyGroup _filterBodyGroup = BodyGroup.Shoulder;

    public event Action? BackRequested;

    public PTGoalsViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var grain = Grains.GetGrain<IPTWorkflowGrain>(PatientId);
        var goals = await grain.GetAllActiveGoalsAsync();
        ActiveGoals.Clear();
        foreach (var g in goals) ActiveGoals.Add(g);
    }

    [RelayCommand]
    private void ShowActive() { ShowActiveGoals = true; ShowAddGoal = false; ShowAllGoals = false; }

    [RelayCommand]
    private void ShowAdd() { ShowActiveGoals = false; ShowAddGoal = true; ShowAllGoals = false; }

    [RelayCommand]
    private void ShowAll() { ShowActiveGoals = false; ShowAddGoal = false; ShowAllGoals = true; }

    [RelayCommand]
    private async Task AddGoal()
    {
        if (!HasPatient || string.IsNullOrWhiteSpace(NewGoalDescription)) return;
        IsLoading = true; Error = null;
        try
        {
            var grain = Grains.GetGrain<IPTWorkflowGrain>(PatientId);
            await grain.AddGoalAsync(NewGoalBodyGroup, new PTGoal
            {
                GoalType = NewGoalType,
                Side = NewGoalSide,
                Description = NewGoalDescription,
                TargetValue = NewGoalTarget,
                BaselineValue = NewGoalBaseline,
                CurrentValue = NewGoalBaseline,
                TargetDate = NewGoalTargetDate,
                Notes = NewGoalNotes
            });

            NewGoalDescription = string.Empty; NewGoalNotes = string.Empty;
            NewGoalBaseline = 0; NewGoalTarget = 0; NewGoalTargetDate = null;
            ShowActive();
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void SelectGoalForProgress(PTGoal goal)
    {
        SelectedGoal = goal;
        ProgressValue = goal.CurrentValue;
        ProgressNotes = string.Empty;
        ShowProgressForm = true;
    }

    [RelayCommand]
    private async Task RecordProgress()
    {
        if (!HasPatient || SelectedGoal == null) return;
        IsLoading = true; Error = null;
        try
        {
            var grain = Grains.GetGrain<IPTWorkflowGrain>(PatientId);
            await grain.AddGoalProgressAsync(SelectedGoal.BodyGroup, SelectedGoal.GoalId, ProgressValue, ProgressNotes);
            ShowProgressForm = false;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task LoadAllGoals()
    {
        if (!HasPatient) return;
        IsLoading = true; Error = null;
        try
        {
            var grain = Grains.GetGrain<IPTWorkflowGrain>(PatientId);
            var goals = await grain.GetGoalsForBodyGroupAsync(FilterBodyGroup);
            AllGoals.Clear();
            foreach (var g in goals) AllGoals.Add(g);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void GoBack() => BackRequested?.Invoke();
}
