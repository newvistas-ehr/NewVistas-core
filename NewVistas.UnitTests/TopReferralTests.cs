// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for Treasury Offset Program grains.
/// ITopReferralGrain (key: "TOP-REF:{guid}") and ITopReferralIndexGrain (key: "TOP-REF-IDX").
/// </summary>
[TestFixture]
public class TopReferralTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private ITopReferralGrain NewReferral() =>
        _cluster.GrainFactory.GetGrain<ITopReferralGrain>($"TOP-REF:{Guid.NewGuid()}");

    private ITopReferralIndexGrain Index =>
        _cluster.GrainFactory.GetGrain<ITopReferralIndexGrain>("TOP-REF-IDX");

    // ─── ITopReferralGrain ────────────────────────────────────────────────

    [Test]
    public async Task TopReferralGrain_Create_SetsPendingStatus()
    {
        ITopReferralGrain grain = NewReferral();

        await grain.CreateAsync(
            "AR-ACCT-001", "PATIENT-001", "Smith, John",
            450.00m, 500.00m,
            "USER-001", "Johnson, Mary", "Debt referred per TOP policy");

        TopReferralState state = await grain.GetAsync();

        Assert.That(state.ARAccountId, Is.EqualTo("AR-ACCT-001"));
        Assert.That(state.PatientId, Is.EqualTo("PATIENT-001"));
        Assert.That(state.PatientName, Is.EqualTo("Smith, John"));
        Assert.That(state.ReferredAmount, Is.EqualTo(450.00m));
        Assert.That(state.OriginalBalance, Is.EqualTo(500.00m));
        Assert.That(state.ReferredByUserId, Is.EqualTo("USER-001"));
        Assert.That(state.Status, Is.EqualTo(TopReferralStatus.Pending));
        Assert.That(state.OffsetAmount, Is.EqualTo(0m));
    }

    [Test]
    public async Task TopReferralGrain_RecordOffset_FullOffset_SetsOffsetStatus()
    {
        ITopReferralGrain grain = NewReferral();
        await grain.CreateAsync(
            "AR-ACCT-002", "PATIENT-002", "Jones, Bob",
            300.00m, 300.00m,
            "USER-001", "Johnson, Mary", null);

        await grain.RecordOffsetAsync(300.00m, DateTime.UtcNow);

        TopReferralState state = await grain.GetAsync();
        Assert.That(state.Status, Is.EqualTo(TopReferralStatus.Offset));
        Assert.That(state.OffsetAmount, Is.EqualTo(300.00m));
    }

    [Test]
    public async Task TopReferralGrain_RecordOffset_Partial_SetsPartiallyOffsetStatus()
    {
        ITopReferralGrain grain = NewReferral();
        await grain.CreateAsync(
            "AR-ACCT-003", "PATIENT-003", "Davis, Alice",
            600.00m, 600.00m,
            "USER-001", "Johnson, Mary", null);

        await grain.RecordOffsetAsync(200.00m, DateTime.UtcNow);

        TopReferralState state = await grain.GetAsync();
        Assert.That(state.Status, Is.EqualTo(TopReferralStatus.PartiallyOffset));
        Assert.That(state.OffsetAmount, Is.EqualTo(200.00m));
    }

    [Test]
    public async Task TopReferralGrain_RecordOffset_Cumulative_PromotesToOffset()
    {
        ITopReferralGrain grain = NewReferral();
        await grain.CreateAsync(
            "AR-ACCT-004", "PATIENT-004", "Wilson, Carol",
            500.00m, 500.00m,
            "USER-001", "Johnson, Mary", null);

        // Two partial offsets that together cover the full amount
        await grain.RecordOffsetAsync(300.00m, DateTime.UtcNow.AddDays(-10));
        await grain.RecordOffsetAsync(200.00m, DateTime.UtcNow);

        TopReferralState state = await grain.GetAsync();
        Assert.That(state.Status, Is.EqualTo(TopReferralStatus.Offset));
        Assert.That(state.OffsetAmount, Is.EqualTo(500.00m));
    }

    [Test]
    public async Task TopReferralGrain_Withdraw_SetsWithdrawnStatus()
    {
        ITopReferralGrain grain = NewReferral();
        await grain.CreateAsync(
            "AR-ACCT-005", "PATIENT-005", "Taylor, Robert",
            250.00m, 250.00m,
            "USER-002", "Chen, Lisa", null);

        await grain.WithdrawAsync("Patient entered repayment plan");

        TopReferralState state = await grain.GetAsync();
        Assert.That(state.Status, Is.EqualTo(TopReferralStatus.Withdrawn));
    }

    // ─── ITopReferralIndexGrain ───────────────────────────────────────────

    private static TopReferralIndexEntry MakeEntry(string referralId, string accountId, string patientId, string patientName, decimal amount, TopReferralStatus status) =>
        new(referralId, accountId, patientId, patientName, amount, status, DateTime.UtcNow, 0m);

    [Test]
    public async Task TopReferralIndex_AddOrUpdate_AppearsInGetAll()
    {
        string referralId = $"TOP-REF:{Guid.NewGuid()}";
        await Index.AddOrUpdateAsync(MakeEntry(referralId, "AR-ACCT-IDX-001", "PATIENT-IDX-001", "Miller, Frank", 400.00m, TopReferralStatus.Pending));

        List<TopReferralIndexEntry> all = await Index.GetAllAsync();
        Assert.That(all.Any(e => e.ReferralId == referralId), Is.True);
    }

    [Test]
    public async Task TopReferralIndex_GetPending_ReturnsOnlyPendingAndCertified()
    {
        string pendingId = $"TOP-REF:{Guid.NewGuid()}";
        string offsetId = $"TOP-REF:{Guid.NewGuid()}";

        await Index.AddOrUpdateAsync(MakeEntry(pendingId, "AR-ACCT-IDX-002", "PATIENT-IDX-002", "Anderson, Sue", 300.00m, TopReferralStatus.Pending));
        await Index.AddOrUpdateAsync(MakeEntry(offsetId, "AR-ACCT-IDX-003", "PATIENT-IDX-003", "Thomas, Ken", 150.00m, TopReferralStatus.Offset));

        List<TopReferralIndexEntry> pending = await Index.GetPendingAsync();
        Assert.That(pending.Any(e => e.ReferralId == pendingId), Is.True);
        Assert.That(pending.All(e => e.Status == TopReferralStatus.Pending || e.Status == TopReferralStatus.Certified), Is.True);
    }

    [Test]
    public async Task TopReferralIndex_GetByAccount_FiltersCorrectly()
    {
        string accountId = $"AR-ACCT-{Guid.NewGuid()}";
        string ref1 = $"TOP-REF:{Guid.NewGuid()}";
        string ref2 = $"TOP-REF:{Guid.NewGuid()}";
        string ref3 = $"TOP-REF:{Guid.NewGuid()}";

        await Index.AddOrUpdateAsync(MakeEntry(ref1, accountId, "P1", "P1 Name", 100m, TopReferralStatus.Pending));
        await Index.AddOrUpdateAsync(MakeEntry(ref2, accountId, "P1", "P1 Name", 200m, TopReferralStatus.PartiallyOffset));
        await Index.AddOrUpdateAsync(MakeEntry(ref3, "AR-ACCT-OTHER", "P2", "P2 Name", 50m, TopReferralStatus.Pending));

        List<TopReferralIndexEntry> byAccount = await Index.GetByAccountAsync(accountId);
        Assert.That(byAccount, Has.Count.EqualTo(2));
        Assert.That(byAccount.All(e => e.ARAccountId == accountId), Is.True);
    }

    [Test]
    public async Task TopReferralIndex_AddOrUpdate_ReplacesExistingEntry()
    {
        string referralId = $"TOP-REF:{Guid.NewGuid()}";

        await Index.AddOrUpdateAsync(MakeEntry(referralId, "AR-ACCT-UPD", "P-UPD", "Update Test", 500m, TopReferralStatus.Pending));
        // Update same referral — status changed
        await Index.AddOrUpdateAsync(MakeEntry(referralId, "AR-ACCT-UPD", "P-UPD", "Update Test", 500m, TopReferralStatus.Offset));

        List<TopReferralIndexEntry> all = await Index.GetAllAsync();
        List<TopReferralIndexEntry> matches = all.Where(e => e.ReferralId == referralId).ToList();
        Assert.That(matches, Has.Count.EqualTo(1), "Should not duplicate entry on update");
        Assert.That(matches[0].Status, Is.EqualTo(TopReferralStatus.Offset));
    }
}
