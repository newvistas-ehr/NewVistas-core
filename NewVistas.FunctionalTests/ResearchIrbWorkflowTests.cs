// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Research / IRB Tracking — VistA Research Module ~File #900.
/// Tests end-to-end workflows via direct grain factory access (system-level module).
/// </summary>
[TestFixture]
public class ResearchIrbWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IResearchStudyGrain GetStudy(string id)
        => _cluster.GrainFactory.GetGrain<IResearchStudyGrain>(id);

    private IResearchStudyIndexGrain GetStudyIndex()
        => _cluster.GrainFactory.GetGrain<IResearchStudyIndexGrain>("IRB-STUDY-IDX");

    private IResearchSubjectGrain GetSubject(string id)
        => _cluster.GrainFactory.GetGrain<IResearchSubjectGrain>(id);

    private IResearchSubjectIndexGrain GetSubjectIndex(string studyId)
        => _cluster.GrainFactory.GetGrain<IResearchSubjectIndexGrain>($"IRB-SUBJECT-IDX:{studyId}");

    // ── Study Tests ──────────────────────────────────────────────────────────

    [Test]
    public async Task CreateStudy_SetsInitialDraftStatus()
    {
        string studyId = $"IRB-STUDY-{Guid.NewGuid():N}";
        IResearchStudyGrain grain = GetStudy(studyId);

        await grain.CreateStudyAsync(
            "IRB-2024-001", "Effects of Telehealth on PTSD Outcomes",
            "Telehealth PTSD Study",
            "Dr. Sarah Adams", "EMP-001", "VA Research",
            IrbStudyType.Interventional, IrbStudyPhase.Phase3,
            "Mental Health", 200,
            "Randomized controlled trial comparing telehealth vs in-person CBT for PTSD");

        ResearchStudyState state = await grain.GetStudyAsync();

        Assert.That(state.IrbProtocolNumber, Is.EqualTo("IRB-2024-001"));
        Assert.That(state.Title, Does.Contain("Telehealth"));
        Assert.That(state.PrincipalInvestigator, Is.EqualTo("Dr. Sarah Adams"));
        Assert.That(state.StudyType, Is.EqualTo(IrbStudyType.Interventional));
        Assert.That(state.Phase, Is.EqualTo(IrbStudyPhase.Phase3));
        Assert.That(state.Status, Is.EqualTo(IrbStudyStatus.Draft));
        Assert.That(state.TargetEnrollment, Is.EqualTo(200));
    }

    [Test]
    public async Task OpenForEnrollment_TransitionsFromDraft()
    {
        string studyId = $"IRB-STUDY-{Guid.NewGuid():N}";
        IResearchStudyGrain grain = GetStudy(studyId);

        await grain.CreateStudyAsync(
            "IRB-2024-002", "Biomarkers in Traumatic Brain Injury",
            "TBI Biomarker Study",
            "Dr. James Brown", "EMP-002", "NIH",
            IrbStudyType.Observational, IrbStudyPhase.NotApplicable,
            "Neurology", 150,
            "Observational study of blood biomarkers in TBI patients");

        DateTime approval = DateTime.UtcNow;
        DateTime expiration = approval.AddYears(1);
        await grain.OpenForEnrollmentAsync(approval, expiration, expiration.AddMonths(-1));

        ResearchStudyState state = await grain.GetStudyAsync();
        Assert.That(state.Status, Is.EqualTo(IrbStudyStatus.OpenForEnrollment));
        Assert.That(state.InitialApprovalDate, Is.Not.Null);
        Assert.That(state.CurrentExpirationDate, Is.Not.Null);
    }

    [Test]
    public async Task AddArm_AppendsStudyArm()
    {
        string studyId = $"IRB-STUDY-{Guid.NewGuid():N}";
        IResearchStudyGrain grain = GetStudy(studyId);

        await grain.CreateStudyAsync(
            "IRB-2024-003", "Weight Loss Intervention Trial",
            "Weight Loss RCT",
            "Dr. Kim Lee", "EMP-003", "VA Research",
            IrbStudyType.Interventional, IrbStudyPhase.Phase2,
            "Endocrinology", 100, "Testing new GLP-1 agonist");

        await grain.AddArmAsync("Treatment - GLP-1 Agonist");
        await grain.AddArmAsync("Placebo Control");

        ResearchStudyState state = await grain.GetStudyAsync();
        Assert.That(state.StudyArms, Has.Count.EqualTo(2));
        Assert.That(state.StudyArms, Contains.Item("Placebo Control"));
    }

    [Test]
    public async Task RecordSubmission_AndUpdateDecision()
    {
        string studyId = $"IRB-STUDY-{Guid.NewGuid():N}";
        IResearchStudyGrain grain = GetStudy(studyId);

        await grain.CreateStudyAsync(
            "IRB-2024-004", "Sleep Apnea Device Study",
            "Sleep Device Study",
            "Dr. Mark Wilson", "EMP-004", "MedTech Inc",
            IrbStudyType.DeviceStudy, IrbStudyPhase.NotApplicable,
            "Pulmonology", 50, "Testing new CPAP device");

        string submissionId = $"SUB-{Guid.NewGuid():N}";
        await grain.RecordSubmissionAsync(
            submissionId, IrbSubmissionType.InitialApplication,
            DateTime.UtcNow.AddDays(-14), "Initial application for IRB review");

        await grain.UpdateSubmissionDecisionAsync(
            submissionId, IrbSubmissionStatus.Approved,
            "Approved with minor modifications",
            DateTime.UtcNow, DateTime.UtcNow.AddYears(1));

        ResearchStudyState state = await grain.GetStudyAsync();
        Assert.That(state.Submissions, Has.Count.EqualTo(1));
        Assert.That(state.Submissions[0].Status, Is.EqualTo(IrbSubmissionStatus.Approved));
        Assert.That(state.Submissions[0].Decision, Does.Contain("minor modifications"));
    }

    [Test]
    public async Task CloseToEnrollment_TransitionsCorrectly()
    {
        string studyId = $"IRB-STUDY-{Guid.NewGuid():N}";
        IResearchStudyGrain grain = GetStudy(studyId);

        await grain.CreateStudyAsync(
            "IRB-2024-005", "Pain Management Study",
            "Pain Study", "Dr. Davis", "EMP-005", "VA",
            IrbStudyType.Interventional, IrbStudyPhase.Phase2,
            "Anesthesiology", 75, "Testing non-opioid pain protocol");

        await grain.OpenForEnrollmentAsync(DateTime.UtcNow, DateTime.UtcNow.AddYears(1), null);
        await grain.CloseToEnrollmentAsync();

        ResearchStudyState state = await grain.GetStudyAsync();
        Assert.That(state.Status, Is.EqualTo(IrbStudyStatus.ClosedToEnrollment));
    }

    [Test]
    public async Task CompleteStudy_SetsCompletedStatus()
    {
        string studyId = $"IRB-STUDY-{Guid.NewGuid():N}";
        IResearchStudyGrain grain = GetStudy(studyId);

        await grain.CreateStudyAsync(
            "IRB-2024-006", "Diabetes Prevention Study",
            "DPS", "Dr. Miller", "EMP-006", "CDC",
            IrbStudyType.Behavioral, IrbStudyPhase.NotApplicable,
            "Endocrinology", 200, "Lifestyle intervention study");

        await grain.OpenForEnrollmentAsync(DateTime.UtcNow.AddYears(-2), DateTime.UtcNow.AddMonths(6), null);
        await grain.CloseToEnrollmentAsync();
        await grain.CompleteStudyAsync();

        ResearchStudyState state = await grain.GetStudyAsync();
        Assert.That(state.Status, Is.EqualTo(IrbStudyStatus.Completed));
    }

    // ── Study Index Tests ────────────────────────────────────────────────────

    [Test]
    public async Task StudyIndex_UpsertAndQueryByPI()
    {
        IResearchStudyIndexGrain index = GetStudyIndex();

        string studyId = $"IRB-STUDY-{Guid.NewGuid():N}";
        await index.UpsertStudyAsync(new IrbStudyIndexEntry
        {
            StudyId = studyId, IrbProtocolNumber = "IRB-IDX-001",
            Title = "Index Test Study",
            PrincipalInvestigator = "Dr. Test PI",
            StudyType = IrbStudyType.Interventional,
            Phase = IrbStudyPhase.Phase3,
            Status = IrbStudyStatus.OpenForEnrollment,
            CurrentEnrollment = 25, TargetEnrollment = 100,
            CurrentExpirationDate = DateTime.UtcNow.AddYears(1)
        });

        List<IrbStudyIndexEntry> results = await index.GetStudiesByPIAsync("Dr. Test PI");
        Assert.That(results.Any(s => s.StudyId == studyId), Is.True);
    }

    // ── Subject Tests ────────────────────────────────────────────────────────

    [Test]
    public async Task EnrollSubject_CreatesEnrolledRecord()
    {
        string subjectId = $"IRB-SUBJECT-{Guid.NewGuid():N}";
        string studyId = $"IRB-STUDY-{Guid.NewGuid():N}";
        string patientId = $"PAT-{Guid.NewGuid():N}";
        IResearchSubjectGrain grain = GetSubject(subjectId);

        await grain.EnrollSubjectAsync(
            studyId, "Telehealth PTSD Study",
            patientId, "DOE,JOHN", new DateTime(1980, 6, 15),
            DateTime.UtcNow.AddDays(-7), DateTime.UtcNow,
            DateTime.UtcNow, ConsentType.Written,
            "Research Coordinator Smith", "Treatment Arm");

        ResearchSubjectState state = await grain.GetSubjectAsync();

        Assert.That(state.StudyId, Is.EqualTo(studyId));
        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.ConsentType, Is.EqualTo(ConsentType.Written));
        Assert.That(state.Arm, Is.EqualTo("Treatment Arm"));
        Assert.That(state.EnrollmentStatus, Is.EqualTo(SubjectEnrollmentStatus.Enrolled));
    }

    [Test]
    public async Task ActivateSubject_TransitionsToActive()
    {
        string subjectId = $"IRB-SUBJECT-{Guid.NewGuid():N}";
        IResearchSubjectGrain grain = GetSubject(subjectId);

        await grain.EnrollSubjectAsync(
            "STUDY-001", "Test Study", "PAT-001", "SMITH,JANE", null,
            DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow,
            ConsentType.Written, "Coordinator", "Control");

        await grain.ActivateSubjectAsync();

        ResearchSubjectState state = await grain.GetSubjectAsync();
        Assert.That(state.EnrollmentStatus, Is.EqualTo(SubjectEnrollmentStatus.Active));
    }

    [Test]
    public async Task WithdrawSubject_SetsWithdrawnStatus()
    {
        string subjectId = $"IRB-SUBJECT-{Guid.NewGuid():N}";
        IResearchSubjectGrain grain = GetSubject(subjectId);

        await grain.EnrollSubjectAsync(
            "STUDY-002", "Another Study", "PAT-002", "GREEN,BOB", null,
            DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow,
            ConsentType.Written, "Coordinator", "Treatment");

        await grain.WithdrawSubjectAsync("Patient requests withdrawal", DateTime.UtcNow);

        ResearchSubjectState state = await grain.GetSubjectAsync();
        Assert.That(state.EnrollmentStatus, Is.EqualTo(SubjectEnrollmentStatus.Withdrawn));
        Assert.That(state.WithdrawalReason, Does.Contain("requests withdrawal"));
        Assert.That(state.WithdrawalDate, Is.Not.Null);
    }

    [Test]
    public async Task CompleteSubject_SetsCompletedStatus()
    {
        string subjectId = $"IRB-SUBJECT-{Guid.NewGuid():N}";
        IResearchSubjectGrain grain = GetSubject(subjectId);

        await grain.EnrollSubjectAsync(
            "STUDY-003", "Final Study", "PAT-003", "WHITE,TOM", null,
            DateTime.UtcNow.AddMonths(-6), DateTime.UtcNow.AddMonths(-6),
            DateTime.UtcNow.AddMonths(-6), ConsentType.Written, "Coord", "Arm A");

        await grain.ActivateSubjectAsync();
        await grain.CompleteSubjectAsync(DateTime.UtcNow);

        ResearchSubjectState state = await grain.GetSubjectAsync();
        Assert.That(state.EnrollmentStatus, Is.EqualTo(SubjectEnrollmentStatus.Completed));
        Assert.That(state.CompletionDate, Is.Not.Null);
    }

    // ── Subject Index Tests ──────────────────────────────────────────────────

    [Test]
    public async Task SubjectIndex_QueryActiveSubjects()
    {
        string studyId = $"IRB-STUDY-{Guid.NewGuid():N}";
        IResearchSubjectIndexGrain index = GetSubjectIndex(studyId);

        string subjectId = $"IRB-SUBJECT-{Guid.NewGuid():N}";
        await index.UpsertSubjectAsync(new ResearchSubjectIndexEntry
        {
            SubjectId = subjectId, StudyId = studyId,
            PatientId = "PAT-SIDX-1", PatientName = "TEST,SUBJECT",
            EnrollmentStatus = SubjectEnrollmentStatus.Active,
            EnrollmentDate = DateTime.UtcNow,
            ConsentDate = DateTime.UtcNow, Arm = "Treatment"
        });

        List<ResearchSubjectIndexEntry> active = await index.GetActiveSubjectsAsync();
        Assert.That(active.Any(s => s.SubjectId == subjectId), Is.True);
    }
}
