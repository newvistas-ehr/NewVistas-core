// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

[TestFixture]
public class OutpatientPharmacyWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // -------------------------------------------------------------------
    // 1. Create — default field values
    // -------------------------------------------------------------------
    [Test]
    public async Task PharmacyGrain_CreatePrescription_InitialisesDefaultFields()
    {
        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain grain = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);

        await grain.CreatePrescriptionAsync(
            "P-001", "LISINOPRIL 10MG TAB", null,
            "10MG", "ORAL", "DAILY", "TAKE ONE TABLET DAILY",
            90, 90, 5, "PROV-001", "DR. SMITH",
            "PHARM-01", "MAIN PHARMACY", null, null);

        PharmacyState state = await grain.GetPrescriptionAsync();

        Assert.That(state.Status, Is.EqualTo("ACTIVE"));
        Assert.That(state.Priority, Is.EqualTo("ROUTINE"));
        Assert.That(state.IsVerified, Is.False);
        Assert.That(state.IsLabelPrinted, Is.False);
        Assert.That(state.CounselingRequired, Is.False);
        Assert.That(state.RefillsRemaining, Is.EqualTo(5));
    }

    // -------------------------------------------------------------------
    // 2. Fill — sets ExpirationDate
    // -------------------------------------------------------------------
    [Test]
    public async Task PharmacyGrain_FillPrescription_SetsExpirationDate()
    {
        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain grain = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await grain.CreatePrescriptionAsync("P-001", "METFORMIN 500MG", null, null, null, null, null,
            30, 60, 11, null, null, null, null, null, null);

        await grain.VerifyAsync("RPH-001");

        DateTime fillDate = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);
        await grain.FillPrescriptionAsync(fillDate);

        PharmacyState state = await grain.GetPrescriptionAsync();
        Assert.That(state.FillDate, Is.EqualTo(fillDate));
        Assert.That(state.ExpirationDate, Is.EqualTo(fillDate.AddDays(30)));
    }

    // -------------------------------------------------------------------
    // 3. Fill — appends RefillRecord with FillNumber=0
    // -------------------------------------------------------------------
    [Test]
    public async Task PharmacyGrain_FillPrescription_AppendsRefillRecord()
    {
        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain grain = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await grain.CreatePrescriptionAsync("P-001", "ATORVASTATIN 40MG", null, null, null, null, null,
            90, 90, 3, null, null, null, null, null, null);

        await grain.VerifyAsync("RPH-001");
        await grain.FillPrescriptionAsync(DateTime.UtcNow);

        List<RefillRecord> history = await grain.GetRefillHistoryAsync();
        Assert.That(history, Has.Count.EqualTo(1));
        Assert.That(history[0].FillNumber, Is.EqualTo(0));
    }

    // -------------------------------------------------------------------
    // 4. Refill — decrements RefillsRemaining
    // -------------------------------------------------------------------
    [Test]
    public async Task PharmacyGrain_Refill_DecrementsRefillsRemaining()
    {
        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain grain = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await grain.CreatePrescriptionAsync("P-001", "OMEPRAZOLE 20MG", null, null, null, null, null,
            30, 30, 5, null, null, null, null, null, null);

        await grain.VerifyAsync("RPH-001");
        DateTime baseDate = DateTime.UtcNow.Date;
        await grain.FillPrescriptionAsync(baseDate.AddDays(-56));
        await grain.RefillAsync(baseDate.AddDays(-28));
        await grain.RefillAsync(baseDate);

        PharmacyState state = await grain.GetPrescriptionAsync();
        Assert.That(state.RefillsRemaining, Is.EqualTo(3));
    }

    // -------------------------------------------------------------------
    // 5. Refill — appends fill records with incrementing FillNumbers
    // -------------------------------------------------------------------
    [Test]
    public async Task PharmacyGrain_Refill_AppendsFillRecord()
    {
        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain grain = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await grain.CreatePrescriptionAsync("P-001", "AMLODIPINE 5MG", null, null, null, null, null,
            30, 30, 3, null, null, null, null, null, null);

        await grain.VerifyAsync("RPH-001");
        DateTime baseDate = DateTime.UtcNow.Date;
        await grain.FillPrescriptionAsync(baseDate.AddDays(-28));
        await grain.RefillAsync(baseDate);

        List<RefillRecord> history = await grain.GetRefillHistoryAsync();
        Assert.That(history, Has.Count.EqualTo(2));
        Assert.That(history[0].FillNumber, Is.EqualTo(0));
        Assert.That(history[1].FillNumber, Is.EqualTo(1));
    }

    // -------------------------------------------------------------------
    // 6. Discontinue — uses DiscontinueReason, not Comments
    // -------------------------------------------------------------------
    [Test]
    public async Task PharmacyGrain_Discontinue_UsesDiscontinueReasonField_NotComments()
    {
        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain grain = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await grain.CreatePrescriptionAsync("P-001", "WARFARIN 5MG", null, null, null, null, null,
            30, 30, 0, null, null, null, null, null, "Initial comments");

        await grain.DiscontinueAsync("Drug allergy identified");

        PharmacyState state = await grain.GetPrescriptionAsync();
        Assert.That(state.Status, Is.EqualTo("DISCONTINUED"));
        Assert.That(state.DiscontinueReason, Is.EqualTo("Drug allergy identified"));
        Assert.That(state.Comments, Is.EqualTo("Initial comments")); // Comments not overwritten
    }

    // -------------------------------------------------------------------
    // 7. Hold — uses HoldReason, not Comments
    // -------------------------------------------------------------------
    [Test]
    public async Task PharmacyGrain_Hold_UsesHoldReasonField_NotComments()
    {
        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain grain = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await grain.CreatePrescriptionAsync("P-001", "CARVEDILOL 12.5MG", null, null, null, null, null,
            30, 30, 3, null, null, null, null, null, "Provider notes");

        await grain.PlaceOnHoldAsync("Patient hospitalised");

        PharmacyState state = await grain.GetPrescriptionAsync();
        Assert.That(state.Status, Is.EqualTo("HOLD"));
        Assert.That(state.HoldReason, Is.EqualTo("Patient hospitalised"));
        Assert.That(state.HoldDate, Is.Not.Null);
        Assert.That(state.Comments, Is.EqualTo("Provider notes")); // Comments not overwritten
    }

    // -------------------------------------------------------------------
    // 8. Resume — clears hold status
    // -------------------------------------------------------------------
    [Test]
    public async Task PharmacyGrain_Resume_ClearsHoldStatus()
    {
        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain grain = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await grain.CreatePrescriptionAsync("P-001", "FUROSEMIDE 40MG", null, null, null, null, null,
            30, 30, 2, null, null, null, null, null, null);

        await grain.PlaceOnHoldAsync("Pending labs");
        await grain.ResumeAsync();

        PharmacyState state = await grain.GetPrescriptionAsync();
        Assert.That(state.Status, Is.EqualTo("ACTIVE"));
    }

    // -------------------------------------------------------------------
    // 9. Verify — sets verified fields
    // -------------------------------------------------------------------
    [Test]
    public async Task PharmacyGrain_Verify_SetsVerifiedFields()
    {
        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain grain = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await grain.CreatePrescriptionAsync("P-001", "SIMVASTATIN 20MG", null, null, null, null, null,
            30, 30, 5, null, null, null, null, null, null);

        await grain.VerifyAsync("RPH-007");

        PharmacyState state = await grain.GetPrescriptionAsync();
        Assert.That(state.IsVerified, Is.True);
        Assert.That(state.VerifiedBy, Is.EqualTo("RPH-007"));
        Assert.That(state.VerifiedDate, Is.Not.Null);
    }

    // -------------------------------------------------------------------
    // 10. PrintLabel — sets IsLabelPrinted and LabelPrintedDate
    // -------------------------------------------------------------------
    [Test]
    public async Task PharmacyGrain_PrintLabel_SetsIsLabelPrintedAndDate()
    {
        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain grain = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await grain.CreatePrescriptionAsync("P-001", "HYDROCHLOROTHIAZIDE 25MG", null, null, null, null, null,
            90, 90, 5, null, null, null, null, null, null);

        await grain.VerifyAsync("RPH-001");
        await grain.PrintLabelAsync("RX20260101001");

        PharmacyState state = await grain.GetPrescriptionAsync();
        Assert.That(state.IsLabelPrinted, Is.True);
        Assert.That(state.LabelPrintedDate, Is.Not.Null);
        Assert.That(state.RxNumber, Is.EqualTo("RX20260101001"));
    }

    // -------------------------------------------------------------------
    // 11. SetCounseling — persists flag
    // -------------------------------------------------------------------
    [Test]
    public async Task PharmacyGrain_SetCounseling_PersistsFlag()
    {
        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain grain = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await grain.CreatePrescriptionAsync("P-001", "ISOTRETINOIN 20MG", null, null, null, null, null,
            30, 30, 0, null, null, null, null, null, null);

        await grain.SetCounselingFlagAsync(true);
        PharmacyState on = await grain.GetPrescriptionAsync();
        Assert.That(on.CounselingRequired, Is.True);

        await grain.SetCounselingFlagAsync(false);
        PharmacyState off = await grain.GetPrescriptionAsync();
        Assert.That(off.CounselingRequired, Is.False);
    }

    // -------------------------------------------------------------------
    // 12. UpdatePriority — accepts ROUTINE, URGENT, STAT; rejects others
    // -------------------------------------------------------------------
    [Test]
    public async Task PharmacyGrain_UpdatePriority_AcceptsRoutineUrgentStat()
    {
        string rxId = $"RX-{Guid.NewGuid()}";
        IPharmacyGrain grain = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        await grain.CreatePrescriptionAsync("P-001", "CEFTRIAXONE 1GM", null, null, null, null, null,
            7, 7, 0, null, null, null, null, null, null);

        await grain.UpdatePriorityAsync("STAT");
        PharmacyState state = await grain.GetPrescriptionAsync();
        Assert.That(state.Priority, Is.EqualTo("STAT"));

        await grain.UpdatePriorityAsync("ROUTINE");
        state = await grain.GetPrescriptionAsync();
        Assert.That(state.Priority, Is.EqualTo("ROUTINE"));

        Assert.ThrowsAsync<ArgumentException>(() => grain.UpdatePriorityAsync("INVALID"));
    }

    // -------------------------------------------------------------------
    // 13. Index — AddEntry appears in GetAll
    // -------------------------------------------------------------------
    [Test]
    public async Task PrescriptionIndex_AddEntry_AppearsInGetAll()
    {
        string patientId = $"P-{Guid.NewGuid()}";
        IPatientPrescriptionIndexGrain index =
            _cluster.GrainFactory.GetGrain<IPatientPrescriptionIndexGrain>($"PSO-INDEX:{patientId}");

        PrescriptionIndexEntry entry = new()
        {
            PrescriptionId = $"RX-{Guid.NewGuid()}",
            DrugName = "ASPIRIN 81MG",
            Status = "ACTIVE",
            Priority = "ROUTINE"
        };

        await index.AddOrUpdateEntryAsync(entry);
        List<PrescriptionIndexEntry> all = await index.GetAllAsync();

        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].DrugName, Is.EqualTo("ASPIRIN 81MG"));
    }

    // -------------------------------------------------------------------
    // 14. Index — GetActive filters correctly
    // -------------------------------------------------------------------
    [Test]
    public async Task PrescriptionIndex_GetActive_FiltersCorrectly()
    {
        string patientId = $"P-{Guid.NewGuid()}";
        IPatientPrescriptionIndexGrain index =
            _cluster.GrainFactory.GetGrain<IPatientPrescriptionIndexGrain>($"PSO-INDEX:{patientId}");

        await index.AddOrUpdateEntryAsync(new PrescriptionIndexEntry
            { PrescriptionId = "RX-A", DrugName = "DRUG-A", Status = "ACTIVE", Priority = "ROUTINE" });
        await index.AddOrUpdateEntryAsync(new PrescriptionIndexEntry
            { PrescriptionId = "RX-B", DrugName = "DRUG-B", Status = "DISCONTINUED", Priority = "ROUTINE" });
        await index.AddOrUpdateEntryAsync(new PrescriptionIndexEntry
            { PrescriptionId = "RX-C", DrugName = "DRUG-C", Status = "ACTIVE", Priority = "URGENT" });

        List<PrescriptionIndexEntry> active = await index.GetActiveAsync();

        Assert.That(active, Has.Count.EqualTo(2));
        Assert.That(active.Select(e => e.PrescriptionId), Does.Not.Contain("RX-B"));
    }

    // -------------------------------------------------------------------
    // 15. Index — AddOrUpdate replaces existing entry
    // -------------------------------------------------------------------
    [Test]
    public async Task PrescriptionIndex_AddOrUpdate_ReplacesExistingEntry()
    {
        string patientId = $"P-{Guid.NewGuid()}";
        IPatientPrescriptionIndexGrain index =
            _cluster.GrainFactory.GetGrain<IPatientPrescriptionIndexGrain>($"PSO-INDEX:{patientId}");

        string rxId = $"RX-{Guid.NewGuid()}";

        await index.AddOrUpdateEntryAsync(new PrescriptionIndexEntry
            { PrescriptionId = rxId, DrugName = "OLD DRUG", Status = "ACTIVE", Priority = "ROUTINE" });
        await index.AddOrUpdateEntryAsync(new PrescriptionIndexEntry
            { PrescriptionId = rxId, DrugName = "OLD DRUG", Status = "DISCONTINUED", Priority = "ROUTINE" });

        List<PrescriptionIndexEntry> all = await index.GetAllAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Status, Is.EqualTo("DISCONTINUED"));
    }

    // -------------------------------------------------------------------
    // 16. End-to-end: Create → Fill → Refill → Verify workflow
    // -------------------------------------------------------------------
    [Test]
    public async Task Workflow_CreateFillRefillVerify_EndToEnd()
    {
        string patientId = $"P-{Guid.NewGuid()}";
        string rxId = $"RX-{Guid.NewGuid()}";

        IPharmacyGrain rxGrain = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        IPatientPrescriptionIndexGrain indexGrain =
            _cluster.GrainFactory.GetGrain<IPatientPrescriptionIndexGrain>($"PSO-INDEX:{patientId}");

        // Create
        await rxGrain.CreatePrescriptionAsync(
            patientId, "LISINOPRIL 10MG TAB", "50-001",
            "10MG", "ORAL", "DAILY", "TAKE ONE TABLET DAILY FOR BLOOD PRESSURE",
            90, 90, 5, "PROV-001", "DR. SMITH",
            "PHARM-001", "MAIN PHARMACY", null, null);

        PharmacyState state = await rxGrain.GetPrescriptionAsync();
        await indexGrain.AddOrUpdateEntryAsync(BuildEntry(rxId, state));

        // Verify (must come before Fill per PSOORED.m workflow)
        await rxGrain.VerifyAsync("RPH-007");
        state = await rxGrain.GetPrescriptionAsync();
        await indexGrain.AddOrUpdateEntryAsync(BuildEntry(rxId, state));

        // Fill
        await rxGrain.FillPrescriptionAsync(DateTime.UtcNow.Date.AddDays(-70));
        state = await rxGrain.GetPrescriptionAsync();
        await indexGrain.AddOrUpdateEntryAsync(BuildEntry(rxId, state));

        // Print label
        await rxGrain.PrintLabelAsync("RX2026001001");
        state = await rxGrain.GetPrescriptionAsync();
        await indexGrain.AddOrUpdateEntryAsync(BuildEntry(rxId, state));

        // Refill
        await rxGrain.RefillAsync(DateTime.UtcNow);
        state = await rxGrain.GetPrescriptionAsync();
        await indexGrain.AddOrUpdateEntryAsync(BuildEntry(rxId, state));

        // Assert grain state
        Assert.That(state.IsVerified, Is.True);
        Assert.That(state.IsLabelPrinted, Is.True);
        Assert.That(state.RxNumber, Is.EqualTo("RX2026001001"));
        Assert.That(state.RefillsRemaining, Is.EqualTo(4));

        List<RefillRecord> history = await rxGrain.GetRefillHistoryAsync();
        Assert.That(history, Has.Count.EqualTo(2)); // original fill + 1 refill
        Assert.That(history[1].FillNumber, Is.EqualTo(1));

        // Assert index
        List<PrescriptionIndexEntry> active = await indexGrain.GetActiveAsync();
        Assert.That(active, Has.Count.EqualTo(1));
        Assert.That(active[0].IsVerified, Is.True);
        Assert.That(active[0].RefillsRemaining, Is.EqualTo(4));
    }

    private static PrescriptionIndexEntry BuildEntry(string rxId, PharmacyState s) =>
        new()
        {
            PrescriptionId = rxId,
            DrugName = s.DrugName,
            DrugId = s.DrugId,
            Status = s.Status,
            IssueDate = s.IssueDate,
            ExpirationDate = s.ExpirationDate,
            Refills = s.Refills,
            RefillsRemaining = s.RefillsRemaining,
            Priority = s.Priority,
            IsVerified = s.IsVerified,
            CounselingRequired = s.CounselingRequired,
            ProviderId = s.ProviderId,
            ProviderName = s.ProviderName,
            Dosage = s.Dosage
        };
}
