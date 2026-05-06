// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.ViewModels;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace NewVistas.Wpf_UI_Tests;

[TestFixture]
public class CoverSheetViewModelTests : ViewModelTestBase
{
    [Test]
    public async Task LoadAsync_PopulatesCoverSheet()
    {
        var testData = new CoverSheetState
        {
            ActiveProblems = [new ProblemSummary { ProblemId = "P1", Diagnosis = "HTN" }],
            Allergies = [new AllergySummary { AllergyId = "A1", Allergen = "PCN" }]
        };
        MockWorkflowGrain.GetCoverSheetAsync().Returns(testData);
        SelectPatient("PATIENT-001");
        var vm = new CoverSheetViewModel(GrainService, ApiClient, PatientContext);

        await vm.LoadAsync();

        Assert.That(vm.CoverSheet, Is.Not.Null);
        Assert.That(vm.IsLoading, Is.False);
        Assert.That(vm.Error, Is.Null);
    }

    [Test]
    public async Task LoadAsync_SetsErrorOnFailure()
    {
        MockWorkflowGrain.GetCoverSheetAsync().Throws(new Exception("Grain error"));
        SelectPatient("PATIENT-001");
        var vm = new CoverSheetViewModel(GrainService, ApiClient, PatientContext);

        await vm.LoadAsync();

        Assert.That(vm.Error, Is.Not.Null);
        Assert.That(vm.IsLoading, Is.False);
    }

    [Test]
    public void LoadAsync_RequiresPatient()
    {
        var vm = new CoverSheetViewModel(GrainService, ApiClient, PatientContext);
        Assert.That(vm.LoadCommand.CanExecute(null), Is.False);
    }
}
