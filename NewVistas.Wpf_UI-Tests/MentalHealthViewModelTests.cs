// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.ViewModels;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace NewVistas.Wpf_UI_Tests;

[TestFixture]
public class MentalHealthViewModelTests : ViewModelTestBase
{
    private MentalHealthViewModel _vm = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _vm = new MentalHealthViewModel(GrainService, ApiClient, PatientContext);
    }

    [Test]
    public async Task LoadAsync_PopulatesCollection()
    {
        SelectPatient("PAT-001");
        MockWorkflowGrain.GetMentalHealthScreensAsync()
            .Returns(new List<MentalHealthSummary> { new() { InstrumentName = "PHQ-9" } });

        await _vm.LoadAsync();

        Assert.That(_vm.Screens, Has.Count.EqualTo(1));
        Assert.That(_vm.Screens[0].InstrumentName, Is.EqualTo("PHQ-9"));
        Assert.That(_vm.Error, Is.Null);
    }

    [Test]
    public async Task LoadAsync_SetsErrorOnFailure()
    {
        SelectPatient("PAT-001");
        MockWorkflowGrain.GetMentalHealthScreensAsync()
            .ThrowsAsync(new Exception("Grain unavailable"));

        await _vm.LoadAsync();

        Assert.That(_vm.Error, Is.EqualTo("Grain unavailable"));
    }

    [Test]
    public async Task LoadAsync_RequiresPatient()
    {
        await _vm.LoadAsync();

        Assert.That(_vm.Screens, Has.Count.EqualTo(0));
        Assert.That(_vm.Error, Is.Null);
    }
}
