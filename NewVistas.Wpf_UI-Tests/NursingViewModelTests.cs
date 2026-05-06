// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.ViewModels;
using NSubstitute;

namespace NewVistas.Wpf_UI_Tests;

[TestFixture]
public class NursingViewModelTests : ViewModelTestBase
{
    private NursingViewModel _vm = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _vm = new NursingViewModel(GrainService, ApiClient, PatientContext);
    }

    [Test]
    public async Task LoadDataAsync_LoadsAssessments()
    {
        SelectPatient("PAT-001");
        MockWorkflowGrain.GetNursingAssessmentsAsync()
            .Returns(Task.FromResult(new List<NursingAssessmentIndexEntry> { new() }));
        MockWorkflowGrain.GetNursingCarePlanAsync().Returns(Task.FromResult(new NursingCarePlanState()));
        MockWorkflowGrain.GetNursingAcuityAsync().Returns(Task.FromResult(new NursingAcuityState()));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.Assessments, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task LoadDataAsync_LoadsCarePlanAndAcuity()
    {
        SelectPatient("PAT-001");
        MockWorkflowGrain.GetNursingAssessmentsAsync()
            .Returns(Task.FromResult(new List<NursingAssessmentIndexEntry>()));
        MockWorkflowGrain.GetNursingCarePlanAsync().Returns(Task.FromResult(new NursingCarePlanState()));
        MockWorkflowGrain.GetNursingAcuityAsync().Returns(Task.FromResult(new NursingAcuityState()));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.CarePlan, Is.Not.Null);
        Assert.That(_vm.Acuity, Is.Not.Null);
    }

    [Test]
    public void CannotLoad_WithoutPatient()
    {
        Assert.That(_vm.LoadCommand.CanExecute(null), Is.False);
    }
}
