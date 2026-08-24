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

public partial class DieteticsViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<DieteticsSummary> _dietOrders = new();
    [ObservableProperty] private DieteticsSummary? _selectedOrder;

    // Create form
    [ObservableProperty] private bool _showCreateForm;
    [ObservableProperty] private string _dietType = "REGULAR";
    [ObservableProperty] private string _texture = string.Empty;
    [ObservableProperty] private string _fluidConsistency = string.Empty;
    [ObservableProperty] private string _calorieLevel = string.Empty;
    [ObservableProperty] private string _specialInstructions = string.Empty;
    [ObservableProperty] private string _providerName = "Provider, Test";

    public string[] DietTypes { get; } = [
        "REGULAR", "CARDIAC", "RENAL", "DIABETIC",
        "CLEAR LIQUID", "FULL LIQUID", "NPO", "LOW SODIUM", "LOW FAT",
        "HIGH PROTEIN", "VEGETARIAN", "PUREE"
    ];
    public string[] TextureOptions { get; } = ["REGULAR", "SOFT", "MINCED", "PUREED", "LIQUID"];
    public string[] FluidOptions { get; } = ["THIN", "NECTAR THICK", "HONEY THICK", "SPOON THICK"];

    public DieteticsViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var list = await workflow.GetDietOrdersAsync();
        DietOrders.Clear();
        foreach (var d in list) DietOrders.Add(d);
    }

    [RelayCommand]
    private void ToggleCreateForm() => ShowCreateForm = !ShowCreateForm;

    [RelayCommand]
    private async Task CreateDietOrder()
    {
        if (!HasPatient) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.CreateDietOrderAsync(
                DietType,
                null, // currentDiet
                null, // modifications
                Texture.Length > 0 ? Texture : null,
                FluidConsistency.Length > 0 ? FluidConsistency : null,
                CalorieLevel.Length > 0 ? CalorieLevel : null,
                SpecialInstructions.Length > 0 ? SpecialInstructions : null,
                DateTime.UtcNow,
                null, // providerId
                ProviderName,
                null); // comments
            ShowCreateForm = false;
            SpecialInstructions = string.Empty;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task DiscontinueDietOrder()
    {
        if (SelectedOrder is null || !HasPatient) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.DiscontinueDietOrderAsync(SelectedOrder.DietOrderId);
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
