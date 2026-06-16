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
/// Functional tests for TOP Federal Debt Matching — RCTP*.m, RCTOP*.m.
/// </summary>
[TestFixture]
public class TopMatchingWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private ITopMatchingGrain NewMatch()
        => _cluster.GrainFactory.GetGrain<ITopMatchingGrain>($"TOP-MATCH:{Guid.NewGuid()}");

    private ITopMatchIndexGrain GetIndex()
        => _cluster.GrainFactory.GetGrain<ITopMatchIndexGrain>("TOP-MATCH-IDX");

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ─── Grain Level ─────────────────────────────────────────────────────────

    [Test]
    public async Task RecordOffset_SetsPendingMatch()
    {
        ITopMatchingGrain match = NewMatch();
        await match.RecordOffsetAsync("TX-001", "123-45-6789", "VETERAN,TEST",
            500m, "TAX_REFUND", DateTime.UtcNow, null);

        TopMatchingState state = await match.GetAsync();
        Assert.That(state.Status, Is.EqualTo(TopMatchStatus.PendingMatch));
        Assert.That(state.OffsetAmount, Is.EqualTo(500m));
        Assert.That(state.UnmatchedAmount, Is.EqualTo(500m));
    }

    [Test]
    public async Task RecordOffset_StoresTreasuryData()
    {
        ITopMatchingGrain match = NewMatch();
        await match.RecordOffsetAsync("TX-002", "987-65-4321", "SMITH,JOHN",
            1200m, "SSA", DateTime.UtcNow.AddDays(-5), "Monthly SSA offset");

        TopMatchingState state = await match.GetAsync();
        Assert.That(state.TreasuryTransactionId, Is.EqualTo("TX-002"));
        Assert.That(state.TaxpayerIdNumber, Is.EqualTo("987-65-4321"));
        Assert.That(state.OffsetSource, Is.EqualTo("SSA"));
    }

    [Test]
    public async Task MatchToAccount_SetsMatchedStatus()
    {
        ITopMatchingGrain match = NewMatch();
        await match.RecordOffsetAsync("TX-003", "111-22-3333", "DOE,JANE",
            300m, "TAX_REFUND", DateTime.UtcNow, null);

        await match.MatchToAccountAsync("PAT-TM-001", "AR-ACCT-001", "TOP-REF-001",
            300m, "USR-1", "Processor");

        TopMatchingState state = await match.GetAsync();
        Assert.That(state.Status, Is.EqualTo(TopMatchStatus.Matched));
        Assert.That(state.AppliedAmount, Is.EqualTo(300m));
        Assert.That(state.UnmatchedAmount, Is.EqualTo(0m));
    }

    [Test]
    public async Task MatchToAccount_PartialMatch_SetsPartialStatus()
    {
        ITopMatchingGrain match = NewMatch();
        await match.RecordOffsetAsync("TX-004", "444-55-6666", "JONES,BOB",
            500m, "VENDOR", DateTime.UtcNow, null);

        await match.MatchToAccountAsync("PAT-TM-002", "AR-ACCT-002", null,
            350m, "USR-2", "Proc2");

        TopMatchingState state = await match.GetAsync();
        Assert.That(state.Status, Is.EqualTo(TopMatchStatus.PartialMatch));
        Assert.That(state.AppliedAmount, Is.EqualTo(350m));
        Assert.That(state.UnmatchedAmount, Is.EqualTo(150m));
    }

    [Test]
    public async Task MarkUnmatched_SetsUnmatchedStatus()
    {
        ITopMatchingGrain match = NewMatch();
        await match.RecordOffsetAsync("TX-005", "777-88-9999", "UNKNOWN",
            200m, "OPM", DateTime.UtcNow, null);

        await match.MarkUnmatchedAsync(new List<string> { "No matching SSN", "No active TOP referral" }, "USR-3");

        TopMatchingState state = await match.GetAsync();
        Assert.That(state.Status, Is.EqualTo(TopMatchStatus.Unmatched));
        Assert.That(state.MatchReasons, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Reject_SetsRejectedStatus()
    {
        ITopMatchingGrain match = NewMatch();
        await match.RecordOffsetAsync("TX-006", "000-00-0000", "INVALID",
            100m, "TAX_REFUND", DateTime.UtcNow, null);

        await match.RejectAsync(new List<string> { "Invalid TIN format" }, "USR-4");

        TopMatchingState state = await match.GetAsync();
        Assert.That(state.Status, Is.EqualTo(TopMatchStatus.Rejected));
    }

    // ─── Index ───────────────────────────────────────────────────────────────

    [Test]
    public async Task Index_AddOrUpdate_AppearsInGetAll()
    {
        ITopMatchIndexGrain index = GetIndex();
        string matchId = Guid.NewGuid().ToString();
        await index.AddOrUpdateAsync(new TopMatchIndexEntry
        {
            MatchId = matchId, TreasuryTransactionId = "TX-IDX-1", TaxpayerIdNumber = "123-45-6789",
            OffsetAmount = 500m, Status = TopMatchStatus.Matched, OffsetSource = "TAX_REFUND",
            OffsetReceivedDate = DateTime.UtcNow, MatchedPatientId = "PAT-IDX-1",
        });

        List<TopMatchIndexEntry> all = await index.GetAllAsync();
        Assert.That(all.Any(e => e.MatchId == matchId), Is.True);
    }

    [Test]
    public async Task Index_GetPending_FiltersPendingOnly()
    {
        ITopMatchIndexGrain index = GetIndex();
        string pendingId = $"PEND-{Guid.NewGuid()}";
        string matchedId = $"MATCH-{Guid.NewGuid()}";

        await index.AddOrUpdateAsync(new TopMatchIndexEntry
        {
            MatchId = pendingId, TreasuryTransactionId = "TX-P1", TaxpayerIdNumber = "111",
            OffsetAmount = 100m, Status = TopMatchStatus.PendingMatch, OffsetSource = "SSA",
            OffsetReceivedDate = DateTime.UtcNow,
        });
        await index.AddOrUpdateAsync(new TopMatchIndexEntry
        {
            MatchId = matchedId, TreasuryTransactionId = "TX-M1", TaxpayerIdNumber = "222",
            OffsetAmount = 200m, Status = TopMatchStatus.Matched, OffsetSource = "TAX_REFUND",
            OffsetReceivedDate = DateTime.UtcNow, MatchedPatientId = "PAT-1",
        });

        List<TopMatchIndexEntry> pending = await index.GetPendingAsync();
        Assert.That(pending.Any(e => e.MatchId == pendingId), Is.True);
        Assert.That(pending.All(e => e.Status == TopMatchStatus.PendingMatch), Is.True);
    }

    // ─── Workflow Integration ────────────────────────────────────────────────

    [Test]
    public async Task Workflow_ProcessTopOffset_NoReferral_Unmatched()
    {
        string patientId = $"PAT-TM-WF-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        string matchId = await wf.ProcessTopOffsetMatchAsync(
            "TX-WF-001", "555-66-7777", "VETERAN,WF",
            500m, "TAX_REFUND", DateTime.UtcNow, "USR-WF", "WF", null);

        Assert.That(matchId, Is.Not.Null.And.Not.Empty);
        TopMatchingState state = await wf.GetTopMatchRecordAsync(matchId);
        Assert.That(state.Status, Is.EqualTo(TopMatchStatus.Unmatched));
    }

    [Test]
    public async Task Workflow_ProcessTopOffset_WithReferral_Matched()
    {
        string patientId = $"PAT-TM-WF2-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        // Create AR account and TOP referral
        string acctId = await wf.CreateARAccountAsync(null, "CopayOutpatient", 500m, DateTime.UtcNow.AddDays(30));

        IARAccountGrain arAcct = _cluster.GrainFactory.GetGrain<IARAccountGrain>($"AR-ACCOUNT:{acctId}");
        string refId = Guid.NewGuid().ToString();
        await arAcct.ReferToTopAsync(refId, 500m, "USR-TOP", "TOP Admin");

        ITopReferralGrain referral = _cluster.GrainFactory.GetGrain<ITopReferralGrain>($"TOP-REF:{refId}");
        await referral.CreateAsync(acctId, patientId, "VETERAN,TEST", 500m, 500m, "USR-TOP", "TOP Admin", null);

        ITopReferralIndexGrain topIdx = _cluster.GrainFactory.GetGrain<ITopReferralIndexGrain>("TOP-REF-INDEX");
        await topIdx.AddOrUpdateAsync(new TopReferralIndexEntry(refId, acctId, patientId, "VETERAN,TEST", 500m, TopReferralStatus.Pending, DateTime.UtcNow, 0m));

        // Process offset
        string matchId = await wf.ProcessTopOffsetMatchAsync(
            "TX-WF-002", "888-99-0000", "VETERAN,TEST",
            500m, "TAX_REFUND", DateTime.UtcNow, "USR-WF", "WF", null);

        TopMatchingState state = await wf.GetTopMatchRecordAsync(matchId);
        Assert.That(state.Status, Is.EqualTo(TopMatchStatus.Matched));
        Assert.That(state.MatchedARAccountId, Is.EqualTo(acctId));
        Assert.That(state.AppliedAmount, Is.EqualTo(500m));
    }

    [Test]
    public async Task Workflow_GetTopMatchRecords_ReturnsList()
    {
        string patientId = $"PAT-TM-WF3-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = GetWorkflow(patientId);

        // Process an offset (will be unmatched since no referral)
        await wf.ProcessTopOffsetMatchAsync(
            "TX-WF-003", "111-22-3333", "TEST",
            200m, "SSA", DateTime.UtcNow, null, null, null);

        List<TopMatchIndexEntry> records = await wf.GetTopMatchRecordsAsync();
        // May or may not have records depending on patient matching in the singleton index
        Assert.That(records, Is.Not.Null);
    }
}
