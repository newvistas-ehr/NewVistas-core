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
public class InpatientPharmacyViewModelTests : ViewModelTestBase
{
    private InpatientPharmacyViewModel _vm = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _vm = new InpatientPharmacyViewModel(GrainService, PatientContext);
    }

    [Test]
    public async Task LoadDataAsync_LoadsMAR()
    {
        SelectPatient("PAT-001");
        var mar = new List<MarEntry> { new() { OrderId = "O1" } };
        MockWorkflowGrain.GetPatientMARAsync().Returns(Task.FromResult(mar));

        await _vm.LoadCommand.ExecuteAsync(null);

        Assert.That(_vm.MarEntries, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task SelectOrder_LoadsOrderDetail()
    {
        SelectPatient("PAT-001");
        var entry = new MarEntry { OrderId = "PSJ-ORDER-001" };
        var mockOrder = Substitute.For<IInpatientOrderGrain>();
        var orderState = new InpatientOrderState { OrderId = "PSJ-ORDER-001" };
        mockOrder.GetOrderAsync().Returns(Task.FromResult(orderState));
        MockGrainFactory.GetGrain<IInpatientOrderGrain>("PSJ-ORDER-001", Arg.Any<string?>())
            .Returns(mockOrder);

        await _vm.SelectOrderCommand.ExecuteAsync(entry);

        Assert.That(_vm.SelectedOrder, Is.Not.Null);
        Assert.That(_vm.SelectedOrder!.OrderId, Is.EqualTo("PSJ-ORDER-001"));
    }

    [Test]
    public async Task PlaceInpatientOrder_CreatesOrder()
    {
        SelectPatient("PAT-001");
        var mockOrder = Substitute.For<IInpatientOrderGrain>();
        MockGrainFactory.GetGrain<IInpatientOrderGrain>(Arg.Any<string>(), Arg.Any<string?>())
            .Returns(mockOrder);
        MockWorkflowGrain.GetPatientMARAsync().Returns(Task.FromResult(new List<MarEntry>()));

        _vm.DrugName = "Metoprolol";
        await _vm.PlaceInpatientOrderCommand.ExecuteAsync(null);

        await mockOrder.Received(1).CreateOrderAsync(
            "PAT-001", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
            "UNIT_DOSE", "Metoprolol", Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<string?>(),
            "PO", "QD", "ROUTINE",
            Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<int?>(), Arg.Any<int?>(),
            Arg.Any<string?>(), "Provider, Test", Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<int?>(), Arg.Any<string?>());
    }
}
