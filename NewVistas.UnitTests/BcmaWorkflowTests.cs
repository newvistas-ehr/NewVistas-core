// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Tests for the BCMA (Bar Code Medication Administration) subsystem.
/// Covers both the individual BcmaGrain administration records and the
/// per-patient PatientMARGrain (Medication Administration Record) index.
/// </summary>
[TestFixture]
public class BcmaWorkflowTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain NewWorkflow() =>
        _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>($"PATIENT-{Guid.NewGuid()}");

    private IPatientMARGrain MARGrain(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientMARGrain>($"BCMA-MAR:{patientId}");

    private IBcmaGrain BcmaGrain(string bcmaId) =>
        _cluster.GrainFactory.GetGrain<IBcmaGrain>(bcmaId);

    private static MarEntry MakeEntry(string orderId, bool isActive = true) => new()
    {
        OrderId = orderId,
        DrugName = "Metoprolol 25mg",
        Dosage = "25mg",
        Route = "PO",
        Schedule = "BID",
        OrderType = "UNIT_DOSE",
        Priority = "ROUTINE",
        WardId = "WARD-MED-3A",
        RoomBed = "301-A",
        ProviderName = "Dr. Test",
        StartDate = DateTime.UtcNow.AddDays(-1),
        ScheduledTimes = [DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(10)],
        IsActive = isActive
    };

    // ─── Test 1: Record standalone GIVEN administration ──────────────────

    [Test]
    public async Task Bcma_RecordStandaloneAdministration_GivenStatus()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        string bcmaId = await w.RecordMedicationAdministrationAsync(
            "Lisinopril 10mg", null, "10mg", "PO",
            "GIVEN", DateTime.UtcNow.AddHours(-1), DateTime.UtcNow,
            "RN-001", "Nurse Adams", null, null, null, null);

        Assert.That(bcmaId, Does.StartWith("BCMA-"));

        BcmaState state = await BcmaGrain(bcmaId).GetAdministrationAsync();
        Assert.That(state.DrugName, Is.EqualTo("Lisinopril 10mg"));
        Assert.That(state.ActionStatus, Is.EqualTo("GIVEN"));
        Assert.That(state.AdministeredByName, Is.EqualTo("Nurse Adams"));
    }

    // ─── Test 2: Record standalone NOT GIVEN with reason ─────────────────

    [Test]
    public async Task Bcma_RecordStandaloneAdministration_NotGivenStatus()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        string bcmaId = await w.RecordMedicationAdministrationAsync(
            "Morphine 4mg", null, "4mg", "IV",
            "NOT GIVEN", null, DateTime.UtcNow,
            "RN-002", "Nurse Brown", null, null, null, "Patient asleep");

        BcmaState state = await BcmaGrain(bcmaId).GetAdministrationAsync();
        Assert.That(state.ActionStatus, Is.EqualTo("NOT GIVEN"));
        Assert.That(state.Comments, Is.EqualTo("Patient asleep"));
    }

    // ─── Test 3: History returns all administrations ──────────────────────

    [Test]
    public async Task Bcma_GetHistory_ReturnsAllAdministrations()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        await w.RecordMedicationAdministrationAsync(
            "Aspirin 81mg", null, "81mg", "PO", "GIVEN",
            null, DateTime.UtcNow, null, null, null, null, null, null);
        await w.RecordMedicationAdministrationAsync(
            "Atorvastatin 40mg", null, "40mg", "PO", "GIVEN",
            null, DateTime.UtcNow, null, null, null, null, null, null);

        List<BcmaSummary> history = await w.GetMedicationAdministrationsAsync(50);
        Assert.That(history, Has.Count.EqualTo(2));
    }

    // ─── Test 4: History ordered most-recent first ────────────────────────

    [Test]
    public async Task Bcma_GetHistory_OrderedByRecency()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        // Record two administrations with explicit but different times
        await w.RecordMedicationAdministrationAsync(
            "Drug A", null, null, "PO", "GIVEN",
            null, DateTime.UtcNow.AddHours(-3), null, "Nurse A", null, null, null, null);
        await w.RecordMedicationAdministrationAsync(
            "Drug B", null, null, "PO", "GIVEN",
            null, DateTime.UtcNow.AddHours(-1), null, "Nurse B", null, null, null, null);

        List<BcmaSummary> history = await w.GetMedicationAdministrationsAsync(50);
        Assert.That(history[0].AdministeredByName, Is.EqualTo("Nurse B"));
        Assert.That(history[1].AdministeredByName, Is.EqualTo("Nurse A"));
    }

    // ─── Test 5: MAR is empty for a new patient ───────────────────────────

    [Test]
    public async Task Bcma_MAR_EmptyOnNewPatient()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        List<MarEntry> mar = await MARGrain(patientId).GetMARAsync();
        Assert.That(mar, Is.Empty);
    }

    // ─── Test 6: Add entry — appears in active MAR ────────────────────────

    [Test]
    public async Task Bcma_MAR_AddEntry_AppearsInActiveMAR()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        string orderId = $"PSJ-{Guid.NewGuid()}";
        IPatientMARGrain mar = MARGrain(patientId);

        await mar.AddOrUpdateEntryAsync(MakeEntry(orderId));

        List<MarEntry> active = await mar.GetActiveMARAsync();
        Assert.That(active, Has.Count.EqualTo(1));
        Assert.That(active[0].OrderId, Is.EqualTo(orderId));
        Assert.That(active[0].DrugName, Is.EqualTo("Metoprolol 25mg"));
    }

    // ─── Test 7: Administer updates last admin fields ─────────────────────

    [Test]
    public async Task Bcma_MAR_RecordAdministration_UpdatesLastAdminFields()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        string orderId = $"PSJ-{Guid.NewGuid()}";
        string bcmaId = $"BCMA-{Guid.NewGuid()}";
        IPatientMARGrain mar = MARGrain(patientId);

        await mar.AddOrUpdateEntryAsync(MakeEntry(orderId));

        DateTime adminTime = DateTime.UtcNow;
        await mar.RecordAdministrationAsync(orderId, bcmaId, "GIVEN", adminTime, "Nurse Chen");

        List<MarEntry> entries = await mar.GetMARAsync();
        MarEntry entry = entries.First(e => e.OrderId == orderId);

        Assert.That(entry.LastAdminStatus, Is.EqualTo("GIVEN"));
        Assert.That(entry.LastAdminBy, Is.EqualTo("Nurse Chen"));
        Assert.That(entry.LastAdminDateTime, Is.EqualTo(adminTime).Within(TimeSpan.FromSeconds(1)));
    }

    // ─── Test 8: Recording administration increments count ───────────────

    [Test]
    public async Task Bcma_MAR_RecordAdministration_IncrementsTotalCount()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        string orderId = $"PSJ-{Guid.NewGuid()}";
        IPatientMARGrain mar = MARGrain(patientId);

        await mar.AddOrUpdateEntryAsync(MakeEntry(orderId));
        Assert.That((await mar.GetMARAsync()).First().TotalAdministrations, Is.EqualTo(0));

        await mar.RecordAdministrationAsync(orderId, $"BCMA-{Guid.NewGuid()}", "GIVEN", DateTime.UtcNow, null);
        await mar.RecordAdministrationAsync(orderId, $"BCMA-{Guid.NewGuid()}", "GIVEN", DateTime.UtcNow, null);

        MarEntry entry = (await mar.GetMARAsync()).First(e => e.OrderId == orderId);
        Assert.That(entry.TotalAdministrations, Is.EqualTo(2));
    }

    // ─── Test 9: Multiple administrations track all BCMA IDs ─────────────

    [Test]
    public async Task Bcma_MAR_MultipleAdmins_AllBcmaIdsTracked()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        string orderId = $"PSJ-{Guid.NewGuid()}";
        IPatientMARGrain mar = MARGrain(patientId);

        await mar.AddOrUpdateEntryAsync(MakeEntry(orderId));

        string id1 = $"BCMA-{Guid.NewGuid()}";
        string id2 = $"BCMA-{Guid.NewGuid()}";
        string id3 = $"BCMA-{Guid.NewGuid()}";

        await mar.RecordAdministrationAsync(orderId, id1, "GIVEN", DateTime.UtcNow, null);
        await mar.RecordAdministrationAsync(orderId, id2, "HELD", DateTime.UtcNow, null);
        await mar.RecordAdministrationAsync(orderId, id3, "GIVEN", DateTime.UtcNow, null);

        MarEntry entry = (await mar.GetMARAsync()).First(e => e.OrderId == orderId);
        Assert.That(entry.BcmaAdministrationIds, Does.Contain(id1));
        Assert.That(entry.BcmaAdministrationIds, Does.Contain(id2));
        Assert.That(entry.BcmaAdministrationIds, Does.Contain(id3));
    }

    // ─── Test 10: Deactivate removes from active MAR ─────────────────────

    [Test]
    public async Task Bcma_MAR_DeactivateEntry_ExcludedFromActiveMAR()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        string orderId1 = $"PSJ-{Guid.NewGuid()}";
        string orderId2 = $"PSJ-{Guid.NewGuid()}";
        IPatientMARGrain mar = MARGrain(patientId);

        await mar.AddOrUpdateEntryAsync(MakeEntry(orderId1));
        await mar.AddOrUpdateEntryAsync(MakeEntry(orderId2));

        await mar.DeactivateEntryAsync(orderId1);

        List<MarEntry> active = await mar.GetActiveMARAsync();
        Assert.That(active, Has.Count.EqualTo(1));
        Assert.That(active[0].OrderId, Is.EqualTo(orderId2));

        // Full MAR still contains both
        List<MarEntry> all = await mar.GetMARAsync();
        Assert.That(all, Has.Count.EqualTo(2));
    }

    // ─── Test 11: GetDueNow returns past scheduled times ─────────────────

    [Test]
    public async Task Bcma_MAR_GetDueNow_ReturnsPastScheduledTimes()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientMARGrain mar = MARGrain(patientId);

        // Entry with a scheduled time 2 hours ago (overdue)
        MarEntry overdueEntry = MakeEntry($"PSJ-{Guid.NewGuid()}");
        overdueEntry.ScheduledTimes = [DateTime.UtcNow.AddHours(-2)];

        // Entry with next dose 6 hours away (not due yet)
        MarEntry futureEntry = MakeEntry($"PSJ-{Guid.NewGuid()}");
        futureEntry.DrugName = "Future Drug";
        futureEntry.ScheduledTimes = [DateTime.UtcNow.AddHours(6)];

        await mar.AddOrUpdateEntryAsync(overdueEntry);
        await mar.AddOrUpdateEntryAsync(futureEntry);

        List<MarEntry> due = await mar.GetDueNowAsync();
        Assert.That(due, Has.Count.EqualTo(1));
        Assert.That(due[0].DrugName, Is.EqualTo("Metoprolol 25mg"));
    }

    // ─── Test 12: GetDueNow excludes future-only schedules ───────────────

    [Test]
    public async Task Bcma_MAR_GetDueNow_FutureScheduledTimesNotReturned()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientMARGrain mar = MARGrain(patientId);

        MarEntry entry = MakeEntry($"PSJ-{Guid.NewGuid()}");
        entry.ScheduledTimes = [DateTime.UtcNow.AddHours(4), DateTime.UtcNow.AddHours(12)];

        await mar.AddOrUpdateEntryAsync(entry);

        List<MarEntry> due = await mar.GetDueNowAsync();
        Assert.That(due, Is.Empty);
    }

    // ─── Test 13: Multiple orders all tracked in MAR ─────────────────────

    [Test]
    public async Task Bcma_MAR_MultipleOrders_AllTracked()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientMARGrain mar = MARGrain(patientId);

        for (int i = 0; i < 5; i++)
            await mar.AddOrUpdateEntryAsync(MakeEntry($"PSJ-ORDER-{i}-{Guid.NewGuid()}"));

        List<MarEntry> all = await mar.GetMARAsync();
        Assert.That(all, Has.Count.EqualTo(5));
    }

    // ─── Test 14: Upsert updates an existing entry ───────────────────────

    [Test]
    public async Task Bcma_MAR_AddOrUpdate_UpdatesExistingEntry()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        string orderId = $"PSJ-{Guid.NewGuid()}";
        IPatientMARGrain mar = MARGrain(patientId);

        MarEntry original = MakeEntry(orderId);
        await mar.AddOrUpdateEntryAsync(original);

        MarEntry updated = MakeEntry(orderId);
        updated.DrugName = "Metoprolol ER 50mg";
        updated.Dosage = "50mg";
        await mar.AddOrUpdateEntryAsync(updated);

        List<MarEntry> all = await mar.GetMARAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].DrugName, Is.EqualTo("Metoprolol ER 50mg"));
        Assert.That(all[0].Dosage, Is.EqualTo("50mg"));
    }

    // ─── Test 15: Witness recorded on BcmaGrain ──────────────────────────

    [Test]
    public async Task Bcma_RecordWitness_StoredOnAdministrationRecord()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        string bcmaId = await w.RecordMedicationAdministrationAsync(
            "Dilaudid 2mg", null, "2mg", "IV", "GIVEN",
            null, DateTime.UtcNow, "RN-003", "Nurse Rivera",
            null, null, null, null);

        IBcmaGrain grain = BcmaGrain(bcmaId);
        await grain.RecordWitnessAsync("RN-004", "Nurse Patel");

        BcmaState state = await grain.GetAdministrationAsync();
        Assert.That(state.WitnessId, Is.EqualTo("RN-004"));
        Assert.That(state.WitnessName, Is.EqualTo("Nurse Patel"));
    }

    // ─── Test 16: STAT orders appear first in MAR ────────────────────────

    [Test]
    public async Task Bcma_MAR_StatPriority_SortedFirst()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientMARGrain mar = MARGrain(patientId);

        MarEntry routine = MakeEntry($"PSJ-{Guid.NewGuid()}");
        routine.DrugName = "Routine Drug";
        routine.Priority = "ROUTINE";

        MarEntry stat = MakeEntry($"PSJ-{Guid.NewGuid()}");
        stat.DrugName = "STAT Drug";
        stat.Priority = "STAT";

        // Add routine first, then STAT
        await mar.AddOrUpdateEntryAsync(routine);
        await mar.AddOrUpdateEntryAsync(stat);

        List<MarEntry> active = await mar.GetActiveMARAsync();
        Assert.That(active[0].Priority, Is.EqualTo("STAT"));
        Assert.That(active[1].Priority, Is.EqualTo("ROUTINE"));
    }
}
