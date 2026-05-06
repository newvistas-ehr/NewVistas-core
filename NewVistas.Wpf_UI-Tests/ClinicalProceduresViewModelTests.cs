// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.ViewModels;
using NSubstitute;

namespace NewVistas.Wpf_UI_Tests;

[TestFixture]
public class ClinicalProceduresViewModelTests : ViewModelTestBase
{
    private ClinicalProceduresViewModel _vm = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _vm = new ClinicalProceduresViewModel(GrainService, ApiClient, PatientContext);
    }

    [Test]
    public async Task LoadDataAsync_LoadsProcedures()
    {
        SelectPatient("PAT-001");
        var procs = new List<ClinicProcedureIndexEntry> { new() { ProcedureId = "P1" } };
        MockWorkflowGrain.GetClinicProceduresAsync().Returns(Task.FromResult(procs));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.Procedures, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task LoadDataAsync_ClearsPreviousProcedures()
    {
        SelectPatient("PAT-001");
        MockWorkflowGrain.GetClinicProceduresAsync().Returns(Task.FromResult(new List<ClinicProcedureIndexEntry> { new() }));
        await _vm.LoadCommand.ExecuteAsync(null);

        MockWorkflowGrain.GetClinicProceduresAsync().Returns(Task.FromResult(new List<ClinicProcedureIndexEntry>()));
        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.Procedures, Has.Count.EqualTo(0));
    }

    [Test]
    public void CannotLoad_WithoutPatient()
    {
        Assert.That(_vm.LoadCommand.CanExecute(null), Is.False);
    }
}
