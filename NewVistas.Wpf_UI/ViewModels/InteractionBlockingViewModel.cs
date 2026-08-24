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

public partial class InteractionBlockingViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<InteractionScreeningIndexEntry> _screenings = new();
    [ObservableProperty] private ObservableCollection<InteractionScreeningIndexEntry> _blockedScreenings = new();
    [ObservableProperty] private InteractionScreeningState? _selectedScreening;
    [ObservableProperty] private string? _actionMessage;

    // Screen form
    [ObservableProperty] private string _screenPrescriptionId = string.Empty;
    [ObservableProperty] private string _screenDrugName = string.Empty;
    [ObservableProperty] private string _newIngredientIen = string.Empty;
    [ObservableProperty] private string _newIngredientName = string.Empty;

    public InteractionBlockingViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var wf = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        List<InteractionScreeningIndexEntry> all = await wf.GetInteractionScreeningsAsync();
        Screenings.Clear();
        BlockedScreenings.Clear();
        foreach (var s in all)
        {
            Screenings.Add(s);
            if (s.Status == InteractionScreeningStatus.BlockedPendingOverride)
                BlockedScreenings.Add(s);
        }
        SelectedScreening = null;
    }

    [RelayCommand]
    private async Task ViewDetail(InteractionScreeningIndexEntry entry)
    {
        try
        {
            var wf = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            SelectedScreening = await wf.GetInteractionScreeningAsync(entry.ScreeningId);
        }
        catch (Exception ex) { Error = ex.Message; }
    }

    [RelayCommand]
    private async Task ScreenPrescription()
    {
        if (string.IsNullOrWhiteSpace(ScreenPrescriptionId) || string.IsNullOrWhiteSpace(ScreenDrugName)) return;
        IsLoading = true; Error = null;
        try
        {
            List<DrugIngredient> newIngredients = new();
            if (!string.IsNullOrWhiteSpace(NewIngredientIen))
                newIngredients.Add(new DrugIngredient { IngredientIen = NewIngredientIen.Trim(), Name = NewIngredientName.Trim() });

            var wf = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            string id = await wf.ScreenPrescriptionForInteractionsAsync(
                ScreenPrescriptionId.Trim(), ScreenDrugName.Trim(), newIngredients, new List<DrugIngredient>(), null);
            ActionMessage = $"Screening completed: {id}";
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
