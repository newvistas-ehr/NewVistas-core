// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.PT.GrainInterfaces;
using NewVistas.PT.Models;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

/// <summary>
/// PT Hub — shows all body groups and which ones have recorded data for the patient.
/// Selecting a body group navigates to PTSessionViewModel for that group.
/// </summary>
public partial class PTHubViewModel : BasePatientViewModel
{
    private readonly IServiceProvider _services;

    [ObservableProperty] private ObservableCollection<BodyGroupItem> _bodyGroups = new();

    /// <summary>
    /// Fired when user selects a body group to navigate to the session view.
    /// MainViewModel subscribes to this to swap the current view.
    /// </summary>
    public event Action<BodyGroup>? BodyGroupSelected;

    /// <summary>Fired when user wants to navigate to the Goals view.</summary>
    public event Action? GoalsRequested;

    /// <summary>Fired when user wants to navigate to the Home Exercises view.</summary>
    public event Action? HomeExercisesRequested;

    /// <summary>Fired when user wants to launch the measurement wizard.</summary>
    public event Action? WizardRequested;

    public PTHubViewModel(OrleansGrainService grains, PatientContext patientContext,
        IServiceProvider services)
        : base(grains, patientContext)
    {
        _services = services;
    }

    protected override async Task LoadDataAsync()
    {
        var grain = Grains.GetGrain<IPTWorkflowGrain>(PatientId);
        var withData = await grain.GetBodyGroupsWithDataAsync();
        var all = BodyGroupDefinitions.GetAllBodyGroups();

        BodyGroups.Clear();
        foreach (var bg in all)
        {
            BodyGroups.Add(new BodyGroupItem(bg, FormatName(bg), withData.Contains(bg)));
        }
    }

    [RelayCommand]
    private void SelectBodyGroup(BodyGroupItem item)
    {
        BodyGroupSelected?.Invoke(item.BodyGroup);
    }

    [RelayCommand]
    private void NavigateToGoals() => GoalsRequested?.Invoke();

    [RelayCommand]
    private void NavigateToHomeExercises() => HomeExercisesRequested?.Invoke();

    [RelayCommand]
    private void StartWizard() => WizardRequested?.Invoke();

    private static string FormatName(BodyGroup bg) => bg switch
    {
        BodyGroup.Cervical => "Cervical (Neck)",
        BodyGroup.ThoracicSpine => "Thoracic Spine",
        BodyGroup.LumbarSpine => "Lumbar Spine",
        BodyGroup.TMJ => "TMJ",
        _ => bg.ToString()
    };
}

public record BodyGroupItem(BodyGroup BodyGroup, string DisplayName, bool HasData);
