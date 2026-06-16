// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.UnitTests;

file class ResearchStudyGrainSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddMemoryGrainStorage("irbStudyStore");
    }
}

file class ResearchStudyIndexGrainSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddMemoryGrainStorage("irbStudyIndexStore");
    }
}

file class ResearchSubjectGrainSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddMemoryGrainStorage("irbSubjectStore");
    }
}

file class ResearchSubjectIndexGrainSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddMemoryGrainStorage("irbSubjectIndexStore");
    }
}

file class ResearchIRBIntegrationSiloConfigurator : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder.AddMemoryGrainStorage("irbStudyStore");
        siloBuilder.AddMemoryGrainStorage("irbStudyIndexStore");
        siloBuilder.AddMemoryGrainStorage("irbSubjectStore");
        siloBuilder.AddMemoryGrainStorage("irbSubjectIndexStore");
    }
}

// ── ResearchStudyGrain Tests ──────────────────────────────────────────────────

[TestFixture]
public class ResearchStudyGrainTests
{
    private TestCluster _cluster;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task ResearchStudyGrain_CanCreateStudy()
    {
        string studyId = $"IRB-STUDY:{Guid.NewGuid()}";
        IResearchStudyGrain grain = _cluster.GrainFactory.GetGrain<IResearchStudyGrain>(studyId);

        await grain.CreateStudyAsync(
            "IRB-2025-001", "Effect of Treatment X on Hypertension",
            "TreatX-HTN", "Smith, John MD", "EMP-001",
            "VA Research", IrbStudyType.Interventional, IrbStudyPhase.Phase2,
            "Cardiology", 50, "Randomized controlled trial of Treatment X.");

        ResearchStudyState state = await grain.GetStudyAsync();
        Assert.That(state.StudyId, Is.EqualTo(studyId));
        Assert.That(state.IrbProtocolNumber, Is.EqualTo("IRB-2025-001"));
        Assert.That(state.Status, Is.EqualTo(IrbStudyStatus.Draft));
        Assert.That(state.TargetEnrollment, Is.EqualTo(50));
        Assert.That(state.CurrentEnrollment, Is.EqualTo(0));
        Assert.That(state.StudyType, Is.EqualTo(IrbStudyType.Interventional));
    }

    [Test]
    public async Task ResearchStudyGrain_CanOpenForEnrollment()
    {
        string studyId = $"IRB-STUDY:{Guid.NewGuid()}";
        IResearchStudyGrain grain = _cluster.GrainFactory.GetGrain<IResearchStudyGrain>(studyId);

        await grain.CreateStudyAsync("IRB-2025-002", "Observational Study Y", "ObsY",
            "Jones, Mary MD", "EMP-002", "NIH", IrbStudyType.Observational, IrbStudyPhase.NotApplicable,
            "Oncology", 100, "Prospective observational study.");
        await grain.OpenForEnrollmentAsync(
            DateTime.UtcNow, DateTime.UtcNow.AddYears(1), DateTime.UtcNow.AddMonths(11));

        ResearchStudyState state = await grain.GetStudyAsync();
        Assert.That(state.Status, Is.EqualTo(IrbStudyStatus.OpenForEnrollment));
        Assert.That(state.InitialApprovalDate, Is.Not.Null);
        Assert.That(state.CurrentExpirationDate, Is.Not.Null);
    }

    [Test]
    public async Task ResearchStudyGrain_CanAddStudyArmsWithoutDuplicates()
    {
        string studyId = $"IRB-STUDY:{Guid.NewGuid()}";
        IResearchStudyGrain grain = _cluster.GrainFactory.GetGrain<IResearchStudyGrain>(studyId);

        await grain.CreateStudyAsync("IRB-2025-003", "Drug Z Phase 3", "DrugZ-P3",
            "Brown, Tim MD", "EMP-003", "Pharma Co", IrbStudyType.Interventional, IrbStudyPhase.Phase3,
            "Oncology", 200, "Phase 3 trial.");
        await grain.AddArmAsync("Treatment Arm A");
        await grain.AddArmAsync("Placebo Arm");
        await grain.AddArmAsync("Treatment Arm A"); // duplicate

        ResearchStudyState state = await grain.GetStudyAsync();
        Assert.That(state.StudyArms, Has.Count.EqualTo(2));
        Assert.That(state.StudyArms, Contains.Item("Treatment Arm A"));
        Assert.That(state.StudyArms, Contains.Item("Placebo Arm"));
    }

