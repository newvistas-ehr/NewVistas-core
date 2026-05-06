// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class WomensHealthViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<WomensHealthIndexEntry> _notifications = new();
    [ObservableProperty] private bool _showFollowUpRequired;

    public WomensHealthViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var list = ShowFollowUpRequired
            ? await workflow.GetWomensHealthFollowUpRequiredAsync()
            : await workflow.GetWomensHealthNotificationsAsync();
        Notifications.Clear();
        foreach (var n in list) Notifications.Add(n);
    }
}
