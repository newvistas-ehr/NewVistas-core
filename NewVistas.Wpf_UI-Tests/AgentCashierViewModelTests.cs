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
public class AgentCashierViewModelTests : ViewModelTestBase
{
    private ICashierReceiptIndexGrain _mockReceiptIndex = null!;
    private ICashierReceiptGrain _mockReceipt = null!;
    private ICashierSessionIndexGrain _mockSessionIndex = null!;
    private ICashierSessionGrain _mockSession = null!;

    [SetUp]
    public override void Setup()
    {
        base.Setup();
        _mockReceiptIndex = Substitute.For<ICashierReceiptIndexGrain>();
        _mockReceipt = Substitute.For<ICashierReceiptGrain>();
        _mockSessionIndex = Substitute.For<ICashierSessionIndexGrain>();
        _mockSession = Substitute.For<ICashierSessionGrain>();

        MockGrainFactory.GetGrain<ICashierReceiptIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockReceiptIndex);
        MockGrainFactory.GetGrain<ICashierReceiptGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockReceipt);
        MockGrainFactory.GetGrain<ICashierSessionIndexGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockSessionIndex);
        MockGrainFactory.GetGrain<ICashierSessionGrain>(Arg.Any<string>(), Arg.Any<string?>()).Returns(_mockSession);
    }

    [Test]
    public async Task LoadPatientReceipts_PopulatesReceipts()
    {
        _mockReceiptIndex.GetAllAsync().Returns(new List<CashierReceiptIndexEntry>
        {
            new() { ReceiptId = "R1", ReceiptNumber = "001" }
        });

        var vm = new AgentCashierViewModel(ApiClient, GrainService) { PatientId = "P1" };
        await vm.LoadPatientReceiptsCommand.ExecuteAsync(null);

        Assert.That(vm.PatientReceipts, Has.Count.EqualTo(1));
        Assert.That(vm.PatientLoaded, Is.True);
    }

    [Test]
    public async Task IssueReceipt_CallsGrain()
    {
        _mockReceiptIndex.GetAllAsync().Returns(new List<CashierReceiptIndexEntry>());

        var vm = new AgentCashierViewModel(ApiClient, GrainService)
        {
            ReceiptNumber = "R001",
            ReceiptPatientId = "P1",
            ReceiptPatientName = "Test Patient",
            ReceiptArAccountId = "AR1",
            ReceiptAmount = 100m,
            ReceiptCashierId = "C1",
            ReceiptCashierName = "Cashier",
            ReceiptSessionId = "S1"
        };
        await vm.IssueReceiptCommand.ExecuteAsync(null);

        await _mockReceipt.Received().IssueAsync(
            "R001", "P1", "Test Patient", "AR1", 100m,
            CashierPaymentMethod.Cash, "C1", "Cashier", "S1", null, null);
    }

    [Test]
    public async Task OpenSession_CallsSessionGrain()
    {
        _mockSessionIndex.GetOpenSessionsAsync().Returns(new List<CashierSessionIndexEntry>());
        _mockSessionIndex.GetAllAsync().Returns(new List<CashierSessionIndexEntry>());

        var vm = new AgentCashierViewModel(ApiClient, GrainService)
        {
            StationId = "STA1",
            StationName = "Station 1",
            CashierId = "C1",
            CashierName = "Test Cashier",
            OpeningBalance = 200m
        };
        await vm.OpenSessionCommand.ExecuteAsync(null);

        await _mockSession.Received().OpenAsync("STA1", "Station 1", "C1", "Test Cashier",
            Arg.Any<DateTime>(), 200m);
    }
}