    [Test]
    public async Task ResearchStudyGrain_CanRecordAndUpdateSubmission()
    {
        string studyId = $"IRB-STUDY:{Guid.NewGuid()}";
        IResearchStudyGrain grain = _cluster.GrainFactory.GetGrain<IResearchStudyGrain>(studyId);

        await grain.CreateStudyAsync("IRB-2025-004", "Registry Study", "Reg-Study",
            "Davis, Ann MD", "EMP-004", "VA", IrbStudyType.Registry, IrbStudyPhase.NotApplicable,
            "Pulmonology", 500, "National registry.");
        string submissionId = Guid.NewGuid().ToString();
        await grain.RecordSubmissionAsync(submissionId, IrbSubmissionType.InitialApplication,
            DateTime.UtcNow.AddDays(-10), "Initial IRB application.");
        await grain.UpdateSubmissionDecisionAsync(submissionId, IrbSubmissionStatus.Approved,
            "Approved with standard conditions", DateTime.UtcNow, DateTime.UtcNow.AddYears(1));

        ResearchStudyState state = await grain.GetStudyAsync();
        Assert.That(state.Submissions, Has.Count.EqualTo(1));
        Assert.That(state.Submissions[0].Status, Is.EqualTo(IrbSubmissionStatus.Approved));
        Assert.That(state.Submissions[0].Decision, Is.EqualTo("Approved with standard conditions"));
        Assert.That(state.CurrentExpirationDate, Is.Not.Null);
    }

    [Test]
    public async Task ResearchStudyGrain_CanIncrementAndDecrementEnrollment()
    {
        string studyId = $"IRB-STUDY:{Guid.NewGuid()}";
        IResearchStudyGrain grain = _cluster.GrainFactory.GetGrain<IResearchStudyGrain>(studyId);

        await grain.CreateStudyAsync("IRB-2025-005", "Device Trial", "DevTrial",
            "Evans, Rick MD", "EMP-005", "MedTech Inc", IrbStudyType.DeviceStudy, IrbStudyPhase.Phase2,
            "Surgery", 30, "Novel device evaluation.");
        await grain.IncrementEnrollmentAsync();
        await grain.IncrementEnrollmentAsync();
        await grain.IncrementEnrollmentAsync();
        await grain.DecrementEnrollmentAsync(); // withdrawal

        ResearchStudyState state = await grain.GetStudyAsync();
        Assert.That(state.CurrentEnrollment, Is.EqualTo(2));
    }

    [Test]
    public async Task ResearchStudyGrain_CanCompleteStudy()
    {
        string studyId = $"IRB-STUDY:{Guid.NewGuid()}";
        IResearchStudyGrain grain = _cluster.GrainFactory.GetGrain<IResearchStudyGrain>(studyId);

        await grain.CreateStudyAsync("IRB-2025-006", "Behavioral Intervention", "BehInt",
            "Ford, Lisa PhD", "EMP-006", "NIH", IrbStudyType.Behavioral, IrbStudyPhase.NotApplicable,
            "Mental Health", 80, "CBT intervention study.");
        await grain.OpenForEnrollmentAsync(DateTime.UtcNow, DateTime.UtcNow.AddYears(1), null);
        await grain.CompleteStudyAsync();

        ResearchStudyState state = await grain.GetStudyAsync();
        Assert.That(state.Status, Is.EqualTo(IrbStudyStatus.Completed));
    }
}

// ── ResearchStudyIndexGrain Tests ─────────────────────────────────────────────

[TestFixture]
public class ResearchStudyIndexGrainTests
{
    private TestCluster _cluster;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IResearchStudyIndexGrain Index()
        => _cluster.GrainFactory.GetGrain<IResearchStudyIndexGrain>("IRB-STUDY-IDX");

