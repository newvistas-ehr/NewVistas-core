// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.ViewModels;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace NewVistas.Wpf_UI_Tests;

[TestFixture]
public class SchedulingViewModelTests : ViewModelTestBase
{
    private SchedulingViewModel _vm = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _vm = new SchedulingViewModel(GrainService, ApiClient, PatientContext);
    }

    [Test]
    public async Task LoadAsync_PopulatesCollection()
    {
        SelectPatient("PAT-001");
        MockWorkflowGrain.GetAllAppointmentsAsync(50)
            .Returns(new List<AppointmentEntry> { new() { ClinicName = "Primary Care" } });
        MockWorkflowGrain.GetClinicListAsync()
            .Returns(new List<ClinicEntry> { new() { Name = "Primary Care" } });

        await _vm.LoadAsync();

        Assert.That(_vm.Appointments, Has.Count.EqualTo(1));
        Assert.That(_vm.Clinics, Has.Count.EqualTo(1));
        Assert.That(_vm.Error, Is.Null);
    }

    [Test]
    public async Task LoadAsync_SetsErrorOnFailure()
    {
        SelectPatient("PAT-001");
        MockWorkflowGrain.GetAllAppointmentsAsync(50)
            .ThrowsAsync(new Exception("Scheduling error"));

        await _vm.LoadAsync();

        Assert.That(_vm.Error, Is.EqualTo("Scheduling error"));
    }

    [Test]
    public async Task LoadAsync_RequiresPatient()
    {
        await _vm.LoadAsync();

        Assert.That(_vm.Appointments, Has.Count.EqualTo(0));
        Assert.That(_vm.Error, Is.Null);
    }
}
