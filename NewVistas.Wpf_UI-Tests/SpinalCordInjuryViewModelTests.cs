// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.ViewModels;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace NewVistas.Wpf_UI_Tests;

[TestFixture]
public class SpinalCordInjuryViewModelTests : ViewModelTestBase
{
    private SpinalCordInjuryViewModel _vm = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _vm = new SpinalCordInjuryViewModel(GrainService, ApiClient, PatientContext);
    }

    [Test]
    public async Task LoadAsync_PopulatesCollection()
    {
        SelectPatient("PAT-001");
        MockWorkflowGrain.GetSCIPatientAsync()
            .Returns(new SCIPatientState());
        MockWorkflowGrain.GetSCIAnnualEncountersAsync()
            .Returns(new List<SCIAnnualEncounterRecord> { new() { FiscalYear = 2026 } });

        await _vm.LoadAsync();

        Assert.That(_vm.Patient, Is.Not.Null);
        Assert.That(_vm.Encounters, Has.Count.EqualTo(1));
        Assert.That(_vm.Error, Is.Null);
    }

    [Test]
    public async Task LoadAsync_SetsErrorOnFailure()
    {
        SelectPatient("PAT-001");
        MockWorkflowGrain.GetSCIPatientAsync()
            .ThrowsAsync(new Exception("SCI registry error"));

        await _vm.LoadAsync();

        Assert.That(_vm.Error, Is.EqualTo("SCI registry error"));
    }

    [Test]
    public async Task LoadAsync_RequiresPatient()
    {
        await _vm.LoadAsync();

        Assert.That(_vm.Patient, Is.Null);
        Assert.That(_vm.Encounters, Has.Count.EqualTo(0));
        Assert.That(_vm.Error, Is.Null);
    }
}