    private IrbStudyIndexEntry MakeStudy(string studyId, IrbStudyStatus status = IrbStudyStatus.OpenForEnrollment,
        IrbStudyType type = IrbStudyType.Interventional, string pi = "Smith, John MD",
        DateTime? expiration = null)
        => new()
        {
            StudyId = studyId,
            IrbProtocolNumber = $"IRB-{Guid.NewGuid():N}"[..12],
            Title = $"Study {studyId}",
            PrincipalInvestigator = pi,
            StudyType = type,
            Phase = IrbStudyPhase.Phase2,
            Status = status,
            CurrentEnrollment = 5,
            TargetEnrollment = 50,
            CurrentExpirationDate = expiration ?? DateTime.UtcNow.AddYears(1)
        };

    [Test]
    public async Task ResearchStudyIndexGrain_CanUpsertAndGetAll()
    {
        IResearchStudyIndexGrain index = Index();
        string id1 = $"IRB-STUDY:{Guid.NewGuid()}";
        string id2 = $"IRB-STUDY:{Guid.NewGuid()}";

        await index.UpsertStudyAsync(MakeStudy(id1));
        await index.UpsertStudyAsync(MakeStudy(id2));

        List<IrbStudyIndexEntry> all = await index.GetAllStudiesAsync();
        Assert.That(all.Any(s => s.StudyId == id1), Is.True);
        Assert.That(all.Any(s => s.StudyId == id2), Is.True);
    }

    [Test]
    public async Task ResearchStudyIndexGrain_CanGetOpenStudies()
    {
        IResearchStudyIndexGrain index = Index();
        string openId = $"IRB-STUDY:{Guid.NewGuid()}";
        string draftId = $"IRB-STUDY:{Guid.NewGuid()}";

        await index.UpsertStudyAsync(MakeStudy(openId, IrbStudyStatus.OpenForEnrollment));
        await index.UpsertStudyAsync(MakeStudy(draftId, IrbStudyStatus.Draft));

        List<IrbStudyIndexEntry> open = await index.GetOpenStudiesAsync();
        Assert.That(open.Any(s => s.StudyId == openId), Is.True);
        Assert.That(open.Any(s => s.StudyId == draftId), Is.False);
    }

    [Test]
    public async Task ResearchStudyIndexGrain_CanGetStudiesByType()
    {
        IResearchStudyIndexGrain index = Index();
        string interventionalId = $"IRB-STUDY:{Guid.NewGuid()}";
        string observationalId = $"IRB-STUDY:{Guid.NewGuid()}";

        await index.UpsertStudyAsync(MakeStudy(interventionalId, type: IrbStudyType.Interventional));
        await index.UpsertStudyAsync(MakeStudy(observationalId, type: IrbStudyType.Observational));

        List<IrbStudyIndexEntry> obs = await index.GetStudiesByTypeAsync(IrbStudyType.Observational);
        Assert.That(obs.Any(s => s.StudyId == observationalId), Is.True);
        Assert.That(obs.Any(s => s.StudyId == interventionalId), Is.False);
    }

    [Test]
    public async Task ResearchStudyIndexGrain_CanGetExpiringStudies()
    {
        IResearchStudyIndexGrain index = Index();
        string expiringSoonId = $"IRB-STUDY:{Guid.NewGuid()}";
        string farId = $"IRB-STUDY:{Guid.NewGuid()}";

        await index.UpsertStudyAsync(MakeStudy(expiringSoonId, expiration: DateTime.UtcNow.AddDays(30)));
        await index.UpsertStudyAsync(MakeStudy(farId, expiration: DateTime.UtcNow.AddYears(2)));

        List<IrbStudyIndexEntry> expiring = await index.GetStudiesExpiringAsync(60);
        Assert.That(expiring.Any(s => s.StudyId == expiringSoonId), Is.True);
        Assert.That(expiring.Any(s => s.StudyId == farId), Is.False);
    }

    [Test]
    public async Task ResearchStudyIndexGrain_UpsertUpdatesExistingEntry()
    {
        IResearchStudyIndexGrain index = Index();
        string id = $"IRB-STUDY:{Guid.NewGuid()}";

        await index.UpsertStudyAsync(MakeStudy(id, IrbStudyStatus.Draft));
        IrbStudyIndexEntry updated = MakeStudy(id, IrbStudyStatus.OpenForEnrollment);
        await index.UpsertStudyAsync(updated);

        List<IrbStudyIndexEntry> all = await index.GetAllStudiesAsync();
        List<IrbStudyIndexEntry> byId = all.Where(s => s.StudyId == id).ToList();
        Assert.That(byId, Has.Count.EqualTo(1));
        Assert.That(byId[0].Status, Is.EqualTo(IrbStudyStatus.OpenForEnrollment));
    }
}

