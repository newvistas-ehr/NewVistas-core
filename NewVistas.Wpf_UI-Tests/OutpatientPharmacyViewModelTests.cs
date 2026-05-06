// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Wpf_UI.ViewModels;
using NSubstitute;

namespace NewVistas.Wpf_UI_Tests;

[TestFixture]
public class OutpatientPharmacyViewModelTests : ViewModelTestBase
{
    private OutpatientPharmacyViewModel _vm = null!;
    private IPatientPrescriptionIndexGrain _mockIndex = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _mockIndex = Substitute.For<IPatientPrescriptionIndexGrain>();
        MockGrainFactory.GetGrain<IPatientPrescriptionIndexGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(_mockIndex);
        _vm = new OutpatientPharmacyViewModel(GrainService, ApiClient, PatientContext);
    }

    [Test]
    public async Task LoadDataAsync_LoadsPrescriptions()
    {
        SelectPatient("PAT-001");
        var list = new List<PrescriptionIndexEntry> { new() { PrescriptionId = "RX1" } };
        _mockIndex.GetAllAsync().Returns(Task.FromResult(list));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.Prescriptions, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task SelectPrescription_LoadsDetailAndRefillHistory()
    {
        SelectPatient("PAT-001");
        var entry = new PrescriptionIndexEntry { PrescriptionId = "RX1" };
        var mockRx = Substitute.For<IPharmacyGrain>();
        var rxState = new PharmacyState { PrescriptionId = "RX1" };
        mockRx.GetPrescriptionAsync().Returns(Task.FromResult(rxState));
        mockRx.GetRefillHistoryAsync().Returns(Task.FromResult(new List<RefillRecord>()));
        MockGrainFactory.GetGrain<IPharmacyGrain>("RX1", Arg.Any<string?>()).Returns(mockRx);

        await _vm.SelectPrescriptionCommand.ExecuteAsync(entry);

        Assert.That(_vm.SelectedPrescription, Is.Not.Null);
        Assert.That(_vm.SelectedPrescription!.PrescriptionId, Is.EqualTo("RX1"));
    }

    [Test]
    public void CannotLoad_WithoutPatient()
    {
        Assert.That(_vm.LoadCommand.CanExecute(null), Is.False);
    }
}
