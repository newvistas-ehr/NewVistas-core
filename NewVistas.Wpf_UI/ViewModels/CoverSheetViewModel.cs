// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.Services;

namespace NewVistas.Wpf_UI.ViewModels;

public partial class CoverSheetViewModel : BasePatientViewModel
{
    [ObservableProperty] private CoverSheetState? _coverSheet;
    [ObservableProperty] private string? _cwadDisplay;

    public CoverSheetViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        CoverSheet = await workflow.GetCoverSheetAsync();
        var flags = CoverSheet?.Cwad.ToString();
        CwadDisplay = string.IsNullOrEmpty(flags) ? null : flags;
    }

    // ── Workflow Navigation Commands ────────────────────────────────────────
    // These set a pending action on PatientContext then navigate to the target page.
    // The target VM (e.g., OrdersViewModel) reads and clears the pending action.

    [RelayCommand]
    private void NewOrder()
        => PatientContext.RequestNavigation("Orders", "NEW_ORDER");

    [RelayCommand]
    private void NewMedOrder()
        => PatientContext.RequestNavigation("Orders", "NEW_ORDER:PHARMACY");

    [RelayCommand]
    private void NewLabOrder()
        => PatientContext.RequestNavigation("Labs", "NEW_ORDER:LAB");

    [RelayCommand]
    private void NewConsult()
        => PatientContext.RequestNavigation("Consults", "NEW_ORDER:CONSULT");
}
