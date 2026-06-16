// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for medium-priority pharmacy gaps:
/// 1. NDC/Lot Tracking at Dispense
/// 2. Patient Counseling Workflow
/// 3. Label Generation
/// 4. Incomplete/Pending DUR Status (MedStatusGroup integration)
///
/// All tests exercise grain-level behavior directly.
/// VistA reference: PSO dispense recording, PSOCP.m, PSJLBL.m.
/// </summary>
[TestFixture]
public class PharmacyMediumPriorityTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private async Task<IPharmacyGrain> CreateVerifiedRx()
    {
        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain grain = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await grain.CreatePrescriptionAsync("P-001", "METFORMIN 500MG", "NDC-12345",
            "500mg", "ORAL", "BID", "Take one tablet twice daily with meals",
            30, 60, 5, "PROV-001", "Dr. Smith", "PHARM-001", "Main Pharmacy", null, null);
        await grain.VerifyAsync("RPH-001");
        return grain;
    }

    private async Task<IPharmacyGrain> CreateVerifiedFilledRx()
    {
        IPharmacyGrain grain = await CreateVerifiedRx();
        await grain.FillPrescriptionAsync(DateTime.UtcNow.Date.AddDays(-5));
        return grain;
    }

    // ═══ NDC/LOT TRACKING ═══════════════════════════════════════════════════

    [Test]
    public async Task RecordDispense_SetsNdcAndLotOnState()
    {
        IPharmacyGrain grain = await CreateVerifiedFilledRx();

        await grain.RecordDispenseAsync("00378-1805-01", "LOT-2026-A001", "RPH-002");

        PharmacyState state = await grain.GetPrescriptionAsync();
        Assert.That(state.NdcDispensed, Is.EqualTo("00378-1805-01"));
        Assert.That(state.LotNumber, Is.EqualTo("LOT-2026-A001"));
    }

    [Test]
    public async Task RecordDispense_UpdatesLatestRefillRecord()
    {
        IPharmacyGrain grain = await CreateVerifiedFilledRx();

        await grain.RecordDispenseAsync("00378-1805-01", "LOT-2026-A001", "RPH-002");

        List<RefillRecord> history = await grain.GetRefillHistoryAsync();
        Assert.That(history[^1].NdcDispensed, Is.EqualTo("00378-1805-01"));
        Assert.That(history[^1].PharmacistId, Is.EqualTo("RPH-002"));
    }

    [Test]
    public async Task RecordDispense_NullValues_ClearsFields()
    {
        IPharmacyGrain grain = await CreateVerifiedFilledRx();
        await grain.RecordDispenseAsync("NDC-OLD", "LOT-OLD", "RPH-001");
        await grain.RecordDispenseAsync(null, null, null);

        PharmacyState state = await grain.GetPrescriptionAsync();
        Assert.That(state.NdcDispensed, Is.Null);
        Assert.That(state.LotNumber, Is.Null);
    }

    // ═══ PATIENT COUNSELING ═════════════════════════════════════════════════

    [Test]
    public async Task RecordCounseling_SetsCompletionFields()
    {
        IPharmacyGrain grain = await CreateVerifiedRx();
        await grain.SetCounselingFlagAsync(true);

        await grain.RecordCounselingAsync("RPH-003", "Discussed dosage, side effects, and interactions.");

        PharmacyState state = await grain.GetPrescriptionAsync();
        Assert.That(state.CounselingCompleted, Is.True);
        Assert.That(state.CounseledBy, Is.EqualTo("RPH-003"));
        Assert.That(state.CounselingDate, Is.Not.Null);
        Assert.That(state.CounselingNotes, Does.Contain("side effects"));
    }

    [Test]
    public async Task RecordCounseling_WhenNotRequired_Throws()
    {
        IPharmacyGrain grain = await CreateVerifiedRx();
        // CounselingRequired defaults to false

        Assert.ThrowsAsync<InvalidOperationException>(
            () => grain.RecordCounselingAsync("RPH-001", "Notes"));
    }

    [Test]
    public async Task RecordCounseling_WhenAlreadyCompleted_Throws()
    {
        IPharmacyGrain grain = await CreateVerifiedRx();
        await grain.SetCounselingFlagAsync(true);
        await grain.RecordCounselingAsync("RPH-001", "First counseling.");

        Assert.ThrowsAsync<InvalidOperationException>(
            () => grain.RecordCounselingAsync("RPH-002", "Second attempt."));
    }

    // ═══ LABEL GENERATION ═══════════════════════════════════════════════════

    [Test]
    public async Task GenerateLabelContent_ReturnsAllFields()
    {
        IPharmacyGrain grain = await CreateVerifiedFilledRx();
        await grain.RecordDispenseAsync("00378-1805-01", "LOT-2026-A001", "RPH-002");
        await grain.PrintLabelAsync("RX2026001");

        PrescriptionLabelContent label = await grain.GenerateLabelContentAsync();

        Assert.That(label.RxNumber, Is.EqualTo("RX2026001"));
        Assert.That(label.DrugName, Is.EqualTo("METFORMIN 500MG"));
        Assert.That(label.Dosage, Is.EqualTo("500mg"));
        Assert.That(label.Route, Is.EqualTo("ORAL"));
        Assert.That(label.Schedule, Is.EqualTo("BID"));
        Assert.That(label.Sig, Is.EqualTo("Take one tablet twice daily with meals"));
        Assert.That(label.Quantity, Is.EqualTo(60));
        Assert.That(label.DaysSupply, Is.EqualTo(30));
        Assert.That(label.RefillsRemaining, Is.EqualTo(5));
        Assert.That(label.ProviderName, Is.EqualTo("Dr. Smith"));
        Assert.That(label.PharmacyName, Is.EqualTo("Main Pharmacy"));
        Assert.That(label.NdcDispensed, Is.EqualTo("00378-1805-01"));
        Assert.That(label.LotNumber, Is.EqualTo("LOT-2026-A001"));
        Assert.That(label.BarcodeData, Is.EqualTo("RX2026001"));
        Assert.That(label.FillNumber, Is.EqualTo(0));
    }

    [Test]
    public async Task GenerateLabelContent_WhenNotVerified_Throws()
    {
        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain grain = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await grain.CreatePrescriptionAsync("P-001", "DRUG", null, null, null, null, null,
            30, 30, 5, null, null, null, null, null, null);

        Assert.ThrowsAsync<InvalidOperationException>(
            () => grain.GenerateLabelContentAsync());
    }

    [Test]
    public async Task GenerateLabelContent_ControlledSubstance_IncludesDeaInfo()
    {
        IPharmacyGrain grain = await CreateVerifiedRx();
        await grain.SetDeaCheckResultAsync(true, "IV", true, null);

        PrescriptionLabelContent label = await grain.GenerateLabelContentAsync();

        Assert.That(label.IsControlledSubstance, Is.True);
        Assert.That(label.DeaSchedule, Is.EqualTo("IV"));
    }

    [Test]
    public async Task GenerateLabelContent_CounselingRequired_FlaggedOnLabel()
    {
        IPharmacyGrain grain = await CreateVerifiedRx();
        await grain.SetCounselingFlagAsync(true);

        PrescriptionLabelContent label = await grain.GenerateLabelContentAsync();

        Assert.That(label.CounselingRequired, Is.True);
    }

    [Test]
    public async Task GenerateLabelContent_AfterRefill_ShowsLatestFillNumber()
    {
        IPharmacyGrain grain = await CreateVerifiedRx();
        await grain.FillPrescriptionAsync(DateTime.UtcNow.Date.AddDays(-25));
        await grain.RefillAsync(DateTime.UtcNow.Date);

        PrescriptionLabelContent label = await grain.GenerateLabelContentAsync();

        Assert.That(label.FillNumber, Is.EqualTo(1));
        Assert.That(label.RefillsRemaining, Is.EqualTo(4));
    }

    // ═══ PENDING DUR STATUS (MedStatusGroup) ════════════════════════════════

    [Test]
    public async Task UpdateMedStatusGroup_ActiveRx_GroupZero()
    {
        IPharmacyGrain grain = await CreateVerifiedRx();
        await grain.UpdateMedStatusGroupAsync();

        PharmacyState state = await grain.GetPrescriptionAsync();
        Assert.That(state.MedStatusGroup, Is.EqualTo(0)); // MED_ACTIVE
    }

    [Test]
    public async Task UpdateMedStatusGroup_DiscontinuedRx_GroupTwo()
    {
        IPharmacyGrain grain = await CreateVerifiedRx();
        await grain.DiscontinueAsync("Testing");
        await grain.UpdateMedStatusGroupAsync();

        PharmacyState state = await grain.GetPrescriptionAsync();
        Assert.That(state.MedStatusGroup, Is.EqualTo(2)); // MED_NONACTIVE
    }

    [Test]
    public async Task UpdateMedStatusGroup_HoldRx_GroupZero()
    {
        IPharmacyGrain grain = await CreateVerifiedRx();
        await grain.PlaceOnHoldAsync("Testing");
        await grain.UpdateMedStatusGroupAsync();

        PharmacyState state = await grain.GetPrescriptionAsync();
        Assert.That(state.MedStatusGroup, Is.EqualTo(0)); // MED_ACTIVE (HOLD is still active group)
    }
}