// ── ResearchSubjectGrain Tests ────────────────────────────────────────────────

[TestFixture]
public class ResearchSubjectGrainTests
{
    private TestCluster _cluster;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task ResearchSubjectGrain_CanEnrollSubject()
    {
        string subjectId = $"IRB-SUBJECT:{Guid.NewGuid()}";
        IResearchSubjectGrain grain = _cluster.GrainFactory.GetGrain<IResearchSubjectGrain>(subjectId);

        await grain.EnrollSubjectAsync(
            "IRB-STUDY:001", "Treatment X Study",
            "PAT-001", "Smith, John", new DateTime(1960, 5, 15),
            DateTime.UtcNow.AddDays(-7), DateTime.UtcNow.AddDays(-5),
            DateTime.UtcNow.AddDays(-5), ConsentType.Written, "Jones, MD", "Arm A");

        ResearchSubjectState state = await grain.GetSubjectAsync();
        Assert.That(state.SubjectId, Is.EqualTo(subjectId));
        Assert.That(state.PatientId, Is.EqualTo("PAT-001"));
        Assert.That(state.EnrollmentStatus, Is.EqualTo(SubjectEnrollmentStatus.Enrolled));
        Assert.That(state.Arm, Is.EqualTo("Arm A"));
        Assert.That(state.ConsentType, Is.EqualTo(ConsentType.Written));
    }

    [Test]
    public async Task ResearchSubjectGrain_CanActivateSubject()
    {
        string subjectId = $"IRB-SUBJECT:{Guid.NewGuid()}";
        IResearchSubjectGrain grain = _cluster.GrainFactory.GetGrain<IResearchSubjectGrain>(subjectId);

        await grain.EnrollSubjectAsync("IRB-STUDY:002", "Obs Study", "PAT-002", "Doe, Jane",
            null, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, ConsentType.Written, "Smith, RN", "Cohort B");
        await grain.ActivateSubjectAsync();

        ResearchSubjectState state = await grain.GetSubjectAsync();
        Assert.That(state.EnrollmentStatus, Is.EqualTo(SubjectEnrollmentStatus.Active));
    }

    [Test]
    public async Task ResearchSubjectGrain_CanWithdrawSubject()
    {
        string subjectId = $"IRB-SUBJECT:{Guid.NewGuid()}";
        IResearchSubjectGrain grain = _cluster.GrainFactory.GetGrain<IResearchSubjectGrain>(subjectId);

        await grain.EnrollSubjectAsync("IRB-STUDY:003", "Drug Z Trial", "PAT-003", "Brown, Bob",
            null, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, ConsentType.Written, "Jones, MD", "Placebo");
        await grain.WithdrawSubjectAsync("Adverse event — patient withdrew consent.", DateTime.UtcNow);

        ResearchSubjectState state = await grain.GetSubjectAsync();
        Assert.That(state.EnrollmentStatus, Is.EqualTo(SubjectEnrollmentStatus.Withdrawn));
        Assert.That(state.WithdrawalReason, Is.EqualTo("Adverse event — patient withdrew consent."));
        Assert.That(state.WithdrawalDate, Is.Not.Null);
    }

    [Test]
    public async Task ResearchSubjectGrain_CanCompleteSubject()
    {
        string subjectId = $"IRB-SUBJECT:{Guid.NewGuid()}";
        IResearchSubjectGrain grain = _cluster.GrainFactory.GetGrain<IResearchSubjectGrain>(subjectId);

        await grain.EnrollSubjectAsync("IRB-STUDY:004", "Registry A", "PAT-004", "Clark, Ann",
            null, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, ConsentType.Waived, "Davis, MD", "");
        await grain.CompleteSubjectAsync(DateTime.UtcNow);

        ResearchSubjectState state = await grain.GetSubjectAsync();
        Assert.That(state.EnrollmentStatus, Is.EqualTo(SubjectEnrollmentStatus.Completed));
        Assert.That(state.CompletionDate, Is.Not.Null);
    }

