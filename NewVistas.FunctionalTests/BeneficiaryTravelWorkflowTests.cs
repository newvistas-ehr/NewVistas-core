// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NUnit.Framework;
using Orleans.Hosting;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

[TestFixture]
public class BeneficiaryTravelWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ─── Claim Creation ──────────────────────────────────────────────────────

    [Test]
    public async Task ClaimGrain_CanCreateMileageClaim()
    {
        string claimId = $"BT-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IBeneficiaryTravelClaimGrain>($"DGBT:{claimId}");
        await grain.CreateClaimAsync("PAT-001", "DOE,JOHN", DateTime.UtcNow, "MILEAGE",
            25.0m, true, "POV", "123 MAIN ST", "VA MEDICAL CENTER", null, "SC", false);

        BeneficiaryTravelClaimState state = await grain.GetClaimAsync();
        Assert.That(state.Status, Is.EqualTo("PENDING"));
        Assert.That(state.TotalMileage, Is.EqualTo(50.0m));
        Assert.That(state.ClaimType, Is.EqualTo("MILEAGE"));
    }

    [Test]
    public async Task ClaimGrain_CalculatesRoundTripCorrectly()
    {
        string claimId = $"BT-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IBeneficiaryTravelClaimGrain>($"DGBT:{claimId}");
        await grain.CreateClaimAsync("PAT-001", "DOE,JOHN", DateTime.UtcNow, "MILEAGE",
            30.0m, true, "POV", null, null, null, "SC", false);

        BeneficiaryTravelClaimState state = await grain.GetClaimAsync();
        Assert.That(state.TotalMileage, Is.EqualTo(60.0m));
        // Gross = 60 * 0.415 = 24.90, Net = 24.90 - (2 * 3.00) = 18.90
        Assert.That(state.GrossAmount, Is.EqualTo(24.90m));
        Assert.That(state.NetAmount, Is.EqualTo(18.90m));
        Assert.That(state.DeductiblesApplied, Is.EqualTo(2));
    }

    [Test]
    public async Task ClaimGrain_OneWayHasSingleDeductible()
    {
        string claimId = $"BT-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IBeneficiaryTravelClaimGrain>($"DGBT:{claimId}");
        await grain.CreateClaimAsync("PAT-001", "DOE,JOHN", DateTime.UtcNow, "MILEAGE",
            30.0m, false, "POV", null, null, null, "SC", false);

        BeneficiaryTravelClaimState state = await grain.GetClaimAsync();
        Assert.That(state.TotalMileage, Is.EqualTo(30.0m));
        // Gross = 30 * 0.415 = 12.45, Net = 12.45 - 3.00 = 9.45
        Assert.That(state.NetAmount, Is.EqualTo(9.45m));
        Assert.That(state.DeductiblesApplied, Is.EqualTo(1));
    }

    [Test]
    public async Task ClaimGrain_DeductibleExemptGetsFullAmount()
    {
        string claimId = $"BT-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IBeneficiaryTravelClaimGrain>($"DGBT:{claimId}");
        await grain.CreateClaimAsync("PAT-001", "DOE,JOHN", DateTime.UtcNow, "MILEAGE",
            25.0m, true, "POV", null, null, null, "SC", true);

        BeneficiaryTravelClaimState state = await grain.GetClaimAsync();
        Assert.That(state.DeductiblesApplied, Is.EqualTo(0));
        Assert.That(state.NetAmount, Is.EqualTo(state.GrossAmount));
    }

    [Test]
    public async Task ClaimGrain_SmallMileageNetNeverNegative()
    {
        string claimId = $"BT-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IBeneficiaryTravelClaimGrain>($"DGBT:{claimId}");
        // 2 miles round trip = 4 miles * 0.415 = 1.66, minus 6.00 deductible = should be 0 not negative
        await grain.CreateClaimAsync("PAT-001", "DOE,JOHN", DateTime.UtcNow, "MILEAGE",
            2.0m, true, "POV", null, null, null, "NSC", false);

        BeneficiaryTravelClaimState state = await grain.GetClaimAsync();
        Assert.That(state.NetAmount, Is.EqualTo(0m));
    }

    // ─── Status Transitions ──────────────────────────────────────────────────

    [Test]
    public async Task ClaimGrain_CanApprove()
    {
        string claimId = $"BT-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IBeneficiaryTravelClaimGrain>($"DGBT:{claimId}");
        await grain.CreateClaimAsync("PAT-001", "DOE,JOHN", DateTime.UtcNow, "MILEAGE", 25.0m, true, "POV", null, null, null, "SC", false);
        await grain.ApproveAsync("CLERK-001");

        Assert.That((await grain.GetClaimAsync()).Status, Is.EqualTo("APPROVED"));
    }

    [Test]
    public async Task ClaimGrain_CanDeny()
    {
        string claimId = $"BT-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IBeneficiaryTravelClaimGrain>($"DGBT:{claimId}");
        await grain.CreateClaimAsync("PAT-001", "DOE,JOHN", DateTime.UtcNow, "MILEAGE", 25.0m, true, "POV", null, null, null, "SC", false);
        await grain.DenyAsync("No eligible appointment", "CLERK-001");

        BeneficiaryTravelClaimState state = await grain.GetClaimAsync();
        Assert.That(state.Status, Is.EqualTo("DENIED"));
        Assert.That(state.DenialReason, Is.EqualTo("No eligible appointment"));
    }

    [Test]
    public async Task ClaimGrain_CanRecordPayment()
    {
        string claimId = $"BT-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IBeneficiaryTravelClaimGrain>($"DGBT:{claimId}");
        await grain.CreateClaimAsync("PAT-001", "DOE,JOHN", DateTime.UtcNow, "MILEAGE", 25.0m, true, "POV", null, null, null, "SC", false);
        await grain.ApproveAsync("CLERK-001");
        var payDate = DateTime.UtcNow;
        await grain.RecordPaymentAsync("EFT", payDate);

        BeneficiaryTravelClaimState state = await grain.GetClaimAsync();
        Assert.That(state.Status, Is.EqualTo("PAID"));
        Assert.That(state.PaymentMethod, Is.EqualTo("EFT"));
    }

    [Test]
    public async Task ClaimGrain_CanCancel()
    {
        string claimId = $"BT-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IBeneficiaryTravelClaimGrain>($"DGBT:{claimId}");
        await grain.CreateClaimAsync("PAT-001", "DOE,JOHN", DateTime.UtcNow, "MILEAGE", 25.0m, true, "POV", null, null, null, "SC", false);
        await grain.CancelAsync("Duplicate claim");

        Assert.That((await grain.GetClaimAsync()).Status, Is.EqualTo("CANCELLED"));
    }

    [Test]
    public async Task ClaimGrain_CanPlaceOnHold()
    {
        string claimId = $"BT-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IBeneficiaryTravelClaimGrain>($"DGBT:{claimId}");
        await grain.CreateClaimAsync("PAT-001", "DOE,JOHN", DateTime.UtcNow, "MILEAGE", 25.0m, true, "POV", null, null, null, "SC", false);
        await grain.PlaceOnHoldAsync("Awaiting eligibility verification");

        Assert.That((await grain.GetClaimAsync()).Status, Is.EqualTo("ON_HOLD"));
    }

    [Test]
    public async Task ClaimGrain_CanSetSpecialModeAuth()
    {
        string claimId = $"BT-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IBeneficiaryTravelClaimGrain>($"DGBT:{claimId}");
        await grain.CreateClaimAsync("PAT-001", "DOE,JOHN", DateTime.UtcNow, "SPECIAL_MODE", 0m, false, "AMBULANCE", null, null, null, "SC", true);
        await grain.SetSpecialModeAuthAsync("AUTH-12345");

        Assert.That((await grain.GetClaimAsync()).SpecialModeAuthNumber, Is.EqualTo("AUTH-12345"));
    }

    // ─── Index ───────────────────────────────────────────────────────────────

    [Test]
    public async Task Index_CanAddAndRetrieveClaims()
    {
        var index = _cluster.GrainFactory.GetGrain<IBeneficiaryTravelIndexGrain>($"DGBT-INDEX:PAT-{Guid.NewGuid():N}");
        await index.AddOrUpdateAsync(new BeneficiaryTravelClaimEntry { ClaimId = "C1", TravelDate = DateTime.UtcNow.AddDays(-5), Status = "PENDING", NetAmount = 15.00m });
        await index.AddOrUpdateAsync(new BeneficiaryTravelClaimEntry { ClaimId = "C2", TravelDate = DateTime.UtcNow, Status = "PAID", NetAmount = 20.00m });

        List<BeneficiaryTravelClaimEntry> all = await index.GetClaimsAsync();
        Assert.That(all, Has.Count.EqualTo(2));

        List<BeneficiaryTravelClaimEntry> pending = await index.GetClaimsByStatusAsync("PENDING");
        Assert.That(pending, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Index_CanUpdateExisting()
    {
        var index = _cluster.GrainFactory.GetGrain<IBeneficiaryTravelIndexGrain>($"DGBT-INDEX:PAT-{Guid.NewGuid():N}");
        await index.AddOrUpdateAsync(new BeneficiaryTravelClaimEntry { ClaimId = "CU", Status = "PENDING", NetAmount = 10.00m });
        await index.AddOrUpdateAsync(new BeneficiaryTravelClaimEntry { ClaimId = "CU", Status = "PAID", NetAmount = 10.00m });

        List<BeneficiaryTravelClaimEntry> all = await index.GetClaimsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Status, Is.EqualTo("PAID"));
    }

    // ─── Full Lifecycle ──────────────────────────────────────────────────────

    [Test]
    public async Task FullLifecycle_PendingToApprovedToPaid()
    {
        string claimId = $"BT-{Guid.NewGuid():N}";
        var grain = _cluster.GrainFactory.GetGrain<IBeneficiaryTravelClaimGrain>($"DGBT:{claimId}");

        await grain.CreateClaimAsync("PAT-LC", "SMITH,ANN", DateTime.UtcNow, "MILEAGE",
            40.0m, true, "POV", "456 OAK AVE", "VA MEDICAL CENTER", "APPT-100", "SC", false);

        BeneficiaryTravelClaimState s1 = await grain.GetClaimAsync();
        Assert.That(s1.Status, Is.EqualTo("PENDING"));
        Assert.That(s1.TotalMileage, Is.EqualTo(80.0m));

        await grain.ApproveAsync("CLERK-002");
        Assert.That((await grain.GetClaimAsync()).Status, Is.EqualTo("APPROVED"));

        await grain.RecordPaymentAsync("CHECK", DateTime.UtcNow);
        BeneficiaryTravelClaimState final = await grain.GetClaimAsync();
        Assert.That(final.Status, Is.EqualTo("PAID"));
        Assert.That(final.PaymentMethod, Is.EqualTo("CHECK"));
    }
}
