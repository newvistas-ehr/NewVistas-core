// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class NursingTaskWorklistViewModel : BasePatientViewModel
{
    [ObservableProperty] private NursingTaskWorklistState? _worklist;
    [ObservableProperty] private ObservableCollection<NursingTask> _dueTasks = new();
    [ObservableProperty] private string? _actionMessage;

    public NursingTaskWorklistViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        Worklist = await workflow.GetNursingTaskWorklistAsync();
        DueTasks.Clear();
        if (Worklist is not null)
        {
            foreach (var t in Worklist.Tasks.Where(t => t.Status == NursingTaskStatus.Due || t.Status == NursingTaskStatus.Overdue))
                DueTasks.Add(t);
        }
    }

    [RelayCommand]
    private async Task RefreshWorklist()
    {
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            Worklist = await workflow.RefreshNursingTaskWorklistAsync();
            DueTasks.Clear();
            if (Worklist is not null)
                foreach (var t in Worklist.Tasks.Where(t => t.Status == NursingTaskStatus.Due || t.Status == NursingTaskStatus.Overdue))
                    DueTasks.Add(t);
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task CompleteTask(NursingTask task)
    {
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.CompleteNursingTaskAsync(task.TaskId, "NURSE-UI", "Nurse", null);
            ActionMessage = "Task completed.";
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task DeferTask(NursingTask task)
    {
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.DeferNursingTaskAsync(task.TaskId, "Deferred via WPF");
            ActionMessage = "Task deferred.";
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}
