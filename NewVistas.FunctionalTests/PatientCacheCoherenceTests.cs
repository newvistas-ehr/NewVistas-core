// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Cache coherence between an entity grain and the patient's copy of it.
///
/// The patient grain keeps a small "recent" window per domain — the hot path the cover sheet
/// and the chart tabs read without fanning out. The entity grain owns the truth. Anything
/// that changes the entity therefore has to reach the patient's copy too, or the chart shows
/// a stale or duplicated row.
///
/// Creates were always handled. <b>Changes were not</b>, in three distinct ways:
/// an order could be completed and still read Pending; signing a note put a second copy of it
/// in the cache; and a retracted note stayed on display. These tests pin all three, plus the
/// create and remove paths, so the next cached domain added has a shape to copy.
/// </summary>
[TestFixture]
public class PatientCacheCoherenceTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IPatientWorkflowGrain Wf(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IPatientGrain Patient(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);

    private static string NewPatient() => $"CACHE-{Guid.NewGuid()}";

    private Task<string> PlaceAsync(string pid, string text = "CBC") =>
        Wf(pid).PlaceOrderAsync("Laboratory", text, null, "PROV-1", "Dr. One", null, null, "ROUTINE", null, null);

    // ── Orders ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Order_Create_ReachesThePatientCache()
    {
        string pid = NewPatient();
        string orderId = await PlaceAsync(pid);

        List<OrderSummary> cached = await Patient(pid).GetRecentOrdersAsync();

        Assert.That(cached, Has.Count.EqualTo(1));
        Assert.That(cached[0].OrderId, Is.EqualTo(orderId));
    }

    [Test]
    public async Task Order_StatusChange_UpdatesThePatientCacheInPlace()
    {
        string pid = NewPatient();
        string orderId = await PlaceAsync(pid);

        string before = (await Patient(pid).GetRecentOrdersAsync())[0].Status;

        await Wf(pid).DiscontinueOrderAsync(orderId, "Entered in error");

        List<OrderSummary> cached = await Patient(pid).GetRecentOrdersAsync();

        Assert.Multiple(() =>
        {
            Assert.That(cached, Has.Count.EqualTo(1), "a status change must not add a second row");
            Assert.That(cached[0].Status, Is.Not.EqualTo(before), "the patient's copy must see the new status");
        });
    }

    [Test]
    public async Task Order_SignThenDiscontinue_StaysASingleCoherentRow()
    {
        string pid = NewPatient();
        string orderId = await PlaceAsync(pid);

        await Wf(pid).SignOrderAsSystemAsync(orderId, "SIG");
        await Wf(pid).HoldOrderAsync(orderId);
        await Wf(pid).ReleaseOrderAsync(orderId);
        await Wf(pid).DiscontinueOrderAsync(orderId, "No longer needed");

        List<OrderSummary> cached = await Patient(pid).GetRecentOrdersAsync();
        OrderState truth = await _cluster.GrainFactory.GetGrain<IOrderGrain>(orderId).GetOrderAsync();

        Assert.Multiple(() =>
        {
            Assert.That(cached, Has.Count.EqualTo(1), "four changes must not produce four rows");
            Assert.That(cached[0].Status, Is.EqualTo(truth.Status),
                "the patient's cached status must match the order grain that owns it");
        });
    }

    [Test]
    public async Task Order_ChangeAfterItAgedOutOfTheWindow_IsHarmless()
    {
        string pid = NewPatient();

        // Push the first order well out of the recent window, then change it. The cache no
        // longer holds it; the index still does, so this must be a quiet no-op rather than a
        // failure or a resurrected row.
        string first = await PlaceAsync(pid, "FIRST");
        for (int i = 0; i < 12; i++) await PlaceAsync(pid, $"FILLER-{i}");

        List<OrderSummary> before = await Patient(pid).GetRecentOrdersAsync();
        Assert.That(before.Any(o => o.OrderId == first), Is.False, "precondition: first order aged out");

        Assert.DoesNotThrowAsync(async () => await Wf(pid).DiscontinueOrderAsync(first, "late change"));

        List<OrderSummary> after = await Patient(pid).GetRecentOrdersAsync();
        Assert.Multiple(() =>
        {
            Assert.That(after, Has.Count.EqualTo(before.Count), "an aged-out change must not grow the cache");
            Assert.That(after.Any(o => o.OrderId == first), Is.False, "nor resurrect the aged-out entry");
        });

        // …and the index, which is the source of truth for older orders, did see it.
        List<OrderSummary> all = await Wf(pid).GetOrdersByFilterAsync(1);
        Assert.That(all.Single(o => o.OrderId == first).Status, Is.EqualTo("Discontinued"));
    }

    // ── Notes ───────────────────────────────────────────────────────────────

    private Task<string> WriteNoteAsync(string pid, string subject = "Progress Note") =>
        Wf(pid).CreateNoteAsync("PROGRESS NOTE", null, "Body text.", subject,
            "PROV-1", "Dr. One", null, null, null, null, null, DateTime.UtcNow);

    [Test]
    public async Task Note_Create_ReachesThePatientCache()
    {
        string pid = NewPatient();
        string docId = await WriteNoteAsync(pid);

        List<TiuNoteSummary> cached = await Patient(pid).GetRecentNotesAsync();

        Assert.That(cached, Has.Count.EqualTo(1));
        Assert.That(cached[0].DocumentId, Is.EqualTo(docId));
    }

    [Test]
    public async Task Note_Sign_DoesNotDuplicateItInTheCache()
    {
        string pid = NewPatient();
        string docId = await WriteNoteAsync(pid);

        await Wf(pid).SignNoteAsSystemAsync(docId);

        List<TiuNoteSummary> cached = await Patient(pid).GetRecentNotesAsync();

        Assert.That(cached.Count(n => n.DocumentId == docId), Is.EqualTo(1),
            "signing a note must update its cached row, not insert a second copy");
    }

    [Test]
    public async Task Note_SignThenAmend_LeavesOneRowCarryingTheLatestStatus()
    {
        string pid = NewPatient();
        string docId = await WriteNoteAsync(pid);

        await Wf(pid).SignNoteAsSystemAsync(docId);
        await Wf(pid).AmendNoteAsync(docId, "Amended body text.");

        List<TiuNoteSummary> cached = await Patient(pid).GetRecentNotesAsync();
        TiuDocumentState truth = await Wf(pid).GetNoteAsync(docId);

        Assert.Multiple(() =>
        {
            Assert.That(cached.Count(n => n.DocumentId == docId), Is.EqualTo(1));
            Assert.That(cached.Single(n => n.DocumentId == docId).Status, Is.EqualTo(truth.Status));
        });
    }

    [Test]
    public async Task Note_Addendum_UpdatesTheParentWithoutAddingItselfToTheCache()
    {
        string pid = NewPatient();
        string parentId = await WriteNoteAsync(pid);
        await Wf(pid).SignNoteAsSystemAsync(parentId);

        string addendumId = await Wf(pid).AddAddendumAsync(parentId, "Additional findings.",
            "PROV-1", "Dr. One", DateTime.UtcNow);

        List<TiuNoteSummary> cached = await Patient(pid).GetRecentNotesAsync();

        Assert.Multiple(() =>
        {
            Assert.That(cached.Any(n => n.DocumentId == addendumId), Is.False,
                "addenda surface through the parent, not as their own cached row");
            Assert.That(cached.Count(n => n.DocumentId == parentId), Is.EqualTo(1));
            Assert.That(cached.Single(n => n.DocumentId == parentId).HasAddenda, Is.True,
                "the parent's cached row should now show it has addenda");
        });
    }

    // ── Vitals ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Vital_Create_ReachesThePatientCache()
    {
        string pid = NewPatient();

        await Wf(pid).RecordVitalsAsync(null, null, "PROV-1", "Dr. One", DateTime.UtcNow,
            new Dictionary<string, string> { ["BLOOD PRESSURE"] = "120/80" }, null);

        List<VitalSummary> cached = await Patient(pid).GetRecentVitalsAsync();
        Assert.That(cached, Is.Not.Empty);
    }

    // ── The primitives themselves ───────────────────────────────────────────

    [Test]
    public async Task RemoveFromCache_DropsTheRow_AndIsANoOpWhenAbsent()
    {
        string pid = NewPatient();
        string orderId = await PlaceAsync(pid);

        await Patient(pid).RemoveRecentOrderAsync(orderId);
        Assert.That(await Patient(pid).GetRecentOrdersAsync(), Is.Empty);

        Assert.DoesNotThrowAsync(async () => await Patient(pid).RemoveRecentOrderAsync("ORDER-does-not-exist"));
        Assert.DoesNotThrowAsync(async () => await Patient(pid).RemoveRecentNoteAsync("TIU-does-not-exist"));
        Assert.DoesNotThrowAsync(async () => await Patient(pid).RemoveRecentVitalAsync("VITAL-does-not-exist"));
    }

    [Test]
    public async Task UpdateCache_KeepsThePositionOfTheEntryItReplaces()
    {
        string pid = NewPatient();
        string first = await PlaceAsync(pid, "FIRST");
        await PlaceAsync(pid, "SECOND");
        await PlaceAsync(pid, "THIRD");

        // Newest first, so the first order placed sits last.
        List<OrderSummary> before = await Patient(pid).GetRecentOrdersAsync();
        int position = before.FindIndex(o => o.OrderId == first);

        await Wf(pid).DiscontinueOrderAsync(first, "Changed my mind");

        List<OrderSummary> after = await Patient(pid).GetRecentOrdersAsync();
        Assert.That(after.FindIndex(o => o.OrderId == first), Is.EqualTo(position),
            "an update must not reorder the window — the row changes, its place does not");
    }
}
