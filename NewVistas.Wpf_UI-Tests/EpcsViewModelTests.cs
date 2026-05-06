// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.ViewModels;
using NSubstitute;

namespace NewVistas.Wpf_UI_Tests;

[TestFixture]
public class EpcsViewModelTests : ViewModelTestBase
{
    private EpcsViewModel _vm = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _vm = new EpcsViewModel(GrainService, ApiClient, PatientContext);
    }

    [Test]
    public async Task LoadDataAsync_LoadsPrescriptions()
    {
        SelectPatient("PAT-001");
        var list = new List<EpcsPrescriptionIndexEntry> { new() { EpcsId = "E1" } };
        MockWorkflowGrain.GetEpcsPrescriptionsAsync().Returns(Task.FromResult(list));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.Prescriptions, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task LoadDataAsync_EmptyList()
    {
        SelectPatient("PAT-001");
        MockWorkflowGrain.GetEpcsPrescriptionsAsync().Returns(Task.FromResult(new List<EpcsPrescriptionIndexEntry>()));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.Prescriptions, Has.Count.EqualTo(0));
    }

    [Test]
    public void CannotLoad_WithoutPatient()
    {
        Assert.That(_vm.LoadCommand.CanExecute(null), Is.False);
    }
}
