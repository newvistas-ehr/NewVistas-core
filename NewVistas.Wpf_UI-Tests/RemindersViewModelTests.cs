// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.ViewModels;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace NewVistas.Wpf_UI_Tests;

[TestFixture]
public class RemindersViewModelTests : ViewModelTestBase
{
    [Test]
    public async Task LoadAsync_PopulatesReminders()
    {
        var testData = new List<ReminderSummary>
        {
            new() { ReminderId = "R1", ReminderName = "Flu Shot", Status = "DUE" },
            new() { ReminderId = "R2", ReminderName = "Colonoscopy", Status = "DUE" }
        };
        MockWorkflowGrain.GetRemindersAsync().Returns(testData);
        SelectPatient("PATIENT-001");
        var vm = new RemindersViewModel(GrainService, ApiClient, PatientContext);

        await vm.LoadAsync();

        Assert.That(vm.Reminders, Has.Count.EqualTo(2));
        Assert.That(vm.IsLoading, Is.False);
        Assert.That(vm.Error, Is.Null);
    }

    [Test]
    public async Task LoadAsync_SetsErrorOnFailure()
    {
        MockWorkflowGrain.GetRemindersAsync().Throws(new Exception("Grain error"));
        SelectPatient("PATIENT-001");
        var vm = new RemindersViewModel(GrainService, ApiClient, PatientContext);

        await vm.LoadAsync();

        Assert.That(vm.Error, Is.Not.Null);
        Assert.That(vm.IsLoading, Is.False);
    }

    [Test]
    public void LoadAsync_RequiresPatient()
    {
        var vm = new RemindersViewModel(GrainService, ApiClient, PatientContext);
        Assert.That(vm.LoadCommand.CanExecute(null), Is.False);
    }
}
