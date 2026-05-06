// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.ViewModels;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace NewVistas.Wpf_UI_Tests;

[TestFixture]
public class ServiceConnectedViewModelTests : ViewModelTestBase
{
    private ServiceConnectedViewModel _vm = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _vm = new ServiceConnectedViewModel(GrainService, ApiClient, PatientContext);
    }

    [Test]
    public async Task LoadAsync_PopulatesCollection()
    {
        SelectPatient("PAT-001");
        MockWorkflowGrain.GetServiceConnectedConditionsAsync()
            .Returns(new List<ServiceConnectedSummary> { new() { Condition = "PTSD" } });

        await _vm.LoadAsync();

        Assert.That(_vm.Conditions, Has.Count.EqualTo(1));
        Assert.That(_vm.Conditions[0].Condition, Is.EqualTo("PTSD"));
        Assert.That(_vm.Error, Is.Null);
    }

    [Test]
    public async Task LoadAsync_SetsErrorOnFailure()
    {
        SelectPatient("PAT-001");
        MockWorkflowGrain.GetServiceConnectedConditionsAsync()
            .ThrowsAsync(new Exception("Service unavailable"));

        await _vm.LoadAsync();

        Assert.That(_vm.Error, Is.EqualTo("Service unavailable"));
    }

    [Test]
    public async Task LoadAsync_RequiresPatient()
    {
        await _vm.LoadAsync();

        Assert.That(_vm.Conditions, Has.Count.EqualTo(0));
        Assert.That(_vm.Error, Is.Null);
    }
}
