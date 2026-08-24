// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
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
        _vm = new OutpatientPharmacyViewModel(GrainService, PatientContext);
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

    [Test]
    public void SelectingNewEntry_ClearsDetailImmediately_WhileFetchInFlight()
    {
        SelectPatient("PAT-001");

        // RX-A resolves synchronously so a detail record is loaded.
        var mockRxA = Substitute.For<IPharmacyGrain>();
        mockRxA.GetPrescriptionAsync().Returns(Task.FromResult(new PharmacyState { PrescriptionId = "RX-A" }));
        mockRxA.GetRefillHistoryAsync().Returns(Task.FromResult(new List<RefillRecord>()));
        MockGrainFactory.GetGrain<IPharmacyGrain>("RX-A", Arg.Any<string?>()).Returns(mockRxA);

        // RX-B's fetch is held open by a TCS to simulate a slow grain call.
        var tcs = new TaskCompletionSource<PharmacyState>();
        var mockRxB = Substitute.For<IPharmacyGrain>();
        mockRxB.GetPrescriptionAsync().Returns(tcs.Task);
        mockRxB.GetRefillHistoryAsync().Returns(Task.FromResult(new List<RefillRecord>()));
        MockGrainFactory.GetGrain<IPharmacyGrain>("RX-B", Arg.Any<string?>()).Returns(mockRxB);

        _vm.SelectedEntry = new PrescriptionIndexEntry { PrescriptionId = "RX-A" };
        Assert.That(_vm.SelectedPrescription?.PrescriptionId, Is.EqualTo("RX-A"));

        // Select row B: its fetch has not completed, so immediately after the
        // setter returns the stale RX-A detail must already be gone — actions
        // gate on SelectedPrescription, so they can never target RX-A here.
        _vm.SelectedEntry = new PrescriptionIndexEntry { PrescriptionId = "RX-B" };
        Assert.That(_vm.SelectedPrescription, Is.Null);

        // Completing the fetch lands the correct record.
        tcs.SetResult(new PharmacyState { PrescriptionId = "RX-B" });
        Assert.That(_vm.SelectedPrescription, Is.Not.Null);
        Assert.That(_vm.SelectedPrescription!.PrescriptionId, Is.EqualTo("RX-B"));
    }

    [Test]
    public void ClearingSelection_ClearsDetail()
    {
        SelectPatient("PAT-001");
        var mockRx = Substitute.For<IPharmacyGrain>();
        mockRx.GetPrescriptionAsync().Returns(Task.FromResult(new PharmacyState { PrescriptionId = "RX1" }));
        mockRx.GetRefillHistoryAsync().Returns(Task.FromResult(new List<RefillRecord>()));
        MockGrainFactory.GetGrain<IPharmacyGrain>("RX1", Arg.Any<string?>()).Returns(mockRx);

        _vm.SelectedEntry = new PrescriptionIndexEntry { PrescriptionId = "RX1" };
        Assert.That(_vm.SelectedPrescription, Is.Not.Null);

        _vm.SelectedEntry = null;
        Assert.That(_vm.SelectedPrescription, Is.Null);
    }
}
