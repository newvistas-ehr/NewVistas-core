// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

// ─── Radiology report lifecycle through the workflow façade ───────────────────
// Sign / amend / critical-result / contrast paths on PatientWorkflowGrain
// (PatientWorkflowGrain.ClinicalDocs.cs), backed by RadiologyGrain (File #75.1).
//
// The report is a legal document: an amendment is recorded ALONGSIDE the signed
// text (AmendmentText), never by rewriting ReportText. These tests pin that.
//
// The grain enforces a minimal report state machine at the signature boundary:
// a signed (FINAL/AMENDED) report can never be re-signed (the first signature is
// immutable), a study with no report text cannot be signed, and only a signed
// report can be amended — an unsigned draft is edited (RecordReportAsync), not
// amended. These guards are pinned below.

[TestFixture]
public class RadiologyReportWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    /// <summary>Order a study and file its report — the normal precondition for signing.</summary>
    private async Task<(IPatientWorkflowGrain wf, string radiologyId)> OrderAndReportAsync(
        string reportText = "Lungs are clear. No acute cardiopulmonary process.",
        string impression = "No acute findings.")
    {
        string pid = $"RADPAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(pid);
        string radiologyId = await wf.OrderRadiologyStudyAsync(
            "CHEST 2 VIEWS PA&LAT", null, "71046", "GENERAL RADIOLOGY",
            "PROV-1", "Dr. A", "ROUTINE", "Cough x 3 weeks", "Rule out pneumonia",
            null, null, null);
        await wf.CompleteRadiologyAsync(radiologyId, reportText, impression, "RAD-1", "Dr. R");
        return (wf, radiologyId);
    }

    // ── Sign → amend: legal-document semantics ──────────────────────────────

    [Test]
    public async Task SignedReport_RecordsSigner_AndBecomesFinal()
    {
        (IPatientWorkflowGrain wf, string radiologyId) = await OrderAndReportAsync();

        await wf.SignRadiologyReportAsync(radiologyId, "RAD-1", "Dr. R");

        RadiologyState study = await wf.GetRadiologyStudyAsync(radiologyId);
        Assert.That(study.SignedById, Is.EqualTo("RAD-1"));
        Assert.That(study.SignedByName, Is.EqualTo("Dr. R"));
        Assert.That(study.SignedDateTime, Is.Not.Null);
        Assert.That(study.ReportStatus, Is.EqualTo("FINAL"));
    }

    [Test]
    public async Task Amendment_IsRecordedAlongside_AndNeverRewritesTheSignedReportText()
    {
        const string originalText = "Lungs are clear. No acute cardiopulmonary process.";
        (IPatientWorkflowGrain wf, string radiologyId) = await OrderAndReportAsync(originalText);
        await wf.SignRadiologyReportAsync(radiologyId, "RAD-1", "Dr. R");

        await wf.AmendRadiologyReportAsync(radiologyId,
            "Addendum: subtle right lower lobe opacity on re-review; recommend follow-up.");

        RadiologyState study = await wf.GetRadiologyStudyAsync(radiologyId);
        Assert.That(study.AmendmentText,
            Is.EqualTo("Addendum: subtle right lower lobe opacity on re-review; recommend follow-up."));
        Assert.That(study.AmendmentDateTime, Is.Not.Null);
        Assert.That(study.ReportStatus, Is.EqualTo("AMENDED"));

        // The legal document: what was signed stays exactly as signed.
        Assert.That(study.ReportText, Is.EqualTo(originalText));
        Assert.That(study.Impression, Is.EqualTo("No acute findings."));
        Assert.That(study.SignedById, Is.EqualTo("RAD-1"),
            "the original signature must survive the amendment");
    }

    // ── The signature boundary: guards pinned ───────────────────────────────

    [Test]
    public async Task SignBeforeAnyReportIsFiled_IsRefused_AndTheStudyStaysUnsigned()
    {
        string pid = $"RADPAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(pid);
        string radiologyId = await wf.OrderRadiologyStudyAsync(
            "CT HEAD W/O CONTRAST", null, "70450", "CT",
            "PROV-1", "Dr. A", "STAT", null, "Head trauma", null, null, null);

        // No report has been filed — there is nothing to sign.
        Assert.That(async () => await wf.SignRadiologyReportAsync(radiologyId, "RAD-2", "Dr. S"),
            Throws.InvalidOperationException.With.Message.Contains("no report text"));

        RadiologyState study = await wf.GetRadiologyStudyAsync(radiologyId);
        Assert.That(study.SignedById, Is.Null, "the refused signature must not be recorded");
        Assert.That(study.ReportText, Is.Null.Or.Empty);
        Assert.That(study.ReportStatus, Is.Not.EqualTo("FINAL"),
            "a study with no report text can never be FINAL");
    }

    [Test]
    public async Task AmendAnUnsignedReport_IsRefused_ADraftIsEditedNotAmended()
    {
        (IPatientWorkflowGrain wf, string radiologyId) = await OrderAndReportAsync();
        // Never signed — the report is still a draft, and drafts are edited
        // (RecordReportAsync / CompleteRadiologyAsync), not amended.

        Assert.That(async () => await wf.AmendRadiologyReportAsync(radiologyId,
                "Corrected laterality: LEFT, not right."),
            Throws.InvalidOperationException.With.Message.Contains("edited, not amended"));

        RadiologyState study = await wf.GetRadiologyStudyAsync(radiologyId);
        Assert.That(study.AmendmentText, Is.Null, "the refused amendment must not be recorded");
        Assert.That(study.AmendmentDateTime, Is.Null);
        Assert.That(study.SignedById, Is.Null, "no signature was ever recorded");
        Assert.That(study.ReportStatus, Is.Not.EqualTo("AMENDED"),
            "an unsigned draft can never jump to AMENDED");
    }

    [Test]
    public async Task DoubleSign_IsRefused_AndTheOriginalSignatureSurvives()
    {
        (IPatientWorkflowGrain wf, string radiologyId) = await OrderAndReportAsync();
        await wf.SignRadiologyReportAsync(radiologyId, "RAD-1", "Dr. R");

        // A second signature is refused: the first signature is immutable.
        Assert.That(async () => await wf.SignRadiologyReportAsync(radiologyId, "RAD-9", "Dr. Z"),
            Throws.InvalidOperationException.With.Message.Contains("already signed"));

        RadiologyState study = await wf.GetRadiologyStudyAsync(radiologyId);
        Assert.That(study.ReportStatus, Is.EqualTo("FINAL"));
        Assert.That(study.SignedById, Is.EqualTo("RAD-1"), "the original signer must survive");
        Assert.That(study.SignedByName, Is.EqualTo("Dr. R"));
        Assert.That(study.SignedDateTime, Is.Not.Null);
    }

    [Test]
    public async Task ReSignAfterAmendment_IsAlsoRefused()
    {
        (IPatientWorkflowGrain wf, string radiologyId) = await OrderAndReportAsync();
        await wf.SignRadiologyReportAsync(radiologyId, "RAD-1", "Dr. R");
        await wf.AmendRadiologyReportAsync(radiologyId, "Addendum: incidental thyroid nodule.");

        // AMENDED is still a signed report — the signature stays immutable.
        Assert.That(async () => await wf.SignRadiologyReportAsync(radiologyId, "RAD-9", "Dr. Z"),
            Throws.InvalidOperationException.With.Message.Contains("already signed"));

        RadiologyState study = await wf.GetRadiologyStudyAsync(radiologyId);
        Assert.That(study.SignedById, Is.EqualTo("RAD-1"));
        Assert.That(study.ReportStatus, Is.EqualTo("AMENDED"));
    }

    // ── Critical results ────────────────────────────────────────────────────

    [Test]
    public async Task CriticalResult_FlagNotifyAcknowledge_RoundTrips()
    {
        (IPatientWorkflowGrain wf, string radiologyId) = await OrderAndReportAsync(
            "Large right pneumothorax.", "PNEUMOTHORAX — critical finding.");

        Assert.That(await wf.IsRadiologyCriticalResultAsync(radiologyId), Is.False,
            "a study starts life uncritical");

        await wf.FlagCriticalRadiologyResultAsync(radiologyId);
        Assert.That(await wf.IsRadiologyCriticalResultAsync(radiologyId), Is.True);

        await wf.RecordCriticalResultNotificationAsync(radiologyId, "Dr. A (ordering provider)");
        await wf.AcknowledgeCriticalResultAsync(radiologyId, "Dr. A");

        RadiologyState study = await wf.GetRadiologyStudyAsync(radiologyId);
        Assert.That(study.IsCriticalResult, Is.True);
        Assert.That(study.CriticalResultNotifiedTo, Is.EqualTo("Dr. A (ordering provider)"));
        Assert.That(study.CriticalResultNotifiedDateTime, Is.Not.Null);
        Assert.That(study.CriticalResultAcknowledgedBy, Is.EqualTo("Dr. A"));
    }

    [Test]
    public async Task DoubleFlag_IsIdempotent_AndDoesNotDisturbTheNotificationTrail()
    {
        (IPatientWorkflowGrain wf, string radiologyId) = await OrderAndReportAsync();

        await wf.FlagCriticalRadiologyResultAsync(radiologyId);
        await wf.RecordCriticalResultNotificationAsync(radiologyId, "Dr. A");

        await wf.FlagCriticalRadiologyResultAsync(radiologyId);

        Assert.That(await wf.IsRadiologyCriticalResultAsync(radiologyId), Is.True);
        RadiologyState study = await wf.GetRadiologyStudyAsync(radiologyId);
        Assert.That(study.CriticalResultNotifiedTo, Is.EqualTo("Dr. A"),
            "re-flagging must not erase who was already notified");
    }

    // ── Contrast administration and reactions ───────────────────────────────

    [Test]
    public async Task Contrast_ThenReaction_RecordsBoth_AndKeepsTheAgentDetails()
    {
        (IPatientWorkflowGrain wf, string radiologyId) = await OrderAndReportAsync();

        await wf.RecordRadiologyContrastAsync(radiologyId, "Iohexol 350", "IV", 100);
        await wf.RecordRadiologyContrastReactionAsync(radiologyId, "Urticaria 10 min post-injection; diphenhydramine given.");

        RadiologyState study = await wf.GetRadiologyStudyAsync(radiologyId);
        Assert.That(study.ContrastAgent, Is.EqualTo("Iohexol 350"));
        Assert.That(study.ContrastRoute, Is.EqualTo("IV"));
        Assert.That(study.ContrastVolumeMl, Is.EqualTo(100));
        Assert.That(study.ContrastReactionOccurred, Is.True);
        Assert.That(study.ContrastReactionDetails,
            Is.EqualTo("Urticaria 10 min post-injection; diphenhydramine given."));
    }

    [Test]
    public async Task ReactionWithoutRecordedContrast_IsStillRecorded_FailOpen()
    {
        (IPatientWorkflowGrain wf, string radiologyId) = await OrderAndReportAsync();

        // Nobody charted the contrast administration, but the patient reacted.
        // Refusing to record a safety event over a missing charting step would be
        // the wrong failure mode — the reaction is recorded as-is.
        await wf.RecordRadiologyContrastReactionAsync(radiologyId, "Bronchospasm during injection.");

        RadiologyState study = await wf.GetRadiologyStudyAsync(radiologyId);
        Assert.That(study.ContrastReactionOccurred, Is.True);
        Assert.That(study.ContrastReactionDetails, Is.EqualTo("Bronchospasm during injection."));
        Assert.That(study.ContrastAgent, Is.Null, "no contrast administration was ever charted");
    }

    // ── Paged radiology history ─────────────────────────────────────────────

    [Test]
    public async Task RadiologyHistory_PagesNewestFirst_AndOffsetBeyondTheEndReturnsEmpty()
    {
        string pid = $"RADPAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(pid);

        string first = await wf.OrderRadiologyStudyAsync(
            "XR WRIST", null, "73100", "GENERAL RADIOLOGY",
            "PROV-1", "Dr. A", "ROUTINE", null, null, null, null, null);
        string second = await wf.OrderRadiologyStudyAsync(
            "CT CHEST", null, "71250", "CT",
            "PROV-1", "Dr. A", "ROUTINE", null, null, null, null, null);
        string third = await wf.OrderRadiologyStudyAsync(
            "MRI BRAIN", null, "70551", "MRI",
            "PROV-1", "Dr. A", "ROUTINE", null, null, null, null, null);

        // Newest first: the most recently ordered study leads the page.
        List<RadiologySummary> page = await wf.GetRadiologyHistoryAsync(0, 10);
        Assert.That(page, Has.Count.EqualTo(3));
        Assert.That(page.Select(s => s.RadiologyId),
            Is.EqualTo(new[] { third, second, first }).AsCollection);

        // Paging: skip the newest, take one — the middle study.
        List<RadiologySummary> middle = await wf.GetRadiologyHistoryAsync(1, 1);
        Assert.That(middle, Has.Count.EqualTo(1));
        Assert.That(middle[0].RadiologyId, Is.EqualTo(second));
        Assert.That(middle[0].ProcedureName, Is.EqualTo("CT CHEST"));

        // Off the end: empty page, no throw.
        Assert.That(await wf.GetRadiologyHistoryAsync(50, 10), Is.Empty);
    }

    [Test]
    public async Task RadiologyHistory_ReflectsReportPresence()
    {
        (IPatientWorkflowGrain wf, string radiologyId) = await OrderAndReportAsync();

        List<RadiologySummary> page = await wf.GetRadiologyHistoryAsync(0, 10);
        RadiologySummary summary = page.Single(s => s.RadiologyId == radiologyId);
        Assert.That(summary.HasReport, Is.True);
        Assert.That(summary.Status, Is.EqualTo("COMPLETE"));
    }

    // ── Workflow façade: rejecting an AI-extracted finding ──────────────────
    // The Acknowledge sibling lives in RadiologyFindingTests.cs; this is the
    // reject arm through the same façade the Radiology page calls.

    [Test]
    public async Task Facade_RejectFinding_RequiresAReason_AndIsRecordedPatientVisible()
    {
        string pid = $"RADPAT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(pid);

        string radiologyId = await wf.OrderRadiologyStudyAsync(
            "MRI CERVICAL SPINE W/O CONTRAST", null, "72141", "MRI",
            "PROV-1", "Dr. A", "ROUTINE", null, "Radiculopathy", null, null, null);
        await wf.CompleteRadiologyAsync(radiologyId,
            RadiologyTestData.SyntheticCervicalReport, "Multilevel degenerative change.",
            "RAD-1", "Dr. R");

        RadiologyExtractionState state = await wf.ExtractRadiologyFindingsAsync(radiologyId, "DOCTOR1");
        string materialId = state.Findings.First(f => f.RequiresAcknowledgment).FindingId;

        // A rejection without a reason is refused — you cannot silently dismiss a finding.
        Assert.That(async () => await wf.RejectRadiologyFindingAsync(radiologyId, materialId, "DOCTOR1", "  "),
            Throws.ArgumentException);

        await wf.RejectRadiologyFindingAsync(radiologyId, materialId, "DOCTOR1",
            "Believed to be a positioning artifact.");

        RadiologyExtractionState? readBack = await wf.GetRadiologyFindingsAsync(radiologyId);
        Assert.That(readBack, Is.Not.Null);
        RadiologyFinding f = readBack!.Findings.Single(x => x.FindingId == materialId);
        Assert.That(f.Acknowledgment, Is.EqualTo(FindingAcknowledgment.Rejected));
        Assert.That(f.RejectionReason, Is.EqualTo("Believed to be a positioning artifact."));
        Assert.That(f.DispositionedBy, Is.EqualTo("DOCTOR1"));
        Assert.That(f.PatientVisible, Is.True); // the rejection is on the record, visible to the patient
    }
}
