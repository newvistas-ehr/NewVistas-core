// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.ViewModels;
using NSubstitute;

namespace NewVistas.Wpf_UI_Tests;

[TestFixture]
public class AnatomicPathologyViewModelTests : ViewModelTestBase
{
    private AnatomicPathologyViewModel _vm = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _vm = new AnatomicPathologyViewModel(GrainService, ApiClient, PatientContext);
    }

    [Test]
    public async Task LoadDataAsync_LoadsCases()
    {
        SelectPatient("PAT-001");
        var cases = new List<APCaseIndexEntry> { new() { CaseId = "C1" } };
        MockWorkflowGrain.GetAPCasesAsync().Returns(Task.FromResult(cases));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.Cases, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task SelectCase_LoadsDetail()
    {
        SelectPatient("PAT-001");
        var entry = new APCaseIndexEntry { CaseId = "C1" };
        var detail = new AnatomicPathologyState { CaseId = "C1" };
        MockWorkflowGrain.GetAPCaseAsync("C1").Returns(Task.FromResult(detail));

        await _vm.SelectCaseCommand.ExecuteAsync(entry);

        Assert.That(_vm.CaseDetail, Is.Not.Null);
        Assert.That(_vm.CaseDetail!.CaseId, Is.EqualTo("C1"));
    }

    [Test]
    public void CannotLoad_WithoutPatient()
    {
        Assert.That(_vm.LoadCommand.CanExecute(null), Is.False);
    }
}
