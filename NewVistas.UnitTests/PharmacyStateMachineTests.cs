// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for PharmacyGrain state machine guards.
/// Validates that lifecycle methods enforce correct sequencing at the grain level.
/// Follows the AutoRefillGrain pattern of throwing InvalidOperationException.
///
/// VistA reference: PSOORED.m enforced sequence.
/// </summary>
[TestFixture]
public class PharmacyStateMachineTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private async Task<IPharmacyGrain> CreateRx(int refills = 5, int daysSupply = 30)
    {
        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain grain = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await grain.CreatePrescriptionAsync("P-001", "TEST DRUG", null, null, null, null, null,
            daysSupply, 30, refills, null, null, null, null, null, null);
        return grain;
    }

    // ─── Fill Guards ────────────────────────────────────────────────────────

    [Test]
    public async Task Fill_WhenNotActive_Throws()
    {
        IPharmacyGrain grain = await CreateRx();
        await grain.DiscontinueAsync("Testing");

        Assert.ThrowsAsync<InvalidOperationException>(
            () => grain.FillPrescriptionAsync(DateTime.UtcNow));
    }

    [Test]
    public async Task Fill_WhenNotVerified_Throws()
    {
        IPharmacyGrain grain = await CreateRx();

        Assert.ThrowsAsync<InvalidOperationException>(
            () => grain.FillPrescriptionAsync(DateTime.UtcNow));
    }

    [Test]
    public async Task Fill_WhenAlreadyFilled_Throws()
    {
        IPharmacyGrain grain = await CreateRx();
        await grain.VerifyAsync("RPH-001");
        await grain.FillPrescriptionAsync(DateTime.UtcNow);

        Assert.ThrowsAsync<InvalidOperationException>(
            () => grain.FillPrescriptionAsync(DateTime.UtcNow));
    }

    [Test]
    public async Task Fill_WhenVerifiedAndActive_Succeeds()
    {
        IPharmacyGrain grain = await CreateRx();
        await grain.VerifyAsync("RPH-001");

        Assert.DoesNotThrowAsync(() => grain.FillPrescriptionAsync(DateTime.UtcNow));

        PharmacyState state = await grain.GetPrescriptionAsync();
        Assert.That(state.FillDate, Is.Not.Null);
    }

    // ─── Refill Guards ──────────────────────────────────────────────────────

    [Test]
    public async Task Refill_WhenNotActive_Throws()
    {
        IPharmacyGrain grain = await CreateRx();
        await grain.VerifyAsync("RPH-001");
        await grain.FillPrescriptionAsync(DateTime.UtcNow.AddDays(-30));
        await grain.PlaceOnHoldAsync("Testing");

        Assert.ThrowsAsync<InvalidOperationException>(
            () => grain.RefillAsync(DateTime.UtcNow));
    }

    [Test]
    public async Task Refill_WhenNoFillDate_Throws()
    {
        IPharmacyGrain grain = await CreateRx();
        await grain.VerifyAsync("RPH-001");

        Assert.ThrowsAsync<InvalidOperationException>(
            () => grain.RefillAsync(DateTime.UtcNow));
    }

    [Test]
    public async Task Refill_WhenNoRefillsRemaining_Throws()
    {
        IPharmacyGrain grain = await CreateRx(refills: 0);
        await grain.VerifyAsync("RPH-001");
        await grain.FillPrescriptionAsync(DateTime.UtcNow.AddDays(-30));

        Assert.ThrowsAsync<InvalidOperationException>(
            () => grain.RefillAsync(DateTime.UtcNow));
    }

    [Test]
    public async Task Refill_WhenExpired_Throws()
    {
        IPharmacyGrain grain = await CreateRx(daysSupply: 10);
        await grain.VerifyAsync("RPH-001");
        await grain.FillPrescriptionAsync(DateTime.UtcNow.AddDays(-30));

        // Expiration was 20 days ago (fillDate + 10 days supply)
        Assert.ThrowsAsync<InvalidOperationException>(
            () => grain.RefillAsync(DateTime.UtcNow));
    }

    // ─── Verify Guards ──────────────────────────────────────────────────────

    [Test]
    public async Task Verify_WhenNotActive_Throws()
    {
        IPharmacyGrain grain = await CreateRx();
        await grain.PlaceOnHoldAsync("Testing");

        Assert.ThrowsAsync<InvalidOperationException>(
            () => grain.VerifyAsync("RPH-001"));
    }

    [Test]
    public async Task Verify_WhenAlreadyVerified_Throws()
    {
        IPharmacyGrain grain = await CreateRx();
        await grain.VerifyAsync("RPH-001");

        Assert.ThrowsAsync<InvalidOperationException>(
            () => grain.VerifyAsync("RPH-002"));
    }

    // ─── Hold/Resume Guards ─────────────────────────────────────────────────

    [Test]
    public async Task Hold_WhenNotActive_Throws()
    {
        IPharmacyGrain grain = await CreateRx();
        await grain.DiscontinueAsync("Testing");

        Assert.ThrowsAsync<InvalidOperationException>(
            () => grain.PlaceOnHoldAsync("Should fail"));
    }

    [Test]
    public async Task Resume_WhenNotOnHold_Throws()
    {
        IPharmacyGrain grain = await CreateRx();

        Assert.ThrowsAsync<InvalidOperationException>(
            () => grain.ResumeAsync());
    }

    // ─── Discontinue Guards ─────────────────────────────────────────────────

    [Test]
    public async Task Discontinue_WhenAlreadyDiscontinued_Throws()
    {
        IPharmacyGrain grain = await CreateRx();
        await grain.DiscontinueAsync("First time");

        Assert.ThrowsAsync<InvalidOperationException>(
            () => grain.DiscontinueAsync("Second time"));
    }

    // ─── Expire Guards ──────────────────────────────────────────────────────

    [Test]
    public async Task Expire_WhenDiscontinued_Throws()
    {
        IPharmacyGrain grain = await CreateRx();
        await grain.DiscontinueAsync("Testing");

        Assert.ThrowsAsync<InvalidOperationException>(
            () => grain.ExpireAsync());
    }

    // ─── PrintLabel Guards ──────────────────────────────────────────────────

    [Test]
    public async Task PrintLabel_WhenNotVerified_Throws()
    {
        IPharmacyGrain grain = await CreateRx();

        Assert.ThrowsAsync<InvalidOperationException>(
            () => grain.PrintLabelAsync("RX001"));
    }

    // ─── Happy Path ─────────────────────────────────────────────────────────

    [Test]
    public async Task FullWorkflow_Create_Verify_Label_Fill_Refill_Succeeds()
    {
        IPharmacyGrain grain = await CreateRx(refills: 3, daysSupply: 30);

        // Verify
        await grain.VerifyAsync("RPH-001");
        PharmacyState state = await grain.GetPrescriptionAsync();
        Assert.That(state.IsVerified, Is.True);

        // Print label
        await grain.PrintLabelAsync("RX2026001");
        state = await grain.GetPrescriptionAsync();
        Assert.That(state.IsLabelPrinted, Is.True);

        // Fill (initial) — date must allow 75% consumed before refill
        await grain.FillPrescriptionAsync(DateTime.UtcNow.AddDays(-25));
        state = await grain.GetPrescriptionAsync();
        Assert.That(state.FillDate, Is.Not.Null);
        Assert.That(state.RefillHistory, Has.Count.EqualTo(1));

        // Refill
        await grain.RefillAsync(DateTime.UtcNow);
        state = await grain.GetPrescriptionAsync();
        Assert.That(state.RefillsRemaining, Is.EqualTo(2));
        Assert.That(state.RefillHistory, Has.Count.EqualTo(2));
    }
}
