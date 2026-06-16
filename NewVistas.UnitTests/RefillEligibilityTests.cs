// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for refill eligibility validation at the PharmacyGrain level.
/// Tests CheckRefillEligibilityAsync (read-only query) and RefillAsync guards
/// (DEA Schedule II prohibition, early refill 75% rule).
///
/// VistA reference: PSO refill date calculation, CalcMaxRefills, 21 CFR 1306.12.
/// </summary>
[TestFixture]
public class RefillEligibilityTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private async Task<IPharmacyGrain> CreateVerifiedFilledRx(
        int refills = 5, int daysSupply = 30, int daysAgoFilled = 15,
        bool isControlled = false, string? deaSchedule = null)
    {
        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain grain = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await grain.CreatePrescriptionAsync("P-001", "TEST DRUG", null, null, null, null, null,
            daysSupply, 30, refills, null, null, null, null, null, null);

        if (isControlled)
            await grain.SetDeaCheckResultAsync(true, deaSchedule, true, null);

        await grain.VerifyAsync("RPH-001");
        await grain.FillPrescriptionAsync(DateTime.UtcNow.AddDays(-daysAgoFilled));
        return grain;
    }

    // ─── CheckRefillEligibility Tests ────────────────────────────────────────

    [Test]
    public async Task CheckEligibility_EligibleRx_ReturnsEligible()
    {
        IPharmacyGrain grain = await CreateVerifiedFilledRx(refills: 5, daysSupply: 30, daysAgoFilled: 25);

        RefillEligibilityResult result = await grain.CheckRefillEligibilityAsync(DateTime.UtcNow);

        Assert.That(result.IsEligible, Is.True);
        Assert.That(result.Reasons, Is.Empty);
        Assert.That(result.RefillsRemaining, Is.EqualTo(5));
        Assert.That(result.TotalRefillsAuthorized, Is.EqualTo(5));
        Assert.That(result.RefillsDispensed, Is.EqualTo(0));
        Assert.That(result.Status, Is.EqualTo("ACTIVE"));
    }

    [Test]
    public async Task CheckEligibility_NotActive_ReturnsIneligible()
    {
        IPharmacyGrain grain = await CreateVerifiedFilledRx();
        await grain.PlaceOnHoldAsync("Testing");

        RefillEligibilityResult result = await grain.CheckRefillEligibilityAsync(DateTime.UtcNow);

        Assert.That(result.IsEligible, Is.False);
        Assert.That(result.Reasons, Does.Contain("Prescription status is HOLD. Must be ACTIVE."));
    }

    [Test]
    public async Task CheckEligibility_NotFilled_ReturnsIneligible()
    {
        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain grain = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await grain.CreatePrescriptionAsync("P-001", "DRUG", null, null, null, null, null,
            30, 30, 5, null, null, null, null, null, null);

        RefillEligibilityResult result = await grain.CheckRefillEligibilityAsync(DateTime.UtcNow);

        Assert.That(result.IsEligible, Is.False);
        Assert.That(result.Reasons, Has.Some.Contains("not been initially filled"));
    }

    [Test]
    public async Task CheckEligibility_NoRefillsRemaining_ReturnsIneligible()
    {
        IPharmacyGrain grain = await CreateVerifiedFilledRx(refills: 0, daysSupply: 30, daysAgoFilled: 25);

        RefillEligibilityResult result = await grain.CheckRefillEligibilityAsync(DateTime.UtcNow);

        Assert.That(result.IsEligible, Is.False);
        Assert.That(result.RefillsRemaining, Is.EqualTo(0));
        Assert.That(result.Reasons, Has.Some.Contains("No refills remaining"));
    }

    [Test]
    public async Task CheckEligibility_Expired_ReturnsIneligible()
    {
        IPharmacyGrain grain = await CreateVerifiedFilledRx(daysSupply: 10, daysAgoFilled: 20);

        RefillEligibilityResult result = await grain.CheckRefillEligibilityAsync(DateTime.UtcNow);

        Assert.That(result.IsEligible, Is.False);
        Assert.That(result.Reasons, Has.Some.Contains("expired"));
    }

    [Test]
    public async Task CheckEligibility_DeaScheduleII_ReturnsIneligible()
    {
        IPharmacyGrain grain = await CreateVerifiedFilledRx(
            isControlled: true, deaSchedule: "II", daysAgoFilled: 25);

        RefillEligibilityResult result = await grain.CheckRefillEligibilityAsync(DateTime.UtcNow);

        Assert.That(result.IsEligible, Is.False);
        Assert.That(result.IsControlledSubstance, Is.True);
        Assert.That(result.DeaSchedule, Is.EqualTo("II"));
        Assert.That(result.Reasons, Has.Some.Contains("Schedule II"));
    }

    [Test]
    public async Task CheckEligibility_DeaScheduleIIN_ReturnsIneligible()
    {
        IPharmacyGrain grain = await CreateVerifiedFilledRx(
            isControlled: true, deaSchedule: "IIN", daysAgoFilled: 25);

        RefillEligibilityResult result = await grain.CheckRefillEligibilityAsync(DateTime.UtcNow);

        Assert.That(result.IsEligible, Is.False);
        Assert.That(result.Reasons, Has.Some.Contains("Schedule II"));
    }

    [Test]
    public async Task CheckEligibility_TooEarly_ReturnsIneligibleWithPercentConsumed()
    {
        // 30-day supply, filled 10 days ago → only ~33% consumed (need 75%)
        IPharmacyGrain grain = await CreateVerifiedFilledRx(daysSupply: 30, daysAgoFilled: 10);

        RefillEligibilityResult result = await grain.CheckRefillEligibilityAsync(DateTime.UtcNow);

        Assert.That(result.IsEligible, Is.False);
        Assert.That(result.IsTooEarly, Is.True);
        Assert.That(result.PercentConsumed, Is.LessThan(75));
        Assert.That(result.EarliestRefillDate, Is.Not.Null);
        Assert.That(result.Reasons, Has.Some.Contains("75% consumed rule"));
    }

    [Test]
    public async Task CheckEligibility_Exactly75Percent_ReturnsEligible()
    {
        // 30-day supply, filled 23 days ago → ~76% consumed (just past 75%)
        IPharmacyGrain grain = await CreateVerifiedFilledRx(daysSupply: 30, daysAgoFilled: 23);

        RefillEligibilityResult result = await grain.CheckRefillEligibilityAsync(DateTime.UtcNow);

        Assert.That(result.IsEligible, Is.True);
        Assert.That(result.IsTooEarly, Is.False);
        Assert.That(result.PercentConsumed, Is.GreaterThanOrEqualTo(75));
    }

    [Test]
    public async Task CheckEligibility_EarliestRefillDate_CalculatedCorrectly()
    {
        // 30-day supply → 75% = 22.5 days
        IPharmacyGrain grain = await CreateVerifiedFilledRx(daysSupply: 30, daysAgoFilled: 5);

        RefillEligibilityResult result = await grain.CheckRefillEligibilityAsync(DateTime.UtcNow);

        Assert.That(result.EarliestRefillDate, Is.Not.Null);
        // Earliest refill should be ~22.5 days after last dispense
        DateTime expectedEarliest = result.LastDispenseDate!.Value.AddDays(22.5);
        Assert.That(result.EarliestRefillDate.Value, Is.EqualTo(expectedEarliest));
    }

    [Test]
    public async Task CheckEligibility_ScheduleIV_AllowsRefill()
    {
        IPharmacyGrain grain = await CreateVerifiedFilledRx(
            isControlled: true, deaSchedule: "IV", daysAgoFilled: 25);

        RefillEligibilityResult result = await grain.CheckRefillEligibilityAsync(DateTime.UtcNow);

        Assert.That(result.IsControlledSubstance, Is.True);
        Assert.That(result.DeaSchedule, Is.EqualTo("IV"));
        // Schedule IV can be refilled (only II/IIN are blocked)
        Assert.That(result.Reasons.Any(r => r.Contains("Schedule II")), Is.False);
    }

    // ─── RefillAsync Enhanced Guards ─────────────────────────────────────────

    [Test]
    public async Task Refill_DeaScheduleII_Throws()
    {
        IPharmacyGrain grain = await CreateVerifiedFilledRx(
            isControlled: true, deaSchedule: "II", daysAgoFilled: 25);

        Assert.ThrowsAsync<InvalidOperationException>(
            () => grain.RefillAsync(DateTime.UtcNow));
    }

    [Test]
    public async Task Refill_TooEarly_Throws()
    {
        IPharmacyGrain grain = await CreateVerifiedFilledRx(daysSupply: 30, daysAgoFilled: 5);

        Assert.ThrowsAsync<InvalidOperationException>(
            () => grain.RefillAsync(DateTime.UtcNow));
    }

    [Test]
    public async Task Refill_After75PercentConsumed_Succeeds()
    {
        IPharmacyGrain grain = await CreateVerifiedFilledRx(daysSupply: 30, daysAgoFilled: 23);

        Assert.DoesNotThrowAsync(() => grain.RefillAsync(DateTime.UtcNow));

        PharmacyState state = await grain.GetPrescriptionAsync();
        Assert.That(state.RefillsRemaining, Is.EqualTo(4));
    }

    [Test]
    public async Task CheckEligibility_MultipleReasons_AllReturned()
    {
        // Create expired + no refills + DEA II → should have 3 reasons
        IPharmacyGrain grain = await CreateVerifiedFilledRx(
            refills: 0, daysSupply: 5, daysAgoFilled: 20,
            isControlled: true, deaSchedule: "II");

        RefillEligibilityResult result = await grain.CheckRefillEligibilityAsync(DateTime.UtcNow);

        Assert.That(result.IsEligible, Is.False);
        Assert.That(result.Reasons.Count, Is.GreaterThanOrEqualTo(3));
    }
}
