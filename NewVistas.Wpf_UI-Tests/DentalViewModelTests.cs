// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.ViewModels;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace NewVistas.Wpf_UI_Tests;

[TestFixture]
public class DentalViewModelTests : ViewModelTestBase
{
    private DentalViewModel _vm = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _vm = new DentalViewModel(GrainService, ApiClient, PatientContext);
    }

    [Test]
    public async Task LoadAsync_PopulatesCollection()
    {
        SelectPatient("PAT-001");
        MockWorkflowGrain.GetDentalPatientAsync()
            .Returns(new DentalPatientState());
        MockWorkflowGrain.GetDentalTreatmentsAsync()
            .Returns(new List<DentalTreatmentIndexEntry> { new() { ProcedureDescription = "Filling" } });

        await _vm.LoadAsync();

        Assert.That(_vm.Patient, Is.Not.Null);
        Assert.That(_vm.Treatments, Has.Count.EqualTo(1));
        Assert.That(_vm.Error, Is.Null);
    }

    [Test]
    public async Task LoadAsync_SetsErrorOnFailure()
    {
        SelectPatient("PAT-001");
        MockWorkflowGrain.GetDentalPatientAsync()
            .ThrowsAsync(new Exception("Dental record error"));

        await _vm.LoadAsync();

        Assert.That(_vm.Error, Is.EqualTo("Dental record error"));
    }

    [Test]
    public async Task LoadAsync_RequiresPatient()
    {
        await _vm.LoadAsync();

        Assert.That(_vm.Patient, Is.Null);
        Assert.That(_vm.Treatments, Has.Count.EqualTo(0));
        Assert.That(_vm.Error, Is.Null);
    }
}
