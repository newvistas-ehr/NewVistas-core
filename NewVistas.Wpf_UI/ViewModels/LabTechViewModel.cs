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

public partial class LabTechViewModel : BasePatientViewModel
{
    [ObservableProperty] private LabWorklistState? _worklist;
    [ObservableProperty] private ObservableCollection<LabAccessionIndexEntry> _accessions = new();
    [ObservableProperty] private string _locationId = "LAB-MAIN";
    [ObservableProperty] private string? _actionMessage;

    // Accession fields
    [ObservableProperty] private string _testIds = string.Empty;
    [ObservableProperty] private string _specimenType = "Blood";
    [ObservableProperty] private string _tubeType = "LAVENDER";
    [ObservableProperty] private string _techId = string.Empty;

    public LabTechViewModel(OrleansGrainService grains, PatientContext patientContext)
        : base(grains, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        Worklist = await workflow.RefreshLabWorklistAsync(LocationId);
        List<LabAccessionIndexEntry> list = await workflow.GetAccessionsAsync();
        Accessions.Clear();
        foreach (var a in list) Accessions.Add(a);
    }

    [RelayCommand]
    private async Task AccessionSpecimen()
    {
        if (string.IsNullOrWhiteSpace(TestIds)) return;
        IsLoading = true; Error = null;
        try
        {
            List<string> ids = TestIds.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            string num = await workflow.AccessionSpecimenAsync(ids, SpecimenType, TubeType, DateTime.UtcNow, "CHEM", TechId, "Tech", null);
            ActionMessage = $"Accessioned: {num}";
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
