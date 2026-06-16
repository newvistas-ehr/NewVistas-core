// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

// ═══════════════════════════════════════════════════════════════════════════
// QMIncidentGrainTests
// ═══════════════════════════════════════════════════════════════════════════

[TestFixture]
public class QMIncidentGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IQMIncidentGrain NewIncident() =>
        _cluster.GrainFactory.GetGrain<IQMIncidentGrain>($"QM-INCIDENT:{Guid.NewGuid()}");

    private static async Task ReportBasicIncident(IQMIncidentGrain grain, string patientId = "PAT-001")
    {
        await grain.ReportIncidentAsync(
            patientId,
            "John Doe",
            DateTime.UtcNow.AddHours(-2),
            OccurrenceCategory.MedicationError,
            "Patient received double dose of metoprolol due to transcription error.",
            "4 West",
            "4W-MED",
            OccurrenceSeverity.ModerateHarm,
            "Nurse Smith",
            "RN",
            "Administered activated charcoal, contacted physician.",
            "Heart failure",
            string.Empty,
            "Metoprolol 50mg (double dose)",
            string.Empty);
    }

    [Test]
    public async Task QMIncidentGrain_CanReportIncident()
    {
        IQMIncidentGrain grain = NewIncident();
        await ReportBasicIncident(grain);

        QMIncidentState state = await grain.GetIncidentAsync();
        Assert.That(state.PatientId, Is.EqualTo("PAT-001"));
        Assert.That(state.Category, Is.EqualTo(OccurrenceCategory.MedicationError));
        Assert.That(state.Severity, Is.EqualTo(OccurrenceSeverity.ModerateHarm));
        Assert.That(state.Status, Is.EqualTo(IncidentStatus.Reported));
        Assert.That(state.MedicationInvolved, Does.Contain("Metoprolol"));
    }

    [Test]
    public async Task QMIncidentGrain_CanUpdateOutcome()
    {
        IQMIncidentGrain grain = NewIncident();
        await ReportBasicIncident(grain);
        await grain.UpdateOutcomeAsync(
            "Patient stabilized. BP normalized after 4 hours monitoring.",
            patientNotified: true,
            familyNotified: true);

        QMIncidentState state = await grain.GetIncidentAsync();
        Assert.That(state.OutcomeDescription, Does.Contain("stabilized"));
        Assert.That(state.PatientNotified, Is.True);
        Assert.That(state.FamilyNotified, Is.True);
    }

    [Test]
    public async Task QMIncidentGrain_CanAddStaffInvolved()
    {
        IQMIncidentGrain grain = NewIncident();
        await ReportBasicIncident(grain);
        await grain.AddStaffInvolvedAsync("Dr. Jones");
        await grain.AddStaffInvolvedAsync("Pharmacist Williams");
        await grain.AddStaffInvolvedAsync("Dr. Jones"); // duplicate — no double-add

        QMIncidentState state = await grain.GetIncidentAsync();
        Assert.That(state.StaffInvolved, Has.Count.EqualTo(2));
        Assert.That(state.StaffInvolved, Contains.Item("Dr. Jones"));
    }

    [Test]
    public async Task QMIncidentGrain_CanAddReviewAndStatusUpdatesToPeerReview()
    {
        IQMIncidentGrain grain = NewIncident();
        await ReportBasicIncident(grain);
        await grain.AddReviewToIncidentAsync("QM-REVIEW:abc", QMReviewType.PeerReview);

        QMIncidentState state = await grain.GetIncidentAsync();
        Assert.That(state.Status, Is.EqualTo(IncidentStatus.PeerReviewAssigned));
        Assert.That(state.ReviewIds, Contains.Item("QM-REVIEW:abc"));
    }

    [Test]
    public async Task QMIncidentGrain_CanAddReviewAndStatusUpdatesToRCA()
    {
        IQMIncidentGrain grain = NewIncident();
        await ReportBasicIncident(grain);
        await grain.AddReviewToIncidentAsync("QM-REVIEW:xyz", QMReviewType.RootCauseAnalysis);

        QMIncidentState state = await grain.GetIncidentAsync();
        Assert.That(state.Status, Is.EqualTo(IncidentStatus.RCAInProgress));
    }

    [Test]
    public async Task QMIncidentGrain_CanSetRootCause()
    {
        IQMIncidentGrain grain = NewIncident();
        await ReportBasicIncident(grain);
        await grain.SetRootCauseIdentifiedAsync(true,
            "Inadequate double-check process for high-alert medications.");

        QMIncidentState state = await grain.GetIncidentAsync();
        Assert.That(state.RootCauseIdentified, Is.True);
        Assert.That(state.CorrectiveActionsSummary, Does.Contain("double-check"));
    }

    [Test]
    public async Task QMIncidentGrain_CanCloseIncident()
    {
        IQMIncidentGrain grain = NewIncident();
        await ReportBasicIncident(grain);
        await grain.CloseIncidentAsync();

        QMIncidentState state = await grain.GetIncidentAsync();
        Assert.That(state.Status, Is.EqualTo(IncidentStatus.Closed));
        Assert.That(state.ClosedDate, Is.Not.Null);
    }

    [Test]
    public async Task QMIncidentGrain_CanVoidIncident()
    {
        IQMIncidentGrain grain = NewIncident();
        await ReportBasicIncident(grain);
        await grain.VoidIncidentAsync("Duplicate entry — see QM-INCIDENT:12345.");

        QMIncidentState state = await grain.GetIncidentAsync();
        Assert.That(state.Status, Is.EqualTo(IncidentStatus.Voided));
        Assert.That(state.VoidReason, Does.Contain("Duplicate"));
    }

    [Test]
    public async Task QMIncidentGrain_IncidentIdMatchesGrainKey()
    {
        IQMIncidentGrain grain = NewIncident();
        await ReportBasicIncident(grain);

        QMIncidentState state = await grain.GetIncidentAsync();
        Assert.That(state.IncidentId, Is.EqualTo(grain.GetPrimaryKeyString()));
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// QMIncidentIndexGrainTests
// ═══════════════════════════════════════════════════════════════════════════

[TestFixture]
public class QMIncidentIndexGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IQMIncidentIndexGrain NewIndex() =>
        _cluster.GrainFactory.GetGrain<IQMIncidentIndexGrain>($"QM-IDX-{Guid.NewGuid()}");

    private static QMIncidentIndexEntry MakeEntry(
        string patientId,
        OccurrenceSeverity severity,
        IncidentStatus status,
        OccurrenceCategory category = OccurrenceCategory.MedicationError) => new()
    {
        IncidentId = $"QM-INCIDENT:{Guid.NewGuid()}",
        PatientId = patientId,
        PatientName = "Test Patient",
        OccurrenceDate = DateTime.UtcNow.AddDays(-1),
        Category = category,
        Severity = severity,
        Status = status,
        Location = "3 East",
        WardUnit = "3E-ICU",
        ReviewCount = 0
    };

    [Test]
    public async Task QMIncidentIndexGrain_CanUpsertAndRetrieve()
    {
        IQMIncidentIndexGrain index = NewIndex();
        QMIncidentIndexEntry entry = MakeEntry("PAT-001", OccurrenceSeverity.NoHarm, IncidentStatus.Reported);
        await index.UpsertIncidentAsync(entry);

        List<QMIncidentIndexEntry> all = await index.GetAllIncidentsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].IncidentId, Is.EqualTo(entry.IncidentId));
    }

    [Test]
    public async Task QMIncidentIndexGrain_UpsertUpdatesExistingEntry()
    {
        IQMIncidentIndexGrain index = NewIndex();
        QMIncidentIndexEntry entry = MakeEntry("PAT-001", OccurrenceSeverity.NoHarm, IncidentStatus.Reported);
        await index.UpsertIncidentAsync(entry);
        entry.Status = IncidentStatus.Closed;
        await index.UpsertIncidentAsync(entry);

        List<QMIncidentIndexEntry> all = await index.GetAllIncidentsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Status, Is.EqualTo(IncidentStatus.Closed));
    }

    [Test]
    public async Task QMIncidentIndexGrain_CanGetBySeverity()
    {
        IQMIncidentIndexGrain index = NewIndex();
        await index.UpsertIncidentAsync(MakeEntry("PAT-001", OccurrenceSeverity.NearMiss, IncidentStatus.Reported));
        await index.UpsertIncidentAsync(MakeEntry("PAT-002", OccurrenceSeverity.SevereHarm, IncidentStatus.Reported));
        await index.UpsertIncidentAsync(MakeEntry("PAT-003", OccurrenceSeverity.SevereHarm, IncidentStatus.Closed));

        List<QMIncidentIndexEntry> severe = await index.GetIncidentsBySeverityAsync(OccurrenceSeverity.SevereHarm);
        Assert.That(severe, Has.Count.EqualTo(2));
        Assert.That(severe.All(i => i.Severity == OccurrenceSeverity.SevereHarm), Is.True);
    }

    [Test]
    public async Task QMIncidentIndexGrain_CanGetByStatus()
    {
        IQMIncidentIndexGrain index = NewIndex();
        await index.UpsertIncidentAsync(MakeEntry("PAT-001", OccurrenceSeverity.NoHarm, IncidentStatus.Reported));
        await index.UpsertIncidentAsync(MakeEntry("PAT-002", OccurrenceSeverity.NoHarm, IncidentStatus.Reported));
        await index.UpsertIncidentAsync(MakeEntry("PAT-003", OccurrenceSeverity.NoHarm, IncidentStatus.Closed));

        List<QMIncidentIndexEntry> open = await index.GetIncidentsByStatusAsync(IncidentStatus.Reported);
        Assert.That(open, Has.Count.EqualTo(2));
        Assert.That(open.All(i => i.Status == IncidentStatus.Reported), Is.True);
    }

    [Test]
    public async Task QMIncidentIndexGrain_CanGetByPatient()
    {
        IQMIncidentIndexGrain index = NewIndex();
        await index.UpsertIncidentAsync(MakeEntry("PAT-AAA", OccurrenceSeverity.NoHarm, IncidentStatus.Reported));
        await index.UpsertIncidentAsync(MakeEntry("PAT-AAA", OccurrenceSeverity.MinorHarm, IncidentStatus.Closed));
        await index.UpsertIncidentAsync(MakeEntry("PAT-BBB", OccurrenceSeverity.NearMiss, IncidentStatus.Reported));

        List<QMIncidentIndexEntry> forAaa = await index.GetIncidentsByPatientAsync("PAT-AAA");
        Assert.That(forAaa, Has.Count.EqualTo(2));
        Assert.That(forAaa.All(i => i.PatientId == "PAT-AAA"), Is.True);
    }

    [Test]
    public async Task QMIncidentIndexGrain_CanGetByCategory()
    {
        IQMIncidentIndexGrain index = NewIndex();
        await index.UpsertIncidentAsync(MakeEntry("PAT-001", OccurrenceSeverity.MinorHarm, IncidentStatus.Reported, OccurrenceCategory.FallWithInjury));
        await index.UpsertIncidentAsync(MakeEntry("PAT-002", OccurrenceSeverity.NoHarm, IncidentStatus.Reported, OccurrenceCategory.MedicationError));
        await index.UpsertIncidentAsync(MakeEntry("PAT-003", OccurrenceSeverity.ModerateHarm, IncidentStatus.Reported, OccurrenceCategory.FallWithInjury));

        List<QMIncidentIndexEntry> falls = await index.GetIncidentsByCategoryAsync(OccurrenceCategory.FallWithInjury);
        Assert.That(falls, Has.Count.EqualTo(2));
        Assert.That(falls.All(i => i.Category == OccurrenceCategory.FallWithInjury), Is.True);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// QMReviewGrainTests
// ═══════════════════════════════════════════════════════════════════════════

[TestFixture]
public class QMReviewGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IQMReviewGrain NewReview() =>
        _cluster.GrainFactory.GetGrain<IQMReviewGrain>($"QM-REVIEW:{Guid.NewGuid()}");

    private static async Task AssignBasicReview(IQMReviewGrain grain, string incidentId = "QM-INCIDENT:test")
    {
        await grain.AssignReviewAsync(
            incidentId,
            QMReviewType.PeerReview,
            "QM Committee",
            "Dr. Brown",
            "MD, Chief of Staff",
            DateTime.UtcNow.AddDays(30),
            confidential: true);
    }

    [Test]
    public async Task QMReviewGrain_CanAssignReview()
    {
        IQMReviewGrain grain = NewReview();
        await AssignBasicReview(grain);

        QMReviewState state = await grain.GetReviewAsync();
        Assert.That(state.ReviewType, Is.EqualTo(QMReviewType.PeerReview));
        Assert.That(state.Status, Is.EqualTo(QMReviewStatus.Pending));
        Assert.That(state.ReviewerName, Is.EqualTo("Dr. Brown"));
        Assert.That(state.Confidential, Is.True);
    }

    [Test]
    public async Task QMReviewGrain_CanStartReview()
    {
        IQMReviewGrain grain = NewReview();
        await AssignBasicReview(grain);
        await grain.StartReviewAsync();

        QMReviewState state = await grain.GetReviewAsync();
        Assert.That(state.Status, Is.EqualTo(QMReviewStatus.InProgress));
    }

    [Test]
    public async Task QMReviewGrain_CanRecordFindings()
    {
        IQMReviewGrain grain = NewReview();
        await AssignBasicReview(grain);
        await grain.StartReviewAsync();
        await grain.RecordFindingsAsync(
            summary: "Review of medication double-dose event.",
            primaryFinding: ReviewFinding.ProcessIssue,
            contributingFactors: new List<string> { "Staffing shortage", "Verbal order misinterpretation" },
            rootCause: "Lack of mandatory second nurse verification for high-alert medications.",
            systemFailures: new List<string> { "No CPOE safeguard for duplicate orders" },
            humanFactors: "Fatigue — nurse at hour 13 of 12-hour shift.",
            environmentalFactors: "High census, distracting environment.");

        QMReviewState state = await grain.GetReviewAsync();
        Assert.That(state.PrimaryFinding, Is.EqualTo(ReviewFinding.ProcessIssue));
        Assert.That(state.ContributingFactors, Has.Count.EqualTo(2));
        Assert.That(state.RootCause, Does.Contain("verification"));
        Assert.That(state.SystemFailures, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task QMReviewGrain_CanAddRecommendation()
    {
        IQMReviewGrain grain = NewReview();
        await AssignBasicReview(grain);
        await grain.AddRecommendationAsync("Implement mandatory pharmacist verification for high-alert medications.");
        await grain.AddRecommendationAsync("Require CPOE alerts for duplicate orders within 4 hours.");
        await grain.AddRecommendationAsync("Implement mandatory pharmacist verification for high-alert medications."); // duplicate

        QMReviewState state = await grain.GetReviewAsync();
        Assert.That(state.Recommendations, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task QMReviewGrain_CanAddActionItem()
    {
        IQMReviewGrain grain = NewReview();
        await AssignBasicReview(grain);
        await grain.AddActionItemAsync(
            "Update high-alert medication policy to require dual-nurse verification.",
            "Director of Nursing",
            DateTime.UtcNow.AddDays(60));
        await grain.AddActionItemAsync(
            "Configure CPOE duplicate-order alert in pharmacy module.",
            "IT/Pharmacy",
            DateTime.UtcNow.AddDays(45));

        QMReviewState state = await grain.GetReviewAsync();
        Assert.That(state.ActionItems, Has.Count.EqualTo(2));
        Assert.That(state.ActionItems[0].Status, Is.EqualTo(ActionItemStatus.Pending));
    }

    [Test]
    public async Task QMReviewGrain_CanCompleteReview()
    {
        IQMReviewGrain grain = NewReview();
        await AssignBasicReview(grain);
        await grain.StartReviewAsync();
        await grain.CompleteReviewAsync(
            "Process failure identified. Corrective actions assigned to Nursing and IT.",
            "Mandatory dual verification for high-alert medications prevents adverse outcomes.");

        QMReviewState state = await grain.GetReviewAsync();
        Assert.That(state.Status, Is.EqualTo(QMReviewStatus.Completed));
        Assert.That(state.CompletedDate, Is.Not.Null);
        Assert.That(state.FinalConclusion, Does.Contain("Process failure"));
    }

    [Test]
    public async Task QMReviewGrain_CanApproveReview()
    {
        IQMReviewGrain grain = NewReview();
        await AssignBasicReview(grain);
        await grain.CompleteReviewAsync("Approved finding.", "Key lesson.");
        await grain.ApproveReviewAsync();

        QMReviewState state = await grain.GetReviewAsync();
        Assert.That(state.Status, Is.EqualTo(QMReviewStatus.Approved));
    }

    [Test]
    public async Task QMReviewGrain_ReviewIdMatchesGrainKey()
    {
        IQMReviewGrain grain = NewReview();
        await AssignBasicReview(grain);

        QMReviewState state = await grain.GetReviewAsync();
        Assert.That(state.ReviewId, Is.EqualTo(grain.GetPrimaryKeyString()));
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// QMReviewIndexGrainTests
// ═══════════════════════════════════════════════════════════════════════════

[TestFixture]
public class QMReviewIndexGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IQMReviewIndexGrain NewIndex() =>
        _cluster.GrainFactory.GetGrain<IQMReviewIndexGrain>($"QM-REV-IDX-{Guid.NewGuid()}");

    private static QMReviewIndexEntry MakeReviewEntry(string incidentId, QMReviewStatus status, DateTime dueDate) => new()
    {
        ReviewId = $"QM-REVIEW:{Guid.NewGuid()}",
        IncidentId = incidentId,
        ReviewType = QMReviewType.PeerReview,
        Status = status,
        ReviewerName = "Dr. Adams",
        AssignedTo = "QM Committee",
        DueDate = dueDate,
        CompletedDate = status == QMReviewStatus.Completed ? DateTime.UtcNow : null,
        ActionItemCount = 1
    };

    [Test]
    public async Task QMReviewIndexGrain_CanUpsertAndRetrieve()
    {
        IQMReviewIndexGrain index = NewIndex();
        string incidentId = $"QM-INCIDENT:{Guid.NewGuid()}";
        QMReviewIndexEntry entry = MakeReviewEntry(incidentId, QMReviewStatus.Pending, DateTime.UtcNow.AddDays(14));
        await index.UpsertReviewAsync(entry);

        List<QMReviewIndexEntry> all = await index.GetAllReviewsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].ReviewId, Is.EqualTo(entry.ReviewId));
    }

    [Test]
    public async Task QMReviewIndexGrain_UpsertUpdatesExistingEntry()
    {
        IQMReviewIndexGrain index = NewIndex();
        string incidentId = $"QM-INCIDENT:{Guid.NewGuid()}";
        QMReviewIndexEntry entry = MakeReviewEntry(incidentId, QMReviewStatus.Pending, DateTime.UtcNow.AddDays(7));
        await index.UpsertReviewAsync(entry);
        entry.Status = QMReviewStatus.Completed;
        await index.UpsertReviewAsync(entry);

        List<QMReviewIndexEntry> all = await index.GetAllReviewsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Status, Is.EqualTo(QMReviewStatus.Completed));
    }

    [Test]
    public async Task QMReviewIndexGrain_CanGetReviewsForIncident()
    {
        IQMReviewIndexGrain index = NewIndex();
        string inc1 = $"QM-INCIDENT:{Guid.NewGuid()}";
        string inc2 = $"QM-INCIDENT:{Guid.NewGuid()}";
        await index.UpsertReviewAsync(MakeReviewEntry(inc1, QMReviewStatus.Pending, DateTime.UtcNow.AddDays(10)));
        await index.UpsertReviewAsync(MakeReviewEntry(inc1, QMReviewStatus.InProgress, DateTime.UtcNow.AddDays(20)));
        await index.UpsertReviewAsync(MakeReviewEntry(inc2, QMReviewStatus.Completed, DateTime.UtcNow.AddDays(5)));

        List<QMReviewIndexEntry> forInc1 = await index.GetReviewsForIncidentAsync(inc1);
        Assert.That(forInc1, Has.Count.EqualTo(2));
        Assert.That(forInc1.All(r => r.IncidentId == inc1), Is.True);
    }

    [Test]
    public async Task QMReviewIndexGrain_CanGetPendingReviews()
    {
        IQMReviewIndexGrain index = NewIndex();
        string inc = $"QM-INCIDENT:{Guid.NewGuid()}";
        await index.UpsertReviewAsync(MakeReviewEntry(inc, QMReviewStatus.Pending, DateTime.UtcNow.AddDays(10)));
        await index.UpsertReviewAsync(MakeReviewEntry(inc, QMReviewStatus.InProgress, DateTime.UtcNow.AddDays(5)));
        await index.UpsertReviewAsync(MakeReviewEntry(inc, QMReviewStatus.Completed, DateTime.UtcNow.AddDays(-5)));

        List<QMReviewIndexEntry> pending = await index.GetPendingReviewsAsync();
        Assert.That(pending, Has.Count.EqualTo(2));
        Assert.That(pending.All(r => r.Status is QMReviewStatus.Pending or QMReviewStatus.InProgress), Is.True);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// QMIntegrationTests
// ═══════════════════════════════════════════════════════════════════════════

[TestFixture]
public class QMIntegrationTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IQMIncidentGrain NewIncident() =>
        _cluster.GrainFactory.GetGrain<IQMIncidentGrain>($"QM-INCIDENT:{Guid.NewGuid()}");

    private IQMIncidentIndexGrain IncidentIndex() =>
        _cluster.GrainFactory.GetGrain<IQMIncidentIndexGrain>($"QM-INCIDENT-IDX-{Guid.NewGuid()}");

    private IQMReviewGrain NewReview() =>
        _cluster.GrainFactory.GetGrain<IQMReviewGrain>($"QM-REVIEW:{Guid.NewGuid()}");

    private IQMReviewIndexGrain ReviewIndex() =>
        _cluster.GrainFactory.GetGrain<IQMReviewIndexGrain>($"QM-REVIEW-IDX-{Guid.NewGuid()}");

    private static async Task<string> CreateAndIndexIncident(
        IQMIncidentGrain grain, IQMIncidentIndexGrain index,
        string patientId, OccurrenceSeverity severity, IncidentStatus status = IncidentStatus.Reported)
    {
        await grain.ReportIncidentAsync(patientId, "Test Patient",
            DateTime.UtcNow.AddHours(-1), OccurrenceCategory.MedicationError,
            "Incident description.", "Ward A", "A1", severity,
            "Nurse", "RN", "Immediate action.", string.Empty,
            string.Empty, string.Empty, string.Empty);
        QMIncidentState state = await grain.GetIncidentAsync();
        await index.UpsertIncidentAsync(new QMIncidentIndexEntry
        {
            IncidentId = state.IncidentId,
            PatientId = state.PatientId,
            PatientName = state.PatientName,
            OccurrenceDate = state.OccurrenceDate,
            Category = state.Category,
            Severity = state.Severity,
            Status = state.Status,
            Location = state.Location,
            WardUnit = state.WardUnit,
            ReviewCount = state.ReviewIds.Count
        });
        return state.IncidentId;
    }

    [Test]
    public async Task Integration_ReportIncidentAndAssignReview()
    {
        IQMIncidentGrain incident = NewIncident();
        IQMIncidentIndexGrain incIndex = IncidentIndex();
        IQMReviewGrain review = NewReview();
        IQMReviewIndexGrain revIndex = ReviewIndex();

        string incidentId = await CreateAndIndexIncident(incident, incIndex, "PAT-001", OccurrenceSeverity.ModerateHarm);

        await review.AssignReviewAsync(incidentId, QMReviewType.PeerReview,
            "QM Team", "Dr. Jones", "MD", DateTime.UtcNow.AddDays(21), confidential: true);
        await incident.AddReviewToIncidentAsync(review.GetPrimaryKeyString(), QMReviewType.PeerReview);

        QMIncidentState incState = await incident.GetIncidentAsync();
        Assert.That(incState.Status, Is.EqualTo(IncidentStatus.PeerReviewAssigned));
        Assert.That(incState.ReviewIds, Has.Count.EqualTo(1));

        QMReviewState revState = await review.GetReviewAsync();
        Assert.That(revState.Status, Is.EqualTo(QMReviewStatus.Pending));
        Assert.That(revState.IncidentId, Is.EqualTo(incidentId));
    }

    [Test]
    public async Task Integration_CloseIncidentAfterReview()
    {
        IQMIncidentGrain incident = NewIncident();
        IQMIncidentIndexGrain incIndex = IncidentIndex();

        await CreateAndIndexIncident(incident, incIndex, "PAT-002", OccurrenceSeverity.NearMiss);
        await incident.CloseIncidentAsync();

        QMIncidentState state = await incident.GetIncidentAsync();
        Assert.That(state.Status, Is.EqualTo(IncidentStatus.Closed));
        Assert.That(state.ClosedDate, Is.Not.Null);
    }

    [Test]
    public async Task Integration_MultipleReviewsForOneIncident()
    {
        IQMIncidentGrain incident = NewIncident();
        IQMReviewIndexGrain revIndex = ReviewIndex();

        await incident.ReportIncidentAsync("PAT-003", "Test",
            DateTime.UtcNow, OccurrenceCategory.SurgicalError, "Desc",
            "OR", "OR-1", OccurrenceSeverity.SevereHarm,
            "Nurse", "RN", "Action", string.Empty, "Appendectomy", string.Empty, string.Empty);

        string incidentId = incident.GetPrimaryKeyString();

        IQMReviewGrain r1 = NewReview();
        IQMReviewGrain r2 = NewReview();
        await r1.AssignReviewAsync(incidentId, QMReviewType.PeerReview, "Team A", "Dr. A", "MD", DateTime.UtcNow.AddDays(14), true);
        await r2.AssignReviewAsync(incidentId, QMReviewType.RootCauseAnalysis, "Team B", "Dr. B", "MD, PhD", DateTime.UtcNow.AddDays(30), true);
        await incident.AddReviewToIncidentAsync(r1.GetPrimaryKeyString(), QMReviewType.PeerReview);
        await incident.AddReviewToIncidentAsync(r2.GetPrimaryKeyString(), QMReviewType.RootCauseAnalysis);

        QMIncidentState state = await incident.GetIncidentAsync();
        Assert.That(state.ReviewIds, Has.Count.EqualTo(2));
        Assert.That(state.Status, Is.EqualTo(IncidentStatus.RCAInProgress));
    }

    [Test]
    public async Task Integration_NearMissTracking()
    {
        IQMIncidentIndexGrain index = IncidentIndex();
        for (int i = 0; i < 3; i++)
        {
            IQMIncidentGrain incident = NewIncident();
            await CreateAndIndexIncident(incident, index, $"PAT-{i:000}", OccurrenceSeverity.NearMiss);
        }
        IQMIncidentGrain closedInc = NewIncident();
        await CreateAndIndexIncident(closedInc, index, "PAT-999", OccurrenceSeverity.ModerateHarm);

        List<QMIncidentIndexEntry> nearMisses = await index.GetIncidentsBySeverityAsync(OccurrenceSeverity.NearMiss);
        Assert.That(nearMisses, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task Integration_SevereHarmAndDeathTracking()
    {
        IQMIncidentIndexGrain index = IncidentIndex();

        IQMIncidentGrain inc1 = NewIncident();
        await CreateAndIndexIncident(inc1, index, "PAT-S01", OccurrenceSeverity.SevereHarm);
        IQMIncidentGrain inc2 = NewIncident();
        await CreateAndIndexIncident(inc2, index, "PAT-S02", OccurrenceSeverity.Death);

        List<QMIncidentIndexEntry> severe = await index.GetIncidentsBySeverityAsync(OccurrenceSeverity.SevereHarm);
        List<QMIncidentIndexEntry> death = await index.GetIncidentsBySeverityAsync(OccurrenceSeverity.Death);
        Assert.That(severe, Has.Count.EqualTo(1));
        Assert.That(death, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Integration_VoidedIncidentsCanBeRetrieved()
    {
        IQMIncidentIndexGrain index = IncidentIndex();
        IQMIncidentGrain incident = NewIncident();
        string incidentId = await CreateAndIndexIncident(incident, index, "PAT-V01", OccurrenceSeverity.NoHarm);
        await incident.VoidIncidentAsync("Duplicate entry.");
        // Update index with voided status
        QMIncidentState state = await incident.GetIncidentAsync();
        await index.UpsertIncidentAsync(new QMIncidentIndexEntry
        {
            IncidentId = state.IncidentId,
            PatientId = state.PatientId,
            PatientName = state.PatientName,
            OccurrenceDate = state.OccurrenceDate,
            Category = state.Category,
            Severity = state.Severity,
            Status = state.Status,
            Location = state.Location,
            WardUnit = state.WardUnit,
            ReviewCount = 0
        });

        List<QMIncidentIndexEntry> voided = await index.GetIncidentsByStatusAsync(IncidentStatus.Voided);
        Assert.That(voided, Has.Count.EqualTo(1));

        List<QMIncidentIndexEntry> open = await index.GetIncidentsByStatusAsync(IncidentStatus.Reported);
        Assert.That(open, Has.Count.EqualTo(0));
    }

    [Test]
    public async Task Integration_OverdueReviewsDetected()
    {
        IQMReviewIndexGrain revIndex = ReviewIndex();
        IQMReviewGrain overdueReview = NewReview();
        IQMReviewGrain activeReview = NewReview();

        await overdueReview.AssignReviewAsync("INC-001", QMReviewType.PeerReview,
            "Team A", "Dr. A", "MD", DateTime.UtcNow.AddDays(-5), true); // past due
        await activeReview.AssignReviewAsync("INC-002", QMReviewType.PeerReview,
            "Team B", "Dr. B", "MD", DateTime.UtcNow.AddDays(10), true); // future

        QMReviewState overS = await overdueReview.GetReviewAsync();
        QMReviewState activeS = await activeReview.GetReviewAsync();
        await revIndex.UpsertReviewAsync(new QMReviewIndexEntry
        {
            ReviewId = overS.ReviewId, IncidentId = overS.IncidentId,
            ReviewType = overS.ReviewType, Status = overS.Status,
            ReviewerName = overS.ReviewerName, AssignedTo = overS.AssignedTo,
            DueDate = overS.DueDate, ActionItemCount = 0
        });
        await revIndex.UpsertReviewAsync(new QMReviewIndexEntry
        {
            ReviewId = activeS.ReviewId, IncidentId = activeS.IncidentId,
            ReviewType = activeS.ReviewType, Status = activeS.Status,
            ReviewerName = activeS.ReviewerName, AssignedTo = activeS.AssignedTo,
            DueDate = activeS.DueDate, ActionItemCount = 0
        });

        List<QMReviewIndexEntry> overdue = await revIndex.GetOverdueReviewsAsync();
        Assert.That(overdue, Has.Count.EqualTo(1));
        Assert.That(overdue[0].DueDate, Is.LessThan(DateTime.UtcNow));
    }
}