    [Test]
    public async Task ResearchSubjectGrain_CanMarkLostToFollowUp()
    {
        string subjectId = $"IRB-SUBJECT:{Guid.NewGuid()}";
        IResearchSubjectGrain grain = _cluster.GrainFactory.GetGrain<IResearchSubjectGrain>(subjectId);

        await grain.EnrollSubjectAsync("IRB-STUDY:005", "Study E", "PAT-005", "Evans, Tom",
            null, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, ConsentType.Written, "Ford, RN", "Active");
        await grain.MarkLostToFollowUpAsync();

        ResearchSubjectState state = await grain.GetSubjectAsync();
        Assert.That(state.EnrollmentStatus, Is.EqualTo(SubjectEnrollmentStatus.LostToFollowUp));
    }
}

// ── ResearchSubjectIndexGrain Tests ───────────────────────────────────────────

[TestFixture]
public class ResearchSubjectIndexGrainTests
{
    private TestCluster _cluster;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IResearchSubjectIndexGrain Index(string studyId)
        => _cluster.GrainFactory.GetGrain<IResearchSubjectIndexGrain>($"IRB-SUBJECT-IDX:{studyId}");

    private ResearchSubjectIndexEntry MakeSubject(string subjectId, SubjectEnrollmentStatus status, string arm = "Arm A")
        => new()
        {
            SubjectId = subjectId,
            StudyId = "IRB-STUDY:TEST",
            PatientId = $"PAT-{subjectId[..4]}",
            PatientName = $"Patient {subjectId[..4]}",
            EnrollmentStatus = status,
            EnrollmentDate = DateTime.UtcNow.AddDays(-30),
            ConsentDate = DateTime.UtcNow.AddDays(-30),
            Arm = arm
        };

