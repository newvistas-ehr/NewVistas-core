// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.ViewModels;
using NSubstitute;

namespace NewVistas.Wpf_UI_Tests;

[TestFixture]
public class PharmacyBenefitsViewModelTests : ViewModelTestBase
{
    private PharmacyBenefitsViewModel _vm = null!;
    private IPatientBenefitPlanGrain _mockPlan = null!;
    private IPriorAuthIndexGrain _mockPaIndex = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _mockPlan = Substitute.For<IPatientBenefitPlanGrain>();
        _mockPaIndex = Substitute.For<IPriorAuthIndexGrain>();
        MockGrainFactory.GetGrain<IPatientBenefitPlanGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_mockPlan);
        MockGrainFactory.GetGrain<IPriorAuthIndexGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_mockPaIndex);
        _vm = new PharmacyBenefitsViewModel(GrainService, ApiClient, PatientContext);
    }

    [Test]
    public async Task LoadDataAsync_LoadsBenefitPlan()
    {
        SelectPatient("PAT-001");
        _mockPlan.GetPlanAsync().Returns(Task.FromResult(new PatientBenefitPlanState { PlanName = "VA Plan" }));
        _mockPaIndex.GetAllAsync().Returns(Task.FromResult(new List<PriorAuthIndexEntry>()));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.BenefitPlan, Is.Not.Null);
        Assert.That(_vm.BenefitPlan!.PlanName, Is.EqualTo("VA Plan"));
    }

    [Test]
    public async Task LoadDataAsync_LoadsPriorAuths()
    {
        SelectPatient("PAT-001");
        _mockPlan.GetPlanAsync().Returns(Task.FromResult(new PatientBenefitPlanState()));
        _mockPaIndex.GetAllAsync().Returns(Task.FromResult(new List<PriorAuthIndexEntry> { new() }));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.PriorAuths, Has.Count.EqualTo(1));
    }

    [Test]
    public void TogglePaForm_TogglesVisibility()
    {
        Assert.That(_vm.ShowPaForm, Is.False);
        _vm.TogglePaFormCommand.Execute(null);
        Assert.That(_vm.ShowPaForm, Is.True);
        _vm.TogglePaFormCommand.Execute(null);
        Assert.That(_vm.ShowPaForm, Is.False);
    }
}
