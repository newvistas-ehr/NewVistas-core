// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.ViewModels;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace NewVistas.Wpf_UI_Tests;

[TestFixture]
public class RadiologyViewModelTests : ViewModelTestBase
{
    [Test]
    public async Task LoadAsync_PopulatesStudies()
    {
        var testData = new List<RadiologySummary>
        {
            new() { RadiologyId = "R1", ProcedureName = "Chest X-Ray", Status = "COMPLETE" }
        };
        MockWorkflowGrain.GetRadiologyStudiesAsync(50).Returns(testData);
        SelectPatient("PATIENT-001");
        var vm = new RadiologyViewModel(GrainService, ApiClient, PatientContext);

        await vm.LoadAsync();

        Assert.That(vm.Studies, Has.Count.EqualTo(1));
        Assert.That(vm.IsLoading, Is.False);
        Assert.That(vm.Error, Is.Null);
    }

    [Test]
    public async Task LoadAsync_SetsErrorOnFailure()
    {
        MockWorkflowGrain.GetRadiologyStudiesAsync(50).Throws(new Exception("Grain error"));
        SelectPatient("PATIENT-001");
        var vm = new RadiologyViewModel(GrainService, ApiClient, PatientContext);

        await vm.LoadAsync();

        Assert.That(vm.Error, Is.Not.Null);
        Assert.That(vm.IsLoading, Is.False);
    }

    [Test]
    public void LoadAsync_RequiresPatient()
    {
        var vm = new RadiologyViewModel(GrainService, ApiClient, PatientContext);
        Assert.That(vm.LoadCommand.CanExecute(null), Is.False);
    }
}
