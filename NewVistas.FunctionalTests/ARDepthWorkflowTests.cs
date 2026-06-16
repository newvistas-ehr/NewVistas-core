// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Accounts Receivable methods through IPatientWorkflowGrain.
/// Covers account creation, payments, adjustments, waivers, interest, penalties,
/// and administrative costs — all via the workflow orchestration layer.
/// </summary>
[TestFixture]
public class ARDepthWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ── Tests ─────────────────────────────────────────────────────────────────

    [Test]
    public async Task CreateARAccount_ReturnsNonEmptyId()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        string accountId = await wf.CreateARAccountAsync(null, "CopayOutpatient", 150.00m, DateTime.UtcNow.AddDays(30));

        Assert.That(accountId, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task GetARAccount_ReturnsFullState()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        string accountId = await wf.CreateARAccountAsync(null, "CopayOutpatient", 150.00m, DateTime.UtcNow.AddDays(30));

        ARAccountState state = await wf.GetARAccountAsync(accountId);
        Assert.That(state.OriginalAmount, Is.EqualTo(150.00m));
        Assert.That(state.ARCategory, Is.EqualTo(ARAccountCategory.CopayOutpatient));
    }

    [Test]
    public async Task GetARAccounts_ReturnsList()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        await wf.CreateARAccountAsync(null, "CopayOutpatient", 100.00m, DateTime.UtcNow.AddDays(30));
        await wf.CreateARAccountAsync(null, "CopayPharmacy", 50.00m, DateTime.UtcNow.AddDays(30));

        List<ARAccountIndexEntry> accounts = await wf.GetARAccountsAsync();
        Assert.That(accounts, Has.Count.GreaterThanOrEqualTo(2));
    }

    [Test]
    public async Task PostARPayment_ReducesBalance()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        string accountId = await wf.CreateARAccountAsync(null, "CopayOutpatient", 200.00m, DateTime.UtcNow.AddDays(30));

        await wf.PostARPaymentAsync(accountId, 50m, "CHECK", "USER-001", "Clerk A", null, "1234", null);

        ARAccountState state = await wf.GetARAccountAsync(accountId);
        Assert.That(state.CurrentBalance, Is.EqualTo(150.00m));
    }

    [Test]
    public async Task PostARAdjustment_ModifiesBalance()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        string accountId = await wf.CreateARAccountAsync(null, "CopayOutpatient", 200.00m, DateTime.UtcNow.AddDays(30));

        await wf.PostARAdjustmentAsync(accountId, 25m, "ADMINISTRATIVE", "USER-002", "Supervisor", "Billing error correction");

        ARAccountState state = await wf.GetARAccountAsync(accountId);
        Assert.That(state.CurrentBalance, Is.EqualTo(175.00m));
    }

    [Test]
    public async Task WaiveARAccount_ReducesBalance()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        string accountId = await wf.CreateARAccountAsync(null, "CopayOutpatient", 200.00m, DateTime.UtcNow.AddDays(30));

        await wf.WaiveARAccountAsync(accountId, 100m, "USER-003", "Director", "Hardship waiver approved");

        ARAccountState state = await wf.GetARAccountAsync(accountId);
        Assert.That(state.CurrentBalance, Is.EqualTo(100.00m));
    }

    [Test]
    public async Task AccrueARInterest_IncreasesBalance()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        string accountId = await wf.CreateARAccountAsync(null, "CopayOutpatient", 200.00m, DateTime.UtcNow.AddDays(30));

        await wf.AccrueARInterestAsync(accountId, 5.25m, "SYSTEM");

        ARAccountState state = await wf.GetARAccountAsync(accountId);
        Assert.That(state.CurrentBalance, Is.EqualTo(205.25m));
    }

    [Test]
    public async Task AccrueARPenalty_IncreasesBalance()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        string accountId = await wf.CreateARAccountAsync(null, "CopayOutpatient", 200.00m, DateTime.UtcNow.AddDays(30));

        await wf.AccrueARPenaltyAsync(accountId, 15m, "SYSTEM");

        ARAccountState state = await wf.GetARAccountAsync(accountId);
        Assert.That(state.CurrentBalance, Is.EqualTo(215.00m));
    }

    [Test]
    public async Task AccrueARAdminCost_IncreasesBalance()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        string accountId = await wf.CreateARAccountAsync(null, "CopayOutpatient", 200.00m, DateTime.UtcNow.AddDays(30));

        await wf.AccrueARAdminCostAsync(accountId, 10m, "SYSTEM");

        ARAccountState state = await wf.GetARAccountAsync(accountId);
        Assert.That(state.CurrentBalance, Is.EqualTo(210.00m));
    }
}
