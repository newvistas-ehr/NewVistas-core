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

/// <summary>
/// Patient Portal — health submissions, secure messages, and review queue.
/// Mirrors the Blazor PatientPortal.razor page.
/// </summary>
public partial class PatientPortalViewModel : BasePatientViewModel
{
    [ObservableProperty] private string _activeTab = "submissions";
    [ObservableProperty] private ObservableCollection<PatientSubmissionSummary> _submissions = new();
    [ObservableProperty] private ObservableCollection<SecureMessageThreadSummary> _threads = new();
    [ObservableProperty] private ObservableCollection<PatientSubmissionSummary> _queueItems = new();

    public bool IsSubmissionsTab => ActiveTab == "submissions";
    public bool IsMessagesTab => ActiveTab == "messages";
    public bool IsQueueTab => ActiveTab == "queue";

    public PatientPortalViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext) { }

    partial void OnActiveTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsSubmissionsTab));
        OnPropertyChanged(nameof(IsMessagesTab));
        OnPropertyChanged(nameof(IsQueueTab));
    }

    protected override async Task LoadDataAsync()
    {
        var subIndex = Grains.GetGrain<IPatientSubmissionIndexGrain>($"PATIENT-SUB-IDX:{PatientId}");
        var subs = await subIndex.GetAllSubmissionsAsync();
        Submissions = new(subs);

        var msgIndex = Grains.GetGrain<ISecureMessageIndexGrain>($"SECURE-MSG-IDX:{PatientId}");
        var threads = await msgIndex.GetAllThreadsAsync();
        Threads = new(threads);
    }

    [RelayCommand]
    private void ShowSubmissions() => ActiveTab = "submissions";

    [RelayCommand]
    private void ShowMessages() => ActiveTab = "messages";

    [RelayCommand]
    private void ShowQueue() => ActiveTab = "queue";

    [RelayCommand]
    private async Task LoadQueueAsync()
    {
        try
        {
            var queue = Grains.GetGrain<IPatientSubmissionQueueGrain>("PATIENT-SUB-QUEUE");
            var items = await queue.GetPendingSubmissionsAsync();
            QueueItems = new(items);
        }
        catch (Exception ex) { Error = ex.Message; }
    }
}