    [Test]
    public async Task ResearchSubjectIndexGrain_CanUpsertAndGetAll()
    {
        string studyId = $"STUDY-{Guid.NewGuid()}";
        IResearchSubjectIndexGrain index = Index(studyId);

        string s1 = Guid.NewGuid().ToString()[..8];
        string s2 = Guid.NewGuid().ToString()[..8];
        await index.UpsertSubjectAsync(MakeSubject(s1, SubjectEnrollmentStatus.Active));
        await index.UpsertSubjectAsync(MakeSubject(s2, SubjectEnrollmentStatus.Enrolled));

        List<ResearchSubjectIndexEntry> all = await index.GetAllSubjectsAsync();
        Assert.That(all, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task ResearchSubjectIndexGrain_CanGetActiveSubjects()
    {
        string studyId = $"STUDY-{Guid.NewGuid()}";
        IResearchSubjectIndexGrain index = Index(studyId);

        string activeId = Guid.NewGuid().ToString()[..8];
        string withdrawnId = Guid.NewGuid().ToString()[..8];
        await index.UpsertSubjectAsync(MakeSubject(activeId, SubjectEnrollmentStatus.Active));
        await index.UpsertSubjectAsync(MakeSubject(withdrawnId, SubjectEnrollmentStatus.Withdrawn));

        List<ResearchSubjectIndexEntry> active = await index.GetActiveSubjectsAsync();
        Assert.That(active.Any(s => s.SubjectId == activeId), Is.True);
        Assert.That(active.Any(s => s.SubjectId == withdrawnId), Is.False);
    }

    [Test]
    public async Task ResearchSubjectIndexGrain_ActiveIncludesEnrolledStatus()
    {
        string studyId = $"STUDY-{Guid.NewGuid()}";
        IResearchSubjectIndexGrain index = Index(studyId);

        string enrolledId = Guid.NewGuid().ToString()[..8];
        await index.UpsertSubjectAsync(MakeSubject(enrolledId, SubjectEnrollmentStatus.Enrolled));

        List<ResearchSubjectIndexEntry> active = await index.GetActiveSubjectsAsync();
        Assert.That(active.Any(s => s.SubjectId == enrolledId), Is.True);
    }

    [Test]
    public async Task ResearchSubjectIndexGrain_CanGetWithdrawnSubjects()
    {
        string studyId = $"STUDY-{Guid.NewGuid()}";
        IResearchSubjectIndexGrain index = Index(studyId);

        string withdrawnId = Guid.NewGuid().ToString()[..8];
        string completedId = Guid.NewGuid().ToString()[..8];
        await index.UpsertSubjectAsync(MakeSubject(withdrawnId, SubjectEnrollmentStatus.Withdrawn));
        await index.UpsertSubjectAsync(MakeSubject(completedId, SubjectEnrollmentStatus.Completed));

        List<ResearchSubjectIndexEntry> withdrawn = await index.GetWithdrawnSubjectsAsync();
        Assert.That(withdrawn.Any(s => s.SubjectId == withdrawnId), Is.True);
        Assert.That(withdrawn.Any(s => s.SubjectId == completedId), Is.False);
    }

    [Test]
    public async Task ResearchSubjectIndexGrain_UpsertUpdatesExistingEntry()
    {
        string studyId = $"STUDY-{Guid.NewGuid()}";
        IResearchSubjectIndexGrain index = Index(studyId);

        string subjectId = Guid.NewGuid().ToString()[..8];
        await index.UpsertSubjectAsync(MakeSubject(subjectId, SubjectEnrollmentStatus.Enrolled));
        ResearchSubjectIndexEntry updated = MakeSubject(subjectId, SubjectEnrollmentStatus.Completed);
        await index.UpsertSubjectAsync(updated);

        List<ResearchSubjectIndexEntry> all = await index.GetAllSubjectsAsync();
        List<ResearchSubjectIndexEntry> byId = all.Where(s => s.SubjectId == subjectId).ToList();
        Assert.That(byId, Has.Count.EqualTo(1));
        Assert.That(byId[0].EnrollmentStatus, Is.EqualTo(SubjectEnrollmentStatus.Completed));
    }
}

// ── Research IRB Integration Tests ────────────────────────────────────────────

[TestFixture]
public class ResearchIRBIntegrationTests
{
    private TestCluster _cluster;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task ResearchIRB_CanRegisterStudyAndOpenForEnrollment()
    {
        string studyId = $"IRB-STUDY:{Guid.NewGuid()}";
        IResearchStudyGrain study = _cluster.GrainFactory.GetGrain<IResearchStudyGrain>(studyId);
        IResearchStudyIndexGrain index = _cluster.GrainFactory.GetGrain<IResearchStudyIndexGrain>("IRB-INT-IDX-1");

        await study.CreateStudyAsync("IRB-INT-001", "Integration Study A", "IntStudA",
            "Grant, Bob MD", "EMP-INT-001", "VA Research",
            IrbStudyType.Interventional, IrbStudyPhase.Phase2,
            "Cardiology", 60, "Integration test study.");
        ResearchStudyState state = await study.GetStudyAsync();
        await index.UpsertStudyAsync(new IrbStudyIndexEntry
        {
            StudyId = state.StudyId, IrbProtocolNumber = state.IrbProtocolNumber,
            Title = state.Title, PrincipalInvestigator = state.PrincipalInvestigator,
            StudyType = state.StudyType, Phase = state.Phase, Status = state.Status,
            CurrentEnrollment = state.CurrentEnrollment, TargetEnrollment = state.TargetEnrollment,
            CurrentExpirationDate = state.CurrentExpirationDate
        });

        await study.OpenForEnrollmentAsync(DateTime.UtcNow, DateTime.UtcNow.AddYears(1), null);
        state = await study.GetStudyAsync();
        await index.UpsertStudyAsync(new IrbStudyIndexEntry
        {
            StudyId = state.StudyId, IrbProtocolNumber = state.IrbProtocolNumber,
            Title = state.Title, PrincipalInvestigator = state.PrincipalInvestigator,
            StudyType = state.StudyType, Phase = state.Phase, Status = state.Status,
            CurrentEnrollment = state.CurrentEnrollment, TargetEnrollment = state.TargetEnrollment,
            CurrentExpirationDate = state.CurrentExpirationDate
        });

        List<IrbStudyIndexEntry> open = await index.GetOpenStudiesAsync();
        Assert.That(open.Any(s => s.StudyId == studyId), Is.True);
    }

    [Test]
    public async Task ResearchIRB_EnrollmentCountTrackedAcrossStudyAndSubjectIndex()
    {
        string studyId = $"IRB-STUDY:{Guid.NewGuid()}";
        IResearchStudyGrain study = _cluster.GrainFactory.GetGrain<IResearchStudyGrain>(studyId);
        IResearchSubjectIndexGrain subjectIndex = _cluster.GrainFactory.GetGrain<IResearchSubjectIndexGrain>($"IRB-SUBJECT-IDX:{studyId}");

        await study.CreateStudyAsync("IRB-INT-002", "Enrollment Tracking Study", "EnrollTrack",
            "Hall, Kay MD", "EMP-INT-002", "NIH",
            IrbStudyType.Observational, IrbStudyPhase.NotApplicable,
            "Oncology", 20, "Tracks enrollment across grains.");
        await study.OpenForEnrollmentAsync(DateTime.UtcNow, DateTime.UtcNow.AddYears(1), null);

        // Enroll 3 subjects
        for (int i = 1; i <= 3; i++)
        {
            string subjectId = $"IRB-SUBJECT:{Guid.NewGuid()}";
            IResearchSubjectGrain sub = _cluster.GrainFactory.GetGrain<IResearchSubjectGrain>(subjectId);
            await sub.EnrollSubjectAsync(studyId, "Enrollment Tracking Study", $"PAT-{i:D3}", $"Patient {i}",
                null, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, ConsentType.Written, "Smith, RN", "Cohort A");
            ResearchSubjectState subState = await sub.GetSubjectAsync();
            await subjectIndex.UpsertSubjectAsync(new ResearchSubjectIndexEntry
            {
                SubjectId = subState.SubjectId, StudyId = subState.StudyId,
                PatientId = subState.PatientId, PatientName = subState.PatientName,
                EnrollmentStatus = subState.EnrollmentStatus,
                EnrollmentDate = subState.EnrollmentDate, ConsentDate = subState.ConsentDate,
                Arm = subState.Arm
            });
            await study.IncrementEnrollmentAsync();
        }

        ResearchStudyState studyState = await study.GetStudyAsync();
        List<ResearchSubjectIndexEntry> subjects = await subjectIndex.GetAllSubjectsAsync();

        Assert.That(studyState.CurrentEnrollment, Is.EqualTo(3));
        Assert.That(subjects, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task ResearchIRB_WithdrawalDecrementsEnrollment()
    {
        string studyId = $"IRB-STUDY:{Guid.NewGuid()}";
        IResearchStudyGrain study = _cluster.GrainFactory.GetGrain<IResearchStudyGrain>(studyId);
        IResearchSubjectIndexGrain subjectIndex = _cluster.GrainFactory.GetGrain<IResearchSubjectIndexGrain>($"IRB-SUBJECT-IDX:{studyId}");

        await study.CreateStudyAsync("IRB-INT-003", "Withdrawal Test Study", "WdTest",
            "Irving, Dan MD", "EMP-INT-003", "VA",
            IrbStudyType.Interventional, IrbStudyPhase.Phase1,
            "Neurology", 10, "Tests withdrawal flow.");
        await study.OpenForEnrollmentAsync(DateTime.UtcNow, DateTime.UtcNow.AddYears(1), null);

        string subjectId = $"IRB-SUBJECT:{Guid.NewGuid()}";
        IResearchSubjectGrain sub = _cluster.GrainFactory.GetGrain<IResearchSubjectGrain>(subjectId);
        await sub.EnrollSubjectAsync(studyId, "Withdrawal Test Study", "PAT-WD", "Patient WD",
            null, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow, ConsentType.Written, "Jones, MD", "Treatment");
        await study.IncrementEnrollmentAsync();

        ResearchStudyState beforeState = await study.GetStudyAsync();
        Assert.That(beforeState.CurrentEnrollment, Is.EqualTo(1));

        await sub.WithdrawSubjectAsync("Withdrew consent.", DateTime.UtcNow);
        await study.DecrementEnrollmentAsync();
        ResearchSubjectState subState = await sub.GetSubjectAsync();
        await subjectIndex.UpsertSubjectAsync(new ResearchSubjectIndexEntry
        {
            SubjectId = subState.SubjectId, StudyId = subState.StudyId,
            PatientId = subState.PatientId, PatientName = subState.PatientName,
            EnrollmentStatus = subState.EnrollmentStatus,
            EnrollmentDate = subState.EnrollmentDate, ConsentDate = subState.ConsentDate,
            Arm = subState.Arm
        });

        ResearchStudyState afterState = await study.GetStudyAsync();
        List<ResearchSubjectIndexEntry> withdrawn = await subjectIndex.GetWithdrawnSubjectsAsync();

        Assert.That(afterState.CurrentEnrollment, Is.EqualTo(0));
        Assert.That(withdrawn.Any(s => s.SubjectId == subjectId), Is.True);
    }

    [Test]
    public async Task ResearchIRB_ContinuingReviewUpdatesExpiration()
    {
        string studyId = $"IRB-STUDY:{Guid.NewGuid()}";
        IResearchStudyGrain study = _cluster.GrainFactory.GetGrain<IResearchStudyGrain>(studyId);

        await study.CreateStudyAsync("IRB-INT-004", "Long-Running Study", "LongRun",
            "Jones, Kay MD", "EMP-INT-004", "NIH",
            IrbStudyType.Observational, IrbStudyPhase.NotApplicable,
            "Endocrinology", 100, "Multi-year observational study.");
        await study.OpenForEnrollmentAsync(DateTime.UtcNow.AddYears(-1),
            DateTime.UtcNow.AddDays(30), // expiring soon
            DateTime.UtcNow.AddDays(15));

        string submissionId = Guid.NewGuid().ToString();
        await study.RecordSubmissionAsync(submissionId, IrbSubmissionType.ContinuingReview,
            DateTime.UtcNow.AddDays(-5), "Annual continuing review.");
        DateTime newExpiration = DateTime.UtcNow.AddYears(1);
        await study.UpdateSubmissionDecisionAsync(submissionId, IrbSubmissionStatus.Approved,
            "Approved for another year", DateTime.UtcNow, newExpiration);

        ResearchStudyState state = await study.GetStudyAsync();
        Assert.That(state.CurrentExpirationDate!.Value.Date, Is.EqualTo(newExpiration.Date));
        Assert.That(state.Submissions[0].Status, Is.EqualTo(IrbSubmissionStatus.Approved));
    }

    [Test]
    public async Task ResearchIRB_MultipleArmsAndSubmissionsTracked()
    {
        string studyId = $"IRB-STUDY:{Guid.NewGuid()}";
        IResearchStudyGrain study = _cluster.GrainFactory.GetGrain<IResearchStudyGrain>(studyId);

        await study.CreateStudyAsync("IRB-INT-005", "Three-Arm Phase 3 Trial", "3ArmP3",
            "Lee, Pat MD", "EMP-INT-005", "Pharma Corp",
            IrbStudyType.Interventional, IrbStudyPhase.Phase3,
            "Hematology", 300, "Randomized 3-arm trial.");

        // Add arms
        await study.AddArmAsync("Dose Low");
        await study.AddArmAsync("Dose High");
        await study.AddArmAsync("Placebo");

        // Record initial + amendment
        string initId = Guid.NewGuid().ToString();
        string amendId = Guid.NewGuid().ToString();
        await study.RecordSubmissionAsync(initId, IrbSubmissionType.InitialApplication, DateTime.UtcNow.AddDays(-60), "Initial application.");
        await study.UpdateSubmissionDecisionAsync(initId, IrbSubmissionStatus.Approved, "Approved", DateTime.UtcNow.AddDays(-45), DateTime.UtcNow.AddYears(1));
        await study.RecordSubmissionAsync(amendId, IrbSubmissionType.Amendment, DateTime.UtcNow.AddDays(-10), "Protocol amendment to add biomarker assessment.");

        ResearchStudyState state = await study.GetStudyAsync();
        Assert.That(state.StudyArms, Has.Count.EqualTo(3));
        Assert.That(state.Submissions, Has.Count.EqualTo(2));
        Assert.That(state.Submissions.Any(s => s.SubmissionType == IrbSubmissionType.Amendment), Is.True);
    }
}
