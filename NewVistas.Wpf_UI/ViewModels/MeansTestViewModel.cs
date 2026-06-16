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

public partial class MeansTestViewModel : BasePatientViewModel
{
    [ObservableProperty] private ObservableCollection<MeansTestSummary> _meansTests = new();

    // Record form
    [ObservableProperty] private bool _showRecordForm;
    [ObservableProperty] private string _testType = "ANNUAL";
    [ObservableProperty] private decimal? _annualIncome;
    [ObservableProperty] private decimal? _netWorth;
    [ObservableProperty] private int? _numberOfDependents;
    [ObservableProperty] private string _eligibilityStatus = string.Empty;
    [ObservableProperty] private string _priorityGroup = string.Empty;
    [ObservableProperty] private string _completedByName = "Staff, Test";

    public string[] TestTypes { get; } = ["ANNUAL", "HARDSHIP", "VETERAN REQUESTED", "ADMINISTRATIVE"];
    public string[] EligibilityOptions { get; } = [
        "EXEMPT FROM COPAY", "COPAY REQUIRED", "HARDSHIP WAIVER", "PENDING"
    ];
    public string[] PriorityGroups { get; } = ["1", "2", "3", "4", "5", "6", "7", "8"];

    public MeansTestViewModel(OrleansGrainService grains, ApiClient api, PatientContext patientContext)
        : base(grains, api, patientContext) { }

    protected override async Task LoadDataAsync()
    {
        var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
        var list = await workflow.GetMeansTestsAsync();
        MeansTests.Clear();
        foreach (var m in list) MeansTests.Add(m);
    }

    [RelayCommand]
    private void ToggleRecordForm() => ShowRecordForm = !ShowRecordForm;

    [RelayCommand]
    private async Task RecordMeansTest()
    {
        if (!HasPatient) return;
        IsLoading = true; Error = null;
        try
        {
            var workflow = Grains.GetGrain<IPatientWorkflowGrain>(PatientId);
            await workflow.RecordMeansTestAsync(
                TestType,
                DateTime.UtcNow,
                AnnualIncome,
                NetWorth,
                NumberOfDependents,
                EligibilityStatus.Length > 0 ? EligibilityStatus : null,
                PriorityGroup.Length > 0 ? PriorityGroup : null,
                null, // completedById
                CompletedByName,
                null); // comments
            ShowRecordForm = false;
            AnnualIncome = null; NetWorth = null; NumberOfDependents = null;
            await LoadDataAsync();
        }
        catch (Exception ex) { Error = ex.Message; }
        finally { IsLoading = false; }
    }
}
