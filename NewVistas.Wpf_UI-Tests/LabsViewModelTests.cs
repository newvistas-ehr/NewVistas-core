// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.ViewModels;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace NewVistas.Wpf_UI_Tests;

[TestFixture]
public class LabsViewModelTests : ViewModelTestBase
{
    [Test]
    public async Task LoadAsync_PopulatesLabResultsAndSummary()
    {
        var results = new List<LabResultSummary>
        {
            new() { LabTestId = "L1", TestName = "CBC" },
            new() { LabTestId = "L2", TestName = "BMP" }
        };
        var summary = new List<LabTestSummaryEntry>
        {
            new() { TestName = "CBC", LoincCode = "58410-2" }
        };
        MockWorkflowGrain.GetLabResultsAsync().Returns(results);
        MockWorkflowGrain.GetLabSummaryAsync().Returns(summary);
        SelectPatient("PATIENT-001");
        var vm = new LabsViewModel(GrainService, ApiClient, PatientContext);

        await vm.LoadAsync();

        Assert.That(vm.LabResults, Has.Count.EqualTo(2));
        Assert.That(vm.LabSummary, Has.Count.EqualTo(1));
        Assert.That(vm.IsLoading, Is.False);
        Assert.That(vm.Error, Is.Null);
    }

    [Test]
    public async Task LoadAsync_SetsErrorOnFailure()
    {
        MockWorkflowGrain.GetLabResultsAsync().Throws(new Exception("Grain error"));
        SelectPatient("PATIENT-001");
        var vm = new LabsViewModel(GrainService, ApiClient, PatientContext);

        await vm.LoadAsync();

        Assert.That(vm.Error, Is.Not.Null);
        Assert.That(vm.IsLoading, Is.False);
    }

    [Test]
    public void LoadAsync_RequiresPatient()
    {
        var vm = new LabsViewModel(GrainService, ApiClient, PatientContext);
        Assert.That(vm.LoadCommand.CanExecute(null), Is.False);
    }
}
