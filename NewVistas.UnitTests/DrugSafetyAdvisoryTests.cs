// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Services;
using Orleans.TestingHost;

namespace NewVistas.UnitTests;

// ─── Drug Safety Advisory Tests ───────────────────────────────────────────────

[TestFixture]
public class DrugSafetyAdvisoryTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IDrugSafetyAdvisoryGrain Advisory(string id) =>
        _cluster.GrainFactory.GetGrain<IDrugSafetyAdvisoryGrain>(id);

    private IPatientSafetyAdvisoryGrain PatientLog(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientSafetyAdvisoryGrain>(patientId);

    private static DrugSafetyAdvisoryState NewPpiAdvisory(string id) => new()
    {
        AdvisoryId = id,
        Title = "PPIs and risk of bone fracture",
        SourceType = AdvisorySourceType.FdaDrugSafetyCommunication,
        SourceReference = "FDA Drug Safety Communication, May 25, 2010",
        SourcePublishedDate = new DateTime(2010, 5, 25, 0, 0, 0, DateTimeKind.Utc),
        Severity = AdvisorySeverity.High,
        ActionType = AdvisoryActionType.WarnPatient,
        TargetDrugClassCodes = ["GA301"],
        DefaultMessage = "Long-term high-dose PPI use may increase fracture risk. Do not stop on your own.",
        ClinicalSummary = "Reassess indication and duration; lowest effective dose.",
        CreatedBy = "PHARM-INFORMATICS",
    };

    // ── Authoring & lifecycle ──────────────────────────────────────────────

    [Test]
    public async Task SaveAndActivate_AdvisoryAppearsInActiveIndexByClass()
    {
        string id = $"DSA-{Guid.NewGuid()}";
        await Advisory(id).SaveAsync(NewPpiAdvisory(id));
        await Advisory(id).ActivateAsync();

        IDrugSafetyAdvisoryIndexGrain index =
            _cluster.GrainFactory.GetGrain<IDrugSafetyAdvisoryIndexGrain>("DSA-INDEX");

        List<DrugSafetyAdvisorySummary> byClass = await index.GetActiveByDrugClassAsync("GA301");
        Assert.That(byClass.Select(s => s.AdvisoryId), Does.Contain(id));

        DrugSafetyAdvisoryState state = await Advisory(id).GetAsync();
        Assert.That(state.Status, Is.EqualTo(AdvisoryStatus.Active));
    }

    [Test]
    public async Task Dispatch_NonActiveAdvisory_Throws()
    {
        string id = $"DSA-{Guid.NewGuid()}";
        await Advisory(id).SaveAsync(NewPpiAdvisory(id)); // still Draft

        Assert.That(async () => await Advisory(id).DispatchAsync(
                "msg", ["PATIENT-1"], "PROV-1", "Dr. A", AdvisoryChannel.PatientPortal),
            Throws.InvalidOperationException);
    }

    // ── Provider edits the message, then sends ─────────────────────────────

    [Test]
    public async Task Dispatch_RecordsProviderEditedMessage_VerbatimOnPatientReceipt()
    {
        string id = $"DSA-{Guid.NewGuid()}";
        await Advisory(id).SaveAsync(NewPpiAdvisory(id));
        await Advisory(id).ActivateAsync();

        string patientId = $"PATIENT-{Guid.NewGuid()}";
        const string editedMessage =
            "Hi John — because you've been on omeprazole a long time, let's review it "
            + "and check your bone health at your next visit. Please don't stop it on your own.";

        AdvisoryDispatchResult result = await Advisory(id).DispatchAsync(
            editedMessage, [patientId], "PROV-7", "Dr. Smith", AdvisoryChannel.SecureMessage);

        Assert.That(result.SentCount, Is.EqualTo(1));

        // The patient's record holds exactly what was sent — the edited text, not the default.
        List<PatientAdvisoryReceipt> receipts = await PatientLog(patientId).GetReceiptsAsync();
        Assert.That(receipts, Has.Count.EqualTo(1));
        Assert.That(receipts[0].AdvisoryId, Is.EqualTo(id));
        Assert.That(receipts[0].MessageSent, Is.EqualTo(editedMessage));
        Assert.That(receipts[0].SentByProviderName, Is.EqualTo("Dr. Smith"));
        Assert.That(receipts[0].Channel, Is.EqualTo(AdvisoryChannel.SecureMessage));
        Assert.That(receipts[0].Status, Is.EqualTo(AdvisoryReceiptStatus.Sent));
    }

    // ── "What patients received what warning" + never double-warn ──────────

    [Test]
    public async Task Dispatch_ToMultiplePatients_ThenRedispatch_SkipsAlreadyReached()
    {
        string id = $"DSA-{Guid.NewGuid()}";
        await Advisory(id).SaveAsync(NewPpiAdvisory(id));
        await Advisory(id).ActivateAsync();

        string p1 = $"PATIENT-{Guid.NewGuid()}";
        string p2 = $"PATIENT-{Guid.NewGuid()}";
        string p3 = $"PATIENT-{Guid.NewGuid()}";

        AdvisoryDispatchResult first = await Advisory(id).DispatchAsync(
            "msg v1", [p1, p2], "PROV-1", "Dr. A", AdvisoryChannel.PatientPortal);
        Assert.That(first.SentCount, Is.EqualTo(2));
        Assert.That(first.SkippedAlreadySent, Is.Empty);

        // A second provider re-runs the dispatch overlapping p2 and adding p3.
        AdvisoryDispatchResult second = await Advisory(id).DispatchAsync(
            "msg v2", [p2, p3], "PROV-2", "Dr. B", AdvisoryChannel.PatientPortal);
        Assert.That(second.SentCount, Is.EqualTo(1));              // only p3 is new
        Assert.That(second.SkippedAlreadySent, Does.Contain(p2));  // p2 not double-warned

        // p2 keeps its original receipt/message; it was not overwritten.
        List<PatientAdvisoryReceipt> p2Receipts = await PatientLog(p2).GetReceiptsAsync();
        Assert.That(p2Receipts, Has.Count.EqualTo(1));
        Assert.That(p2Receipts[0].MessageSent, Is.EqualTo("msg v1"));

        // Advisory reach reflects 3 distinct patients.
        DrugSafetyAdvisoryState state = await Advisory(id).GetAsync();
        Assert.That(state.TotalDispatched, Is.EqualTo(3));
        Assert.That(await Advisory(id).HasReachedAsync(p3), Is.True);
    }

    [Test]
    public async Task RecordReceipt_IsIdempotentPerAdvisory()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        string advisoryId = $"DSA-{Guid.NewGuid()}";

        string r1 = await PatientLog(patientId).RecordReceiptAsync(
            advisoryId, "T", "m1", "PROV-1", "Dr. A", AdvisoryChannel.PatientPortal);
        string r2 = await PatientLog(patientId).RecordReceiptAsync(
            advisoryId, "T", "m2", "PROV-1", "Dr. A", AdvisoryChannel.PatientPortal);

        Assert.That(r2, Is.EqualTo(r1));
        Assert.That((await PatientLog(patientId).GetReceiptsAsync()), Has.Count.EqualTo(1));
        Assert.That(await PatientLog(patientId).HasReceivedAsync(advisoryId), Is.True);
    }

    [Test]
    public async Task Acknowledge_MarksReceiptAcknowledged()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        string advisoryId = $"DSA-{Guid.NewGuid()}";
        await PatientLog(patientId).RecordReceiptAsync(
            advisoryId, "T", "m", "PROV-1", "Dr. A", AdvisoryChannel.PatientPortal);

        DateTime ackDate = new(2026, 6, 24, 0, 0, 0, DateTimeKind.Utc);
        await PatientLog(patientId).AcknowledgeAsync(advisoryId, ackDate);

        PatientAdvisoryReceipt receipt = (await PatientLog(patientId).GetReceiptsAsync()).Single();
        Assert.That(receipt.Status, Is.EqualTo(AdvisoryReceiptStatus.Acknowledged));
        Assert.That(receipt.AcknowledgedDate, Is.EqualTo(ackDate));
    }

    // ── OTC switch reuses the same machinery (reconcile, not warn) ─────────

    [Test]
    public async Task RxToOtcSwitch_DispatchesAsReconciliationAction()
    {
        string id = $"DSA-{Guid.NewGuid()}";
        DrugSafetyAdvisoryState otc = NewPpiAdvisory(id);
        otc.SourceType = AdvisorySourceType.RxToOtcSwitch;
        otc.ActionType = AdvisoryActionType.ReconcileMedication;
        otc.Title = "Omeprazole now OTC — confirm current use";
        await Advisory(id).SaveAsync(otc);
        await Advisory(id).ActivateAsync();

        string patientId = $"PATIENT-{Guid.NewGuid()}";
        AdvisoryDispatchResult result = await Advisory(id).DispatchAsync(
            "Are you still taking omeprazole over the counter? Let us know.",
            [patientId], "PROV-1", "Dr. A", AdvisoryChannel.PatientPortal);

        Assert.That(result.SentCount, Is.EqualTo(1));

        DrugSafetyAdvisoryState state = await Advisory(id).GetAsync();
        Assert.That(state.ActionType, Is.EqualTo(AdvisoryActionType.ReconcileMedication));
    }

    // ── Ingestion seam ─────────────────────────────────────────────────────

    [Test]
    public async Task StaticFdaSource_SeedsPpiAndOtcDrafts_ThatPromoteToAdvisories()
    {
        IFdaDrugWarningSource source = new StaticFdaDrugWarningSource();
        Assert.That(source.IsLiveSource, Is.False);

        List<FdaDrugWarningDraft> drafts = await source.FetchCandidateWarningsAsync();
        Assert.That(drafts, Has.Count.GreaterThanOrEqualTo(2));

        FdaDrugWarningDraft ppi = drafts.First(d => d.SourceType == AdvisorySourceType.FdaDrugSafetyCommunication);
        Assert.That(ppi.TargetDrugClassCodes, Does.Contain("GA301"));
        Assert.That(drafts.Any(d => d.ActionType == AdvisoryActionType.ReconcileMedication), Is.True);

        // A reviewer promotes the draft to an advisory.
        string id = $"DSA-{Guid.NewGuid()}";
        await Advisory(id).SaveAsync(new DrugSafetyAdvisoryState
        {
            AdvisoryId = id,
            Title = ppi.Title,
            SourceType = ppi.SourceType,
            SourceReference = ppi.SourceReference,
            SourcePublishedDate = ppi.SourcePublishedDate,
            Severity = ppi.Severity,
            ActionType = ppi.ActionType,
            TargetDrugClassCodes = ppi.TargetDrugClassCodes,
            DefaultMessage = ppi.SuggestedMessage,
            ClinicalSummary = ppi.ClinicalSummary,
        });

        DrugSafetyAdvisoryState saved = await Advisory(id).GetAsync();
        Assert.That(saved.DefaultMessage, Is.EqualTo(ppi.SuggestedMessage));
        Assert.That(saved.TargetDrugClassCodes, Does.Contain("GA301"));
    }
}
