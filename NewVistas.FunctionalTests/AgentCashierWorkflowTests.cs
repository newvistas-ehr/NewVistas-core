// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for VistA Agent Cashier — File #36.
/// Tests session open/close/turn-in lifecycle and receipt issuance with AR cross-grain integration.
/// </summary>
[TestFixture]
public class AgentCashierWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private ICashierSessionGrain NewSession()
        => _cluster.GrainFactory.GetGrain<ICashierSessionGrain>($"CASHIER-SESSION:{Guid.NewGuid()}");

    private ICashierReceiptGrain NewReceipt()
        => _cluster.GrainFactory.GetGrain<ICashierReceiptGrain>($"CASHIER-RECEIPT:{Guid.NewGuid()}");

    private IARAccountGrain NewARAccount()
        => _cluster.GrainFactory.GetGrain<IARAccountGrain>($"AR-ACCOUNT:{Guid.NewGuid()}");

    // ─── Session Lifecycle ───────────────────────────────────────────────

    [Test]
    public async Task OpenSession_SetsOpenStatus()
    {
        ICashierSessionGrain session = NewSession();
        await session.OpenAsync("STA-001", "Main Cashier", "USR-001", "Test Cashier", DateTime.UtcNow.Date, 100m);

        CashierSessionState state = await session.GetAsync();
        Assert.That(state.Status, Is.EqualTo(CashierSessionStatus.Open));
    }

    [Test]
    public async Task OpenSession_ExpectedBalanceEqualsOpeningBalance()
    {
        ICashierSessionGrain session = NewSession();
        await session.OpenAsync("STA-001", "Main Cashier", "USR-001", "Test Cashier", DateTime.UtcNow.Date, 150m);

        CashierSessionState state = await session.GetAsync();
        Assert.That(state.ExpectedBalance, Is.EqualTo(150m));
        Assert.That(state.OpeningBalance, Is.EqualTo(150m));
    }

    [Test]
    public async Task OpenSession_StoresCashierInfo()
    {
        ICashierSessionGrain session = NewSession();
        await session.OpenAsync("STA-002", "North Wing Cashier", "USR-007", "Jane Smith", DateTime.UtcNow.Date, 50m);

        CashierSessionState state = await session.GetAsync();
        Assert.That(state.CashierId, Is.EqualTo("USR-007"));
        Assert.That(state.CashierName, Is.EqualTo("Jane Smith"));
        Assert.That(state.StationId, Is.EqualTo("STA-002"));
    }

    [Test]
    public async Task CloseSession_SetsClosedStatus()
    {
        ICashierSessionGrain session = NewSession();
        await session.OpenAsync("STA-001", "Main Cashier", "USR-001", "Test Cashier", DateTime.UtcNow.Date, 100m);

        await session.CloseAsync(100m, null);

        CashierSessionState state = await session.GetAsync();
        Assert.That(state.Status, Is.EqualTo(CashierSessionStatus.Closed));
    }

    [Test]
    public async Task CloseSession_RecordsActualBalanceAndDiscrepancy()
    {
        ICashierSessionGrain session = NewSession();
        await session.OpenAsync("STA-001", "Main Cashier", "USR-001", "Test Cashier", DateTime.UtcNow.Date, 100m);

        // Add a receipt to increase expected balance to 150
        await session.RecordReceiptAsync($"R-{Guid.NewGuid()}", 50m, "Cash", DateTime.UtcNow);

        // Cashier counts drawer at 145 — 5 short
        await session.CloseAsync(145m, "Counted twice");

        CashierSessionState state = await session.GetAsync();
        Assert.That(state.ActualBalance, Is.EqualTo(145m));
        Assert.That(state.ExpectedBalance, Is.EqualTo(150m));
        Assert.That(state.Discrepancy, Is.EqualTo(5m));
    }

    [Test]
    public async Task TurnInSession_SetsTurnedInStatus()
    {
        ICashierSessionGrain session = NewSession();
        await session.OpenAsync("STA-001", "Main Cashier", "USR-001", "Test Cashier", DateTime.UtcNow.Date, 100m);
        await session.CloseAsync(100m, null);

        await session.TurnInAsync(100m, "USR-FISCAL", "TURNIN-001");

        CashierSessionState state = await session.GetAsync();
        Assert.That(state.Status, Is.EqualTo(CashierSessionStatus.TurnedIn));
    }

    [Test]
    public async Task RecordReceipt_IncreasesExpectedBalance()
    {
        ICashierSessionGrain session = NewSession();
        await session.OpenAsync("STA-001", "Main Cashier", "USR-001", "Test Cashier", DateTime.UtcNow.Date, 100m);

        await session.RecordReceiptAsync($"R-{Guid.NewGuid()}", 30m, "Cash", DateTime.UtcNow);
        await session.RecordReceiptAsync($"R-{Guid.NewGuid()}", 20m, "Check", DateTime.UtcNow);

        CashierSessionState state = await session.GetAsync();
        Assert.That(state.ExpectedBalance, Is.EqualTo(150m));
        Assert.That(state.TotalCollected, Is.EqualTo(50m));
    }

    // ─── Receipt Issuance (cross-grain with AR) ───────────────────────────

    [Test]
    public async Task IssueReceipt_SetsIssuedStatus()
    {
        // Arrange — CashierReceiptGrain prepends "AR-ACCOUNT:" and "CASHIER-SESSION:" internally,
        // so we pass raw IDs (no prefix) and reference grains with the full prefixed key.
        string rawArId = Guid.NewGuid().ToString();
        IARAccountGrain arAcct = _cluster.GrainFactory.GetGrain<IARAccountGrain>($"AR-ACCOUNT:{rawArId}");
        await arAcct.CreateAsync("PAT-CSH-001", null, ARAccountCategory.CopayOutpatient, 50m, null);

        string rawSessionId = Guid.NewGuid().ToString();
        ICashierSessionGrain session = _cluster.GrainFactory.GetGrain<ICashierSessionGrain>($"CASHIER-SESSION:{rawSessionId}");
        await session.OpenAsync("STA-001", "Main", "USR-001", "Cashier", DateTime.UtcNow.Date, 0m);

        // Act
        ICashierReceiptGrain receipt = NewReceipt();
        await receipt.IssueAsync(
            "R-0001", "PAT-CSH-001", "John Smith",
            rawArId, 50m, CashierPaymentMethod.Cash,
            "USR-001", "Test Cashier", rawSessionId,
            null, null);

        CashierReceiptState state = await receipt.GetAsync();
        Assert.That(state.Status, Is.EqualTo(CashierReceiptStatus.Issued));
        Assert.That(state.Amount, Is.EqualTo(50m));
    }

    [Test]
    public async Task IssueReceipt_PostsPaymentToARAccount()
    {
        // Arrange
        string rawArId = Guid.NewGuid().ToString();
        IARAccountGrain arAcct = _cluster.GrainFactory.GetGrain<IARAccountGrain>($"AR-ACCOUNT:{rawArId}");
        await arAcct.CreateAsync("PAT-CSH-002", null, ARAccountCategory.CopayOutpatient, 50m, null);

        string rawSessionId = Guid.NewGuid().ToString();
        ICashierSessionGrain session = _cluster.GrainFactory.GetGrain<ICashierSessionGrain>($"CASHIER-SESSION:{rawSessionId}");
        await session.OpenAsync("STA-001", "Main", "USR-001", "Cashier", DateTime.UtcNow.Date, 0m);

        // Act
        ICashierReceiptGrain receipt = NewReceipt();
        await receipt.IssueAsync(
            "R-0002", "PAT-CSH-002", "Jane Doe",
            rawArId, 50m, CashierPaymentMethod.Cash,
            "USR-001", "Test Cashier", rawSessionId,
            null, null);

        // Assert — AR balance should be 0 (full payment)
        ARAccountState arState = await arAcct.GetAsync();
        Assert.That(arState.CurrentBalance, Is.EqualTo(0m));
        Assert.That(arState.ARStatus, Is.EqualTo(ARAccountStatus.Paid));
    }

    [Test]
    public async Task IssueReceipt_UpdatesSessionTotals()
    {
        // Arrange
        string rawArId = Guid.NewGuid().ToString();
        IARAccountGrain arAcct = _cluster.GrainFactory.GetGrain<IARAccountGrain>($"AR-ACCOUNT:{rawArId}");
        await arAcct.CreateAsync("PAT-CSH-003", null, ARAccountCategory.CopayOutpatient, 30m, null);

        string rawSessionId = Guid.NewGuid().ToString();
        ICashierSessionGrain session = _cluster.GrainFactory.GetGrain<ICashierSessionGrain>($"CASHIER-SESSION:{rawSessionId}");
        await session.OpenAsync("STA-001", "Main", "USR-001", "Cashier", DateTime.UtcNow.Date, 0m);

        // Act
        ICashierReceiptGrain receipt = NewReceipt();
        await receipt.IssueAsync(
            "R-0003", "PAT-CSH-003", "Bob Jones",
            rawArId, 30m, CashierPaymentMethod.Cash,
            "USR-001", "Test Cashier", rawSessionId,
            null, null);

        // Assert — session expected balance increased
        CashierSessionState sessionState = await session.GetAsync();
        Assert.That(sessionState.TotalCollected, Is.EqualTo(30m));
        Assert.That(sessionState.ExpectedBalance, Is.EqualTo(30m));
    }

    // ─── Receipt Voiding ─────────────────────────────────────────────────

    [Test]
    public async Task VoidReceipt_SetsVoidedStatus()
    {
        string rawArId = Guid.NewGuid().ToString();
        IARAccountGrain arAcct = _cluster.GrainFactory.GetGrain<IARAccountGrain>($"AR-ACCOUNT:{rawArId}");
        await arAcct.CreateAsync("PAT-CSH-010", null, ARAccountCategory.CopayOutpatient, 15m, null);

        string rawSessionId = Guid.NewGuid().ToString();
        ICashierSessionGrain session = _cluster.GrainFactory.GetGrain<ICashierSessionGrain>($"CASHIER-SESSION:{rawSessionId}");
        await session.OpenAsync("STA-001", "Main", "USR-001", "Cashier", DateTime.UtcNow.Date, 0m);

        ICashierReceiptGrain receipt = NewReceipt();
        await receipt.IssueAsync(
            "R-VOID-001", "PAT-CSH-010", "Alice Brown",
            rawArId, 15m, CashierPaymentMethod.Cash,
            "USR-001", "Test Cashier", rawSessionId,
            null, null);

        await receipt.VoidAsync("Entered wrong patient", "USR-002");

        CashierReceiptState state = await receipt.GetAsync();
        Assert.That(state.Status, Is.EqualTo(CashierReceiptStatus.Voided));
        Assert.That(state.VoidReason, Is.Not.Null.And.Not.Empty);
    }
}
