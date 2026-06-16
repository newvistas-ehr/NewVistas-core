// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for the three High-priority pharmacy gaps:
/// 1. Controlled Substance / DEA enforcement (fill gate, Schedule III-V limits)
/// 2. Dispense Constraints (MaxRefills, MaxDaysSupply, MaxQuantity)
/// 3. Prior Auth / Insurance (tested at workflow level in functional tests)
///
/// All tests exercise grain-level enforcement directly.
/// VistA reference: PSOORED.m DEA checks, PSO*5.0*340, CalcMaxRefills, QtyToDays.
/// </summary>
[TestFixture]
public class PharmacyHighPriorityGapTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private async Task<IPharmacyGrain> CreateRx(
        int refills = 5, int daysSupply = 30, int quantity = 30,
        bool isControlled = false, string? deaSchedule = null, bool deaPassed = true)
    {
        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain grain = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await grain.CreatePrescriptionAsync("P-001", "TEST DRUG", null, null, null, null, null,
            daysSupply, quantity, refills, null, null, null, null, null, null);

        if (isControlled)
            await grain.SetDeaCheckResultAsync(true, deaSchedule, deaPassed, deaPassed ? null : "DEA check failed");

        return grain;
    }

    // ═══ DEA / CONTROLLED SUBSTANCE ═════════════════════════════════════════

    // ─── DEA Fill Gate ──────────────────────────────────────────────────────

    [Test]
    public async Task Fill_ControlledSubstance_DeaNotPassed_Throws()
    {
        IPharmacyGrain grain = await CreateRx(isControlled: true, deaSchedule: "III", deaPassed: false);
        await grain.VerifyAsync("RPH-001");

        Assert.ThrowsAsync<InvalidOperationException>(
            () => grain.FillPrescriptionAsync(DateTime.UtcNow));
    }

    [Test]
    public async Task Fill_ControlledSubstance_DeaPassed_Succeeds()
    {
        IPharmacyGrain grain = await CreateRx(isControlled: true, deaSchedule: "III", deaPassed: true);
        await grain.VerifyAsync("RPH-001");

        Assert.DoesNotThrowAsync(() => grain.FillPrescriptionAsync(DateTime.UtcNow));
    }

    [Test]
    public async Task Fill_NonControlled_NoDeaCheckNeeded()
    {
        IPharmacyGrain grain = await CreateRx(isControlled: false);
        await grain.VerifyAsync("RPH-001");

        Assert.DoesNotThrowAsync(() => grain.FillPrescriptionAsync(DateTime.UtcNow));
    }

    // ─── DEA Schedule III-V: Max 5 Refills ──────────────────────────────────

    [Test]
    public async Task Refill_ScheduleIII_After5Refills_Throws()
    {
        IPharmacyGrain grain = await CreateRx(refills: 10, daysSupply: 5,
            isControlled: true, deaSchedule: "III");
        await grain.VerifyAsync("RPH-001");

        DateTime baseDate = DateTime.UtcNow.Date;
        await grain.FillPrescriptionAsync(baseDate.AddDays(-35));
        // 5 refills
        for (int i = 1; i <= 5; i++)
            await grain.RefillAsync(baseDate.AddDays(-35 + (i * 5)));

        // 6th refill should fail
        Assert.ThrowsAsync<InvalidOperationException>(
            () => grain.RefillAsync(baseDate));
    }

    [Test]
    public async Task Refill_ScheduleIV_Within5Refills_Succeeds()
    {
        IPharmacyGrain grain = await CreateRx(refills: 5, daysSupply: 5,
            isControlled: true, deaSchedule: "IV");
        await grain.VerifyAsync("RPH-001");

        DateTime baseDate = DateTime.UtcNow.Date;
        await grain.FillPrescriptionAsync(baseDate.AddDays(-5));
        await grain.RefillAsync(baseDate);

        PharmacyState state = await grain.GetPrescriptionAsync();
        Assert.That(state.RefillsRemaining, Is.EqualTo(4));
    }

    // ─── DEA Schedule III-V: 6-Month Limit ──────────────────────────────────

    [Test]
    public async Task Refill_ScheduleIII_After6Months_Throws()
    {
        IPharmacyGrain grain = await CreateRx(refills: 5, daysSupply: 30,
            isControlled: true, deaSchedule: "III");
        await grain.VerifyAsync("RPH-001");

        // Fill within 6 months
        DateTime baseDate = DateTime.UtcNow.Date;
        await grain.FillPrescriptionAsync(baseDate.AddDays(-25));

        // Try to refill more than 6 months from issue date
        DateTime sevenMonthsFromNow = baseDate.AddMonths(7);
        Assert.ThrowsAsync<InvalidOperationException>(
            () => grain.RefillAsync(sevenMonthsFromNow));
    }

    // ─── CheckRefillEligibility: DEA Schedule III-V ─────────────────────────

    [Test]
    public async Task CheckEligibility_ScheduleIII_5RefillsUsed_ReportsIneligible()
    {
        IPharmacyGrain grain = await CreateRx(refills: 10, daysSupply: 5,
            isControlled: true, deaSchedule: "III");
        await grain.VerifyAsync("RPH-001");

        DateTime baseDate = DateTime.UtcNow.Date;
        await grain.FillPrescriptionAsync(baseDate.AddDays(-35));
        for (int i = 1; i <= 5; i++)
            await grain.RefillAsync(baseDate.AddDays(-35 + (i * 5)));

        RefillEligibilityResult result = await grain.CheckRefillEligibilityAsync(baseDate);

        Assert.That(result.IsEligible, Is.False);
        Assert.That(result.Reasons, Has.Some.Contains("maximum 5 refills"));
    }

    // ═══ DISPENSE CONSTRAINTS ═══════════════════════════════════════════════

    // ─── MaxDaysSupply ──────────────────────────────────────────────────────

    [Test]
    public async Task Fill_DaysSupplyExceedsMax_Throws()
    {
        IPharmacyGrain grain = await CreateRx(daysSupply: 90, quantity: 90);
        await grain.SetDispenseConstraintsAsync(null, 30, null, false, false, false);
        await grain.VerifyAsync("RPH-001");

        Assert.ThrowsAsync<InvalidOperationException>(
            () => grain.FillPrescriptionAsync(DateTime.UtcNow));
    }

    [Test]
    public async Task Fill_DaysSupplyWithinMax_Succeeds()
    {
        IPharmacyGrain grain = await CreateRx(daysSupply: 30, quantity: 30);
        await grain.SetDispenseConstraintsAsync(null, 90, null, false, false, false);
        await grain.VerifyAsync("RPH-001");

        Assert.DoesNotThrowAsync(() => grain.FillPrescriptionAsync(DateTime.UtcNow));
    }

    // ─── MaxQuantity ────────────────────────────────────────────────────────

    [Test]
    public async Task Fill_QuantityExceedsMax_Throws()
    {
        IPharmacyGrain grain = await CreateRx(daysSupply: 30, quantity: 100);
        await grain.SetDispenseConstraintsAsync(null, null, 60, false, false, false);
        await grain.VerifyAsync("RPH-001");

        Assert.ThrowsAsync<InvalidOperationException>(
            () => grain.FillPrescriptionAsync(DateTime.UtcNow));
    }

    [Test]
    public async Task Fill_QuantityWithinMax_Succeeds()
    {
        IPharmacyGrain grain = await CreateRx(daysSupply: 30, quantity: 30);
        await grain.SetDispenseConstraintsAsync(null, null, 60, false, false, false);
        await grain.VerifyAsync("RPH-001");

        Assert.DoesNotThrowAsync(() => grain.FillPrescriptionAsync(DateTime.UtcNow));
    }

    // ─── MaxRefills ─────────────────────────────────────────────────────────

    [Test]
    public async Task Refill_ExceedsMaxRefills_Throws()
    {
        IPharmacyGrain grain = await CreateRx(refills: 10, daysSupply: 5);
        await grain.SetDispenseConstraintsAsync(2, null, null, false, false, false);
        await grain.VerifyAsync("RPH-001");

        DateTime baseDate = DateTime.UtcNow.Date;
        await grain.FillPrescriptionAsync(baseDate.AddDays(-20));
        await grain.RefillAsync(baseDate.AddDays(-16));
        await grain.RefillAsync(baseDate.AddDays(-12));

        // 3rd refill should fail (MaxRefills = 2)
        // Note: expiration = -12+5 = -7, so refill at -8 is before exp
        Assert.ThrowsAsync<InvalidOperationException>(
            () => grain.RefillAsync(baseDate.AddDays(-8)));
    }

    [Test]
    public async Task Refill_WithinMaxRefills_Succeeds()
    {
        IPharmacyGrain grain = await CreateRx(refills: 5, daysSupply: 5);
        await grain.SetDispenseConstraintsAsync(3, null, null, false, false, false);
        await grain.VerifyAsync("RPH-001");

        DateTime baseDate = DateTime.UtcNow.Date;
        await grain.FillPrescriptionAsync(baseDate.AddDays(-5));
        await grain.RefillAsync(baseDate);

        PharmacyState state = await grain.GetPrescriptionAsync();
        Assert.That(state.RefillsRemaining, Is.EqualTo(4));
    }

    // ─── Refill MaxDaysSupply/MaxQuantity ───────────────────────────────────

    [Test]
    public async Task Refill_DaysSupplyExceedsMax_Throws()
    {
        IPharmacyGrain grain = await CreateRx(refills: 5, daysSupply: 90, quantity: 90);
        await grain.SetDispenseConstraintsAsync(null, 30, null, false, false, false);
        await grain.VerifyAsync("RPH-001");

        // Fill succeeds because we test the fill constraint separately
        // For refill, we need to have filled first — but fill also checks MaxDaysSupply
        // So create without constraints, fill, then set constraints
        string rxId2 = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain grain2 = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId2);
        await grain2.CreatePrescriptionAsync("P-001", "DRUG", null, null, null, null, null,
            90, 90, 5, null, null, null, null, null, null);
        await grain2.VerifyAsync("RPH-001");
        await grain2.FillPrescriptionAsync(DateTime.UtcNow.Date.AddDays(-100));

        // Set constraint AFTER initial fill
        await grain2.SetDispenseConstraintsAsync(null, 30, null, false, false, false);

        Assert.ThrowsAsync<InvalidOperationException>(
            () => grain2.RefillAsync(DateTime.UtcNow.Date));
    }

    // ─── CheckRefillEligibility: Constraints ────────────────────────────────

    [Test]
    public async Task CheckEligibility_MaxRefillsExceeded_ReportsIneligible()
    {
        IPharmacyGrain grain = await CreateRx(refills: 10, daysSupply: 5);
        await grain.SetDispenseConstraintsAsync(1, null, null, false, false, false);
        await grain.VerifyAsync("RPH-001");

        DateTime baseDate = DateTime.UtcNow.Date;
        await grain.FillPrescriptionAsync(baseDate.AddDays(-10));
        await grain.RefillAsync(baseDate.AddDays(-6));

        RefillEligibilityResult result = await grain.CheckRefillEligibilityAsync(baseDate.AddDays(-2));

        Assert.That(result.IsEligible, Is.False);
        Assert.That(result.Reasons, Has.Some.Contains("Maximum refills reached"));
    }

    [Test]
    public async Task CheckEligibility_MaxDaysSupplyExceeded_ReportsIneligible()
    {
        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain grain = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await grain.CreatePrescriptionAsync("P-001", "DRUG", null, null, null, null, null,
            90, 90, 5, null, null, null, null, null, null);
        await grain.VerifyAsync("RPH-001");
        await grain.FillPrescriptionAsync(DateTime.UtcNow.Date.AddDays(-100));
        await grain.SetDispenseConstraintsAsync(null, 30, null, false, false, false);

        RefillEligibilityResult result = await grain.CheckRefillEligibilityAsync(DateTime.UtcNow.Date);

        Assert.That(result.IsEligible, Is.False);
        Assert.That(result.Reasons, Has.Some.Contains("Days supply"));
    }

    [Test]
    public async Task CheckEligibility_MaxQuantityExceeded_ReportsIneligible()
    {
        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain grain = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await grain.CreatePrescriptionAsync("P-001", "DRUG", null, null, null, null, null,
            30, 100, 5, null, null, null, null, null, null);
        await grain.VerifyAsync("RPH-001");
        await grain.FillPrescriptionAsync(DateTime.UtcNow.Date.AddDays(-28));
        await grain.SetDispenseConstraintsAsync(null, null, 60, false, false, false);

        RefillEligibilityResult result = await grain.CheckRefillEligibilityAsync(DateTime.UtcNow.Date);

        Assert.That(result.IsEligible, Is.False);
        Assert.That(result.Reasons, Has.Some.Contains("Quantity"));
    }
}
