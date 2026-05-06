// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.ViewModels;
using NSubstitute;

namespace NewVistas.Wpf_UI_Tests;

[TestFixture]
public class AdtViewModelTests : ViewModelTestBase
{
    private AdtViewModel _vm = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _vm = new AdtViewModel(GrainService, ApiClient, PatientContext);
    }

    [Test]
    public async Task LoadDataAsync_LoadsMovements()
    {
        SelectPatient("PAT-001");
        var movements = new List<AdtSummary> { new() { MovementId = "M1" } };
        MockWorkflowGrain.GetAdtMovementsAsync().Returns(Task.FromResult(movements));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.Movements, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task RecordAdmission_CallsWorkflowGrain()
    {
        SelectPatient("PAT-001");
        MockWorkflowGrain.RecordAdmissionAsync(
            Arg.Any<DateTime>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(Task.FromResult("M-NEW"));
        MockWorkflowGrain.GetAdtMovementsAsync().Returns(Task.FromResult(new List<AdtSummary>()));

        _vm.WardLocationName = "ICU";
        await _vm.RecordAdmissionCommand.ExecuteAsync(null);

        await MockWorkflowGrain.Received(1).RecordAdmissionAsync(
            Arg.Any<DateTime>(), Arg.Any<string?>(), "ICU",
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Test]
    public void HasPatient_ReturnsFalse_WhenNoPatient()
    {
        Assert.That(_vm.LoadCommand.CanExecute(null), Is.False);
    }
}
