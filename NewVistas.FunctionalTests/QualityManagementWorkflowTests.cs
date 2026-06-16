// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Quality Management — VistA File #680.
/// Tests end-to-end workflows via direct grain factory access (system-level module).
/// </summary>
[TestFixture]
public class QualityManagementWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IQMIncidentGrain GetIncident(string id)
        => _cluster.GrainFactory.GetGrain<IQMIncidentGrain>(id);

    private IQMIncidentIndexGrain GetIncidentIndex()
        => _cluster.GrainFactory.GetGrain<IQMIncidentIndexGrain>("QM-INCIDENT-IDX");

    private IQMReviewGrain GetReview(string id)
        => _cluster.GrainFactory.GetGrain<IQMReviewGrain>(id);

    private IQMReviewIndexGrain GetReviewIndex()
        => _cluster.GrainFactory.GetGrain<IQMReviewIndexGrain>("QM-REVIEW-IDX");

    // ── Incident Tests ───────────────────────────────────────────────────────

    [Test]
    public async Task ReportIncident_CreatesReportedIncident()
    {
        string incidentId = $"QM-INCIDENT-{Guid.NewGuid():N}";
        string patientId = $"PATIENT-{Guid.NewGuid():N}";
        IQMIncidentGrain grain = GetIncident(incidentId);

        await grain.ReportIncidentAsync(
            patientId, "DOE,JOHN", DateTime.UtcNow.AddHours(-2),
            OccurrenceCategory.MedicationError,
            "Patient received wrong dose of medication",
            "Pharmacy", "3 West",
            OccurrenceSeverity.MinorHarm,
            "RN Smith", "Registered Nurse",
            "Dose corrected immediately",
            "Hypertension", string.Empty,
            "Lisinopril 10mg given instead of 20mg",
            string.Empty);

        QMIncidentState state = await grain.GetIncidentAsync();

        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.Category, Is.EqualTo(OccurrenceCategory.MedicationError));
        Assert.That(state.Severity, Is.EqualTo(OccurrenceSeverity.MinorHarm));
        Assert.That(state.Status, Is.EqualTo(IncidentStatus.Reported));
        Assert.That(state.Description, Does.Contain("wrong dose"));
        Assert.That(state.ReportedBy, Is.EqualTo("RN Smith"));
    }

    [Test]
    public async Task UpdateOutcome_SetsOutcomeAndNotificationFlags()
    {
        string incidentId = $"QM-INCIDENT-{Guid.NewGuid():N}";
        IQMIncidentGrain grain = GetIncident(incidentId);

        await grain.ReportIncidentAsync(
            "PAT-001", "SMITH,JANE", DateTime.UtcNow,
            OccurrenceCategory.FallWithInjury,
            "Patient fell in bathroom", "Room 201", "2 East",
            OccurrenceSeverity.ModerateHarm,
            "CNA Brown", "Certified Nursing Assistant",
            "Patient assisted to bed, vitals checked",
            "Dementia", string.Empty, string.Empty, string.Empty);

        await grain.UpdateOutcomeAsync("Bruised hip, X-ray negative for fracture", true, true);

        QMIncidentState state = await grain.GetIncidentAsync();
        Assert.That(state.OutcomeDescription, Does.Contain("Bruised hip"));
        Assert.That(state.PatientNotified, Is.True);
        Assert.That(state.FamilyNotified, Is.True);
    }

    [Test]
    public async Task AddStaffInvolved_AppendsToList()
    {
        string incidentId = $"QM-INCIDENT-{Guid.NewGuid():N}";
        IQMIncidentGrain grain = GetIncident(incidentId);

        await grain.ReportIncidentAsync(
            "PAT-002", "GREEN,BOB", DateTime.UtcNow,
            OccurrenceCategory.ProcedureComplication,
            "Unexpected bleeding during procedure", "OR 3", "Surgery",
            OccurrenceSeverity.SevereHarm,
            "Dr. Jones", "Attending Surgeon",
            "Transfusion initiated",
            "Colon cancer", "Colectomy", string.Empty, string.Empty);

        await grain.AddStaffInvolvedAsync("Dr. Jones, Surgeon");
        await grain.AddStaffInvolvedAsync("RN Davis, Circulating Nurse");

        QMIncidentState state = await grain.GetIncidentAsync();
        Assert.That(state.StaffInvolved, Has.Count.EqualTo(2));
        Assert.That(state.StaffInvolved, Contains.Item("Dr. Jones, Surgeon"));
    }

    [Test]
    public async Task AddReviewToIncident_LinksReviewAndUpdatesStatus()
    {
        string incidentId = $"QM-INCIDENT-{Guid.NewGuid():N}";
        IQMIncidentGrain grain = GetIncident(incidentId);

        await grain.ReportIncidentAsync(
            "PAT-003", "WHITE,TOM", DateTime.UtcNow,
            OccurrenceCategory.DiagnosticError,
            "Delayed diagnosis of appendicitis", "ED", "Emergency",
            OccurrenceSeverity.ModerateHarm,
            "RN Miller", "Charge Nurse",
            "Patient transferred to surgery",
            "Appendicitis", string.Empty, string.Empty, string.Empty);

        string reviewId = $"QM-REVIEW-{Guid.NewGuid():N}";
        await grain.AddReviewToIncidentAsync(reviewId, QMReviewType.PeerReview);

        QMIncidentState state = await grain.GetIncidentAsync();
        Assert.That(state.ReviewIds, Contains.Item(reviewId));
        Assert.That(state.Status, Is.EqualTo(IncidentStatus.PeerReviewAssigned));
    }

    [Test]
    public async Task SetRootCauseIdentified_RecordsCorrectiveActions()
    {
        string incidentId = $"QM-INCIDENT-{Guid.NewGuid():N}";
        IQMIncidentGrain grain = GetIncident(incidentId);

        await grain.ReportIncidentAsync(
            "PAT-004", "KING,DAN", DateTime.UtcNow,
            OccurrenceCategory.EquipmentFailure,
            "IV pump malfunction", "ICU", "ICU",
            OccurrenceSeverity.NearMiss,
            "RN Lee", "ICU Nurse",
            "Backup pump placed",
            "Sepsis", string.Empty, string.Empty, "Alaris IV pump SN-12345");

        await grain.SetRootCauseIdentifiedAsync(true, "Battery failure; all units to be inspected");

        QMIncidentState state = await grain.GetIncidentAsync();
        Assert.That(state.RootCauseIdentified, Is.True);
        Assert.That(state.CorrectiveActionsSummary, Does.Contain("Battery failure"));
    }

    [Test]
    public async Task CloseIncident_SetsStatusClosed()
    {
        string incidentId = $"QM-INCIDENT-{Guid.NewGuid():N}";
        IQMIncidentGrain grain = GetIncident(incidentId);

        await grain.ReportIncidentAsync(
            "PAT-005", "BROWN,SUE", DateTime.UtcNow,
            OccurrenceCategory.InfectionEvent,
            "CAUTI identified", "3 East", "Med-Surg",
            OccurrenceSeverity.MinorHarm,
            "IC Nurse Taylor", "Infection Control Nurse",
            "Catheter removed, antibiotics started",
            "UTI", string.Empty, string.Empty, string.Empty);

        await grain.CloseIncidentAsync();

        QMIncidentState state = await grain.GetIncidentAsync();
        Assert.That(state.Status, Is.EqualTo(IncidentStatus.Closed));
        Assert.That(state.ClosedDate, Is.Not.Null);
    }

    [Test]
    public async Task VoidIncident_SetsStatusVoided_WithReason()
    {
        string incidentId = $"QM-INCIDENT-{Guid.NewGuid():N}";
        IQMIncidentGrain grain = GetIncident(incidentId);

        await grain.ReportIncidentAsync(
            "PAT-006", "GRAY,ALICE", DateTime.UtcNow,
            OccurrenceCategory.Other,
            "Duplicate entry", "Clinic B", "Primary Care",
            OccurrenceSeverity.NoHarm,
            "Admin Clark", "Unit Secretary",
            "No action needed", string.Empty, string.Empty, string.Empty, string.Empty);

        await grain.VoidIncidentAsync("Duplicate of incident QM-INCIDENT-XYZ");

        QMIncidentState state = await grain.GetIncidentAsync();
        Assert.That(state.Status, Is.EqualTo(IncidentStatus.Voided));
        Assert.That(state.VoidReason, Does.Contain("Duplicate"));
    }

    // ── Incident Index Tests ─────────────────────────────────────────────────

    [Test]
    public async Task IncidentIndex_UpsertAndQueryBySeverity()
    {
        IQMIncidentIndexGrain index = GetIncidentIndex();

        string id1 = $"QM-INCIDENT-{Guid.NewGuid():N}";
        await index.UpsertIncidentAsync(new QMIncidentIndexEntry
        {
            IncidentId = id1, PatientId = "PAT-IDX-1", PatientName = "TEST,ONE",
            OccurrenceDate = DateTime.UtcNow,
            Category = OccurrenceCategory.MedicationError,
            Severity = OccurrenceSeverity.SevereHarm,
            Status = IncidentStatus.Reported,
            Location = "Pharmacy", WardUnit = "Outpatient"
        });

        List<QMIncidentIndexEntry> results = await index.GetIncidentsBySeverityAsync(OccurrenceSeverity.SevereHarm);
        Assert.That(results.Any(i => i.IncidentId == id1), Is.True);
    }

    // ── Review Tests ─────────────────────────────────────────────────────────

    [Test]
    public async Task AssignReview_CreatesReviewInPendingStatus()
    {
        string reviewId = $"QM-REVIEW-{Guid.NewGuid():N}";
        string incidentId = $"QM-INCIDENT-{Guid.NewGuid():N}";
        IQMReviewGrain grain = GetReview(reviewId);

        await grain.AssignReviewAsync(
            incidentId, QMReviewType.RootCauseAnalysis,
            "QM-TEAM-1", "Dr. Adams", "Chief Medical Officer",
            DateTime.UtcNow.AddDays(45), confidential: true);

        QMReviewState state = await grain.GetReviewAsync();

        Assert.That(state.IncidentId, Is.EqualTo(incidentId));
        Assert.That(state.ReviewType, Is.EqualTo(QMReviewType.RootCauseAnalysis));
        Assert.That(state.Status, Is.EqualTo(QMReviewStatus.Pending));
        Assert.That(state.ReviewerName, Is.EqualTo("Dr. Adams"));
        Assert.That(state.Confidential, Is.True);
    }

    [Test]
    public async Task ReviewFullLifecycle_AssignStartFindingsCompleteApprove()
    {
        string reviewId = $"QM-REVIEW-{Guid.NewGuid():N}";
        string incidentId = $"QM-INCIDENT-{Guid.NewGuid():N}";
        IQMReviewGrain grain = GetReview(reviewId);

        // Assign
        await grain.AssignReviewAsync(
            incidentId, QMReviewType.PeerReview,
            "REVIEWER-001", "Dr. Wilson", "Department Chief",
            DateTime.UtcNow.AddDays(30), confidential: true);

        // Start
        await grain.StartReviewAsync();
        QMReviewState stateStarted = await grain.GetReviewAsync();
        Assert.That(stateStarted.Status, Is.EqualTo(QMReviewStatus.InProgress));

        // Record findings
        await grain.RecordFindingsAsync(
            "Communication failure between pharmacy and nursing",
            ReviewFinding.CommunicationBreakdown,
            new List<string> { "Shift change", "No read-back protocol" },
            "Lack of standardized communication during shift handoff",
            new List<string> { "No electronic med reconciliation" },
            "Staff fatigue at end of 12-hour shift",
            "Noisy environment during shift change");

        // Add recommendation and action item
        await grain.AddRecommendationAsync("Implement standardized handoff protocol (I-PASS)");
        await grain.AddActionItemAsync("Develop I-PASS training module", "Education Dept", DateTime.UtcNow.AddDays(60));

        // Complete
        await grain.CompleteReviewAsync(
            "Communication failure was root cause; corrective actions initiated",
            "Standardized handoff reduces medication errors by 30%");

        QMReviewState stateCompleted = await grain.GetReviewAsync();
        Assert.That(stateCompleted.Status, Is.EqualTo(QMReviewStatus.Completed));
        Assert.That(stateCompleted.PrimaryFinding, Is.EqualTo(ReviewFinding.CommunicationBreakdown));
        Assert.That(stateCompleted.Recommendations, Has.Count.EqualTo(1));
        Assert.That(stateCompleted.ActionItems, Has.Count.EqualTo(1));
        Assert.That(stateCompleted.FinalConclusion, Does.Contain("corrective actions"));
        Assert.That(stateCompleted.LessonsLearned, Does.Contain("30%"));

        // Approve
        await grain.ApproveReviewAsync();
        QMReviewState stateApproved = await grain.GetReviewAsync();
        Assert.That(stateApproved.Status, Is.EqualTo(QMReviewStatus.Approved));
    }

    [Test]
    public async Task ReviewIndex_UpsertAndQueryForIncident()
    {
        IQMReviewIndexGrain index = GetReviewIndex();

        string reviewId = $"QM-REVIEW-{Guid.NewGuid():N}";
        string incidentId = $"QM-INCIDENT-{Guid.NewGuid():N}";

        await index.UpsertReviewAsync(new QMReviewIndexEntry
        {
            ReviewId = reviewId, IncidentId = incidentId,
            ReviewType = QMReviewType.PeerReview,
            Status = QMReviewStatus.Pending,
            ReviewerName = "Dr. Test", AssignedTo = "QM-TEAM-1",
            DueDate = DateTime.UtcNow.AddDays(30)
        });

        List<QMReviewIndexEntry> results = await index.GetReviewsForIncidentAsync(incidentId);
        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].ReviewId, Is.EqualTo(reviewId));
    }
}
