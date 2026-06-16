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

public partial class RemindersViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<ReminderSummary> _reminders = new();
    [ObservableProperty] private ReminderSummary? _selectedReminder;

    // Create form
    [ObservableProperty] private bool _showCreateForm;
    [ObservableProperty] private string _reminderName = string.Empty;
    [ObservableProperty] private string _category = string.Empty;
    [ObservableProperty] private string _priority = "ROUTINE";
    [ObservableProperty] private DateTime? _dueDate;

    public string[] PriorityOptions { get; } = ["URGENT", "ROUTINE", "LOW"];

    public RemindersViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var list = await workflow.GetRemindersAsync();
        Reminders.Clear();
        foreach (var r in list) Reminders.Add(r);
    }

    [RelayCommand]
    private void ToggleCreateForm() => ShowCreateForm = !ShowCreateForm;

    [RelayCommand]
    private async Task CreateReminder()
    {
        if (!HasPatient || string.IsNullOrWhiteSpace(ReminderName)) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.CreateReminderAsync(
                ReminderName,
                null, // reminderDefinitionId
                Category.Length > 0 ? Category : null,
                Priority,
                null, // frequency
                DueDate);
            ShowCreateForm = false;
            ReminderName = string.Empty;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task CompleteReminder()
    {
        if (SelectedReminder is null || !HasPatient) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.CompleteReminderAsync(
                SelectedReminder.ReminderId,
                null, "Provider, Test");
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
