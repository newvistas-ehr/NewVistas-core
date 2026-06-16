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
/// Functional tests for VistA Accounts Receivable — Files #430, #433, #344, #340, #342
/// and Treasury Offset Program (TOP) referrals.
/// </summary>
[TestFixture]
public class AccountsReceivableWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IARAccountGrain NewAccount()
        => _cluster.GrainFactory.GetGrain<IARAccountGrain>($"AR-ACCOUNT:{Guid.NewGuid()}");

    private IARAccountGrain GetAccount(string id)
        => _cluster.GrainFactory.GetGrain<IARAccountGrain>(id);

    private IARAccountIndexGrain GetIndex(string patientId)
        => _cluster.GrainFactory.GetGrain<IARAccountIndexGrain>($"AR-ACCT-IDX:{patientId}");

    private ITopReferralGrain NewReferral()
        => _cluster.GrainFactory.GetGrain<ITopReferralGrain>($"TOP-REF:{Guid.NewGuid()}");

    private ITopReferralIndexGrain GetTopIndex()
        => _cluster.GrainFactory.GetGrain<ITopReferralIndexGrain>("TOP-REF-IDX");

    // ─── AR Account Creation ─────────────────────────────────────────────

    [Test]
    public async Task CreateARAccount_SetsActiveStatus()
    {
        IARAccountGrain acct = NewAccount();
        await acct.CreateAsync("PAT-001", null, ARAccountCategory.CopayOutpatient, 50m, null);

        ARAccountState state = await acct.GetAsync();
        Assert.That(state.ARStatus, Is.EqualTo(ARAccountStatus.Active));
    }

    [Test]
    public async Task CreateARAccount_CurrentBalanceEqualsOriginalAmount()
    {
        IARAccountGrain acct = NewAccount();
        await acct.CreateAsync("PAT-002", null, ARAccountCategory.CopayPharmacy, 15m, null);

        ARAccountState state = await acct.GetAsync();
        Assert.That(state.CurrentBalance, Is.EqualTo(15m));
        Assert.That(state.OriginalAmount, Is.EqualTo(15m));
    }

    [Test]
    public async Task CreateARAccount_CategoryIsStored()
    {
        IARAccountGrain acct = NewAccount();
        await acct.CreateAsync("PAT-003", null, ARAccountCategory.CopayInpatient, 100m, null);

        ARAccountState state = await acct.GetAsync();
        Assert.That(state.ARCategory, Is.EqualTo(ARAccountCategory.CopayInpatient));
    }

    // ─── Payment ─────────────────────────────────────────────────────────

    [Test]
    public async Task PostPayment_ReducesCurrentBalance()
    {
        IARAccountGrain acct = NewAccount();
        await acct.CreateAsync("PAT-010", null, ARAccountCategory.CopayOutpatient, 50m, null);

        await acct.PostPaymentAsync(20m, "Cash", "USR-001", "Test User", null, null, null);

        ARAccountState state = await acct.GetAsync();
        Assert.That(state.CurrentBalance, Is.EqualTo(30m));
        Assert.That(state.AmountPaid, Is.EqualTo(20m));
    }

    [Test]
    public async Task PostPayment_ReturnsTransactionId()
    {
        IARAccountGrain acct = NewAccount();
        await acct.CreateAsync("PAT-011", null, ARAccountCategory.CopayOutpatient, 50m, null);

        string txnId = await acct.PostPaymentAsync(50m, "Check", "USR-001", "Test User", "R001", "CHK-123", null);

        Assert.That(Guid.TryParse(txnId, out _), Is.True);
    }

    [Test]
    public async Task PostPayment_FullAmount_AutoTransitionsToPaid()
    {
        IARAccountGrain acct = NewAccount();
        await acct.CreateAsync("PAT-012", null, ARAccountCategory.CopayOutpatient, 50m, null);

        await acct.PostPaymentAsync(50m, "Cash", "USR-001", "Test User", null, null, null);

        ARAccountState state = await acct.GetAsync();
        Assert.That(state.ARStatus, Is.EqualTo(ARAccountStatus.Paid));
        Assert.That(state.CurrentBalance, Is.EqualTo(0m));
    }

    // ─── Adjustment ──────────────────────────────────────────────────────

    [Test]
    public async Task PostAdjustment_NegativeAmount_ReducesBalance()
    {
        IARAccountGrain acct = NewAccount();
        await acct.CreateAsync("PAT-020", null, ARAccountCategory.CopayOutpatient, 50m, null);

        await acct.PostAdjustmentAsync(10m, "CREDIT", "USR-001", "Test User", null);

        ARAccountState state = await acct.GetAsync();
        Assert.That(state.CurrentBalance, Is.EqualTo(40m));
    }

    // ─── Waiver ──────────────────────────────────────────────────────────

    [Test]
    public async Task WaiveBalance_ReducesCurrentBalance()
    {
        IARAccountGrain acct = NewAccount();
        await acct.CreateAsync("PAT-030", null, ARAccountCategory.CopayOutpatient, 50m, null);

        await acct.WaiveAsync(50m, "USR-001", "Test User", "Financial hardship");

        ARAccountState state = await acct.GetAsync();
        Assert.That(state.CurrentBalance, Is.EqualTo(0m));
        Assert.That(state.AmountWaived, Is.EqualTo(50m));
    }

    [Test]
    public async Task WaiveBalance_FullAmount_SetsWaivedStatus()
    {
        IARAccountGrain acct = NewAccount();
        await acct.CreateAsync("PAT-031", null, ARAccountCategory.CopayOutpatient, 50m, null);

        await acct.WaiveAsync(50m, "USR-001", "Test User", "Hardship");

        ARAccountState state = await acct.GetAsync();
        Assert.That(state.ARStatus, Is.EqualTo(ARAccountStatus.Waived));
    }

    // ─── Write-Off ───────────────────────────────────────────────────────

    [Test]
    public async Task WriteOff_SetsWrittenOffStatus()
    {
        IARAccountGrain acct = NewAccount();
        await acct.CreateAsync("PAT-040", null, ARAccountCategory.CopayOutpatient, 50m, null);

        await acct.WriteOffAsync(50m, "USR-001", "Test User", "Uncollectable");

        ARAccountState state = await acct.GetAsync();
        Assert.That(state.ARStatus, Is.EqualTo(ARAccountStatus.WrittenOff));
    }

    // ─── Interest / Penalty / Admin Cost Accrual ─────────────────────────

    [Test]
    public async Task AccrueInterest_IncreasesCurrentBalance()
    {
        IARAccountGrain acct = NewAccount();
        await acct.CreateAsync("PAT-050", null, ARAccountCategory.CopayOutpatient, 50m, null);

        await acct.AccrueInterestAsync(5m, "USR-001");

        ARAccountState state = await acct.GetAsync();
        Assert.That(state.CurrentBalance, Is.EqualTo(55m));
        Assert.That(state.InterestAccrued, Is.EqualTo(5m));
    }

    [Test]
    public async Task AccruePenalty_IncreasesCurrentBalance()
    {
        IARAccountGrain acct = NewAccount();
        await acct.CreateAsync("PAT-051", null, ARAccountCategory.CopayOutpatient, 50m, null);

        await acct.AccruePenaltyAsync(3m, "USR-001");

        ARAccountState state = await acct.GetAsync();
        Assert.That(state.CurrentBalance, Is.EqualTo(53m));
        Assert.That(state.PenaltyAccrued, Is.EqualTo(3m));
    }

    [Test]
    public async Task AccrueAdminCost_IncreasesCurrentBalance()
    {
        IARAccountGrain acct = NewAccount();
        await acct.CreateAsync("PAT-052", null, ARAccountCategory.CopayOutpatient, 50m, null);

        await acct.AccrueAdminCostAsync(2m, "USR-001");

        ARAccountState state = await acct.GetAsync();
        Assert.That(state.CurrentBalance, Is.EqualTo(52m));
        Assert.That(state.AdminCostAccrued, Is.EqualTo(2m));
    }

    // ─── Treasury Offset Program (TOP) ───────────────────────────────────

    [Test]
    public async Task ReferToTop_SetsIsTreasuryOffsetTrue()
    {
        IARAccountGrain acct = NewAccount();
        await acct.CreateAsync("PAT-060", null, ARAccountCategory.CopayOutpatient, 200m, null);

        await acct.ReferToTopAsync($"TOP-REF-{Guid.NewGuid()}", 200m, "USR-001", "Test User");

        ARAccountState state = await acct.GetAsync();
        Assert.That(state.IsTreasuryOffset, Is.True);
        Assert.That(state.ARStatus, Is.EqualTo(ARAccountStatus.TreasuryOffset));
    }

    [Test]
    public async Task RecordTopOffset_ReducesBalance()
    {
        IARAccountGrain acct = NewAccount();
        await acct.CreateAsync("PAT-061", null, ARAccountCategory.CopayOutpatient, 200m, null);
        await acct.ReferToTopAsync($"TOP-REF-{Guid.NewGuid()}", 200m, "USR-001", "Test User");

        await acct.RecordTopOffsetAsync(100m, "USR-001", "Test User");

        ARAccountState state = await acct.GetAsync();
        Assert.That(state.CurrentBalance, Is.EqualTo(100m));
    }

    [Test]
    public async Task RecordTopOffset_FullAmount_SetsPaidAndClearsFlag()
    {
        IARAccountGrain acct = NewAccount();
        await acct.CreateAsync("PAT-062", null, ARAccountCategory.CopayOutpatient, 200m, null);
        await acct.ReferToTopAsync($"TOP-REF-{Guid.NewGuid()}", 200m, "USR-001", "Test User");

        await acct.RecordTopOffsetAsync(200m, "USR-001", "Test User");

        ARAccountState state = await acct.GetAsync();
        Assert.That(state.ARStatus, Is.EqualTo(ARAccountStatus.Paid));
        Assert.That(state.IsTreasuryOffset, Is.False);
    }

    [Test]
    public async Task WithdrawTopReferral_ClearsIsTreasuryOffsetAndSetsActive()
    {
        IARAccountGrain acct = NewAccount();
        await acct.CreateAsync("PAT-063", null, ARAccountCategory.CopayOutpatient, 200m, null);
        await acct.ReferToTopAsync($"TOP-REF-{Guid.NewGuid()}", 200m, "USR-001", "Test User");

        await acct.WithdrawTopReferralAsync();

        ARAccountState state = await acct.GetAsync();
        Assert.That(state.IsTreasuryOffset, Is.False);
        Assert.That(state.ARStatus, Is.EqualTo(ARAccountStatus.Active));
    }

    // ─── TOP Referral Grain ───────────────────────────────────────────────

    [Test]
    public async Task TopReferral_Create_SetsPendingStatus()
    {
        ITopReferralGrain referral = NewReferral();
        await referral.CreateAsync("AR-ACCOUNT-001", "PAT-070", "John Smith", 500m, 500m, "USR-001", "Test User", null);

        TopReferralState state = await referral.GetAsync();
        Assert.That(state.Status, Is.EqualTo(TopReferralStatus.Pending));
        Assert.That(state.ReferredAmount, Is.EqualTo(500m));
    }

    [Test]
    public async Task TopReferral_FullOffset_SetsOffsetStatus()
    {
        ITopReferralGrain referral = NewReferral();
        await referral.CreateAsync("AR-ACCOUNT-002", "PAT-071", "Jane Doe", 300m, 300m, "USR-001", "Test User", null);

        await referral.RecordOffsetAsync(300m, DateTime.UtcNow);

        TopReferralState state = await referral.GetAsync();
        Assert.That(state.Status, Is.EqualTo(TopReferralStatus.Offset));
        Assert.That(state.OffsetAmount, Is.EqualTo(300m));
    }

    [Test]
    public async Task TopReferral_PartialOffset_SetsPartiallyOffsetStatus()
    {
        ITopReferralGrain referral = NewReferral();
        await referral.CreateAsync("AR-ACCOUNT-003", "PAT-072", "Bob Jones", 300m, 300m, "USR-001", "Test User", null);

        await referral.RecordOffsetAsync(100m, DateTime.UtcNow);

        TopReferralState state = await referral.GetAsync();
        Assert.That(state.Status, Is.EqualTo(TopReferralStatus.PartiallyOffset));
        Assert.That(state.OffsetAmount, Is.EqualTo(100m));
    }

    [Test]
    public async Task TopReferral_Withdraw_SetsWithdrawnStatus()
    {
        ITopReferralGrain referral = NewReferral();
        await referral.CreateAsync("AR-ACCOUNT-004", "PAT-073", "Alice Brown", 200m, 200m, "USR-001", "Test User", null);

        await referral.WithdrawAsync("Patient entered payment plan");

        TopReferralState state = await referral.GetAsync();
        Assert.That(state.Status, Is.EqualTo(TopReferralStatus.Withdrawn));
    }

    // ─── AR Account Index ────────────────────────────────────────────────

    [Test]
    public async Task ARAccountIndex_AddOrUpdate_AppearsInGetAll()
    {
        string patientId = $"PAT-IDX-{Guid.NewGuid()}";
        IARAccountIndexGrain index = GetIndex(patientId);

        string acctId = $"AR-ACCOUNT-{Guid.NewGuid()}";
        ARAccountIndexEntry entry = new ARAccountIndexEntry
        {
            ARAccountId = acctId,
            PatientId = patientId,
            ARCategory = nameof(ARAccountCategory.CopayOutpatient),
            OriginalAmount = 50m,
            CurrentBalance = 50m,
            ARStatus = nameof(ARAccountStatus.Active),
            DateEstablished = DateTime.UtcNow
        };

        await index.AddOrUpdateAsync(entry);
        List<ARAccountIndexEntry> all = await index.GetAllAsync();

        Assert.That(all, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(all.Any(e => e.ARAccountId == acctId), Is.True);
    }

    [Test]
    public async Task ARAccountIndex_GetActive_ReturnsOnlyActiveAccounts()
    {
        string patientId = $"PAT-IDX2-{Guid.NewGuid()}";
        IARAccountIndexGrain index = GetIndex(patientId);

        string activeId = $"AR-ACCOUNT-{Guid.NewGuid()}";
        string paidId = $"AR-ACCOUNT-{Guid.NewGuid()}";

        await index.AddOrUpdateAsync(new ARAccountIndexEntry { ARAccountId = activeId, PatientId = patientId, ARStatus = nameof(ARAccountStatus.Active), ARCategory = nameof(ARAccountCategory.CopayPharmacy), OriginalAmount = 15m, CurrentBalance = 15m, DateEstablished = DateTime.UtcNow });
        await index.AddOrUpdateAsync(new ARAccountIndexEntry { ARAccountId = paidId, PatientId = patientId, ARStatus = nameof(ARAccountStatus.Paid), ARCategory = nameof(ARAccountCategory.CopayPharmacy), OriginalAmount = 15m, CurrentBalance = 0m, DateEstablished = DateTime.UtcNow });

        List<ARAccountIndexEntry> active = await index.GetActiveAsync();

        Assert.That(active.Any(e => e.ARAccountId == activeId), Is.True);
        Assert.That(active.Any(e => e.ARAccountId == paidId), Is.False);
    }
}
