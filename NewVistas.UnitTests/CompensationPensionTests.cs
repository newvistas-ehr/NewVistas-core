// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

// ═══════════════════════════════════════════════════════════════════════════
// CPExamGrainTests
// ═══════════════════════════════════════════════════════════════════════════

[TestFixture]
public class CPExamGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private ICPExamGrain NewExam() =>
        _cluster.GrainFactory.GetGrain<ICPExamGrain>($"CP-EXAM:{Guid.NewGuid()}");

    private static async Task ScheduleBasicExam(ICPExamGrain grain, string patientId = "PAT-001")
    {
        await grain.ScheduleExamAsync(
            patientId,
            "John Doe",
            CPExamType.Initial,
            DateTime.UtcNow.AddDays(14),
            "Dr. Smith",
            "MD",
            CPExaminerType.VAPhysician,
            "Clinic A",
            "VAMC Chicago",
            "CLM-2025-001",
            "Compensation",
            new List<string> { "M17.11", "G89.29" },
            "scheduler@va.gov");
    }

    [Test]
    public async Task CPExamGrain_CanScheduleExam()
    {
        ICPExamGrain grain = NewExam();
        await ScheduleBasicExam(grain);

        CPExamState state = await grain.GetExamAsync();
        Assert.That(state.PatientId, Is.EqualTo("PAT-001"));
        Assert.That(state.PatientName, Is.EqualTo("John Doe"));
        Assert.That(state.ExamType, Is.EqualTo(CPExamType.Initial));
        Assert.That(state.Status, Is.EqualTo(CPExamStatus.Scheduled));
        Assert.That(state.ExaminerName, Is.EqualTo("Dr. Smith"));
        Assert.That(state.ClaimNumber, Is.EqualTo("CLM-2025-001"));
        Assert.That(state.DisabilityClaimedCodes, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task CPExamGrain_CanCompleteExam()
    {
        ICPExamGrain grain = NewExam();
        await ScheduleBasicExam(grain);

        await grain.CompleteExamAsync(
            new List<string> { "Osteoarthritis right knee", "Chronic pain syndrome" },
            nexus: true,
            "As likely as not related to in-service injury documented in STRs.");

        CPExamState state = await grain.GetExamAsync();
        Assert.That(state.Status, Is.EqualTo(CPExamStatus.Completed));
        Assert.That(state.CompletedDate, Is.Not.Null);
        Assert.That(state.Nexus, Is.True);
        Assert.That(state.Diagnoses, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task CPExamGrain_CanCancelExam()
    {
        ICPExamGrain grain = NewExam();
        await ScheduleBasicExam(grain);
        await grain.CancelExamAsync("Veteran unable to attend; no-show.");

        CPExamState state = await grain.GetExamAsync();
        Assert.That(state.Status, Is.EqualTo(CPExamStatus.Cancelled));
        Assert.That(state.CancelledDate, Is.Not.Null);
        Assert.That(state.CancellationReason, Does.Contain("no-show"));
    }

    [Test]
    public async Task CPExamGrain_CanRescheduleExam()
    {
        ICPExamGrain grain = NewExam();
        await ScheduleBasicExam(grain);
        DateTime newDate = DateTime.UtcNow.AddDays(21);
        await grain.RescheduleExamAsync(newDate, "Examiner conflict.");

        CPExamState state = await grain.GetExamAsync();
        Assert.That(state.Status, Is.EqualTo(CPExamStatus.Rescheduled));
        Assert.That(state.ScheduledDate.Date, Is.EqualTo(newDate.Date));
    }

    [Test]
    public async Task CPExamGrain_CanAddDbqToExam()
    {
        ICPExamGrain grain = NewExam();
        await ScheduleBasicExam(grain);
        string dbqId = $"CP-DBQ:{Guid.NewGuid()}";
        await grain.AddDbqToExamAsync(dbqId);
        await grain.AddDbqToExamAsync(dbqId); // duplicate — no double-add

        CPExamState state = await grain.GetExamAsync();
        Assert.That(state.DbqIds, Has.Count.EqualTo(1));
        Assert.That(state.DbqIds, Contains.Item(dbqId));
    }

    [Test]
    public async Task CPExamGrain_ExamIdMatchesGrainKey()
    {
        ICPExamGrain grain = NewExam();
        await ScheduleBasicExam(grain);

        CPExamState state = await grain.GetExamAsync();
        Assert.That(state.ExamId, Is.EqualTo(grain.GetPrimaryKeyString()));
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// CPExamIndexGrainTests
// ═══════════════════════════════════════════════════════════════════════════

[TestFixture]
public class CPExamIndexGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private ICPExamIndexGrain NewIndex() =>
        _cluster.GrainFactory.GetGrain<ICPExamIndexGrain>($"CP-EXAM-IDX:{Guid.NewGuid()}");

    private static CPExamIndexEntry MakeEntry(CPExamStatus status, DateTime scheduledDate) => new()
    {
        ExamId = $"CP-EXAM:{Guid.NewGuid()}",
        ExamType = CPExamType.Initial,
        Status = status,
        ScheduledDate = scheduledDate,
        ExaminerName = "Dr. Jones",
        ClaimNumber = "CLM-001",
        DisabilityCount = 2,
        DbqCount = 0
    };

    [Test]
    public async Task CPExamIndexGrain_CanUpsertAndRetrieve()
    {
        ICPExamIndexGrain index = NewIndex();
        CPExamIndexEntry entry = MakeEntry(CPExamStatus.Scheduled, DateTime.UtcNow.AddDays(7));
        await index.UpsertExamAsync(entry);

        List<CPExamIndexEntry> all = await index.GetAllExamsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].ExamId, Is.EqualTo(entry.ExamId));
    }

    [Test]
    public async Task CPExamIndexGrain_UpsertUpdatesExistingEntry()
    {
        ICPExamIndexGrain index = NewIndex();
        CPExamIndexEntry entry = MakeEntry(CPExamStatus.Scheduled, DateTime.UtcNow.AddDays(7));
        await index.UpsertExamAsync(entry);

        CPExamIndexEntry updated = MakeEntry(CPExamStatus.Completed, DateTime.UtcNow.AddDays(7));
        updated.ExamId = entry.ExamId;
        updated.CompletedDate = DateTime.UtcNow;
        await index.UpsertExamAsync(updated);

        List<CPExamIndexEntry> all = await index.GetAllExamsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Status, Is.EqualTo(CPExamStatus.Completed));
    }

    [Test]
    public async Task CPExamIndexGrain_CanGetScheduledExams()
    {
        ICPExamIndexGrain index = NewIndex();
        await index.UpsertExamAsync(MakeEntry(CPExamStatus.Scheduled, DateTime.UtcNow.AddDays(5)));
        await index.UpsertExamAsync(MakeEntry(CPExamStatus.Rescheduled, DateTime.UtcNow.AddDays(10)));
        await index.UpsertExamAsync(MakeEntry(CPExamStatus.Completed, DateTime.UtcNow.AddDays(-3)));

        List<CPExamIndexEntry> scheduled = await index.GetScheduledExamsAsync();
        Assert.That(scheduled, Has.Count.EqualTo(2));
        Assert.That(scheduled.All(e => e.Status is CPExamStatus.Scheduled or CPExamStatus.Rescheduled), Is.True);
    }

    [Test]
    public async Task CPExamIndexGrain_CanGetCompletedExams()
    {
        ICPExamIndexGrain index = NewIndex();
        await index.UpsertExamAsync(MakeEntry(CPExamStatus.Scheduled, DateTime.UtcNow.AddDays(5)));
        CPExamIndexEntry completedEntry = MakeEntry(CPExamStatus.Completed, DateTime.UtcNow.AddDays(-3));
        completedEntry.CompletedDate = DateTime.UtcNow.AddDays(-3);
        await index.UpsertExamAsync(completedEntry);

        List<CPExamIndexEntry> completed = await index.GetCompletedExamsAsync();
        Assert.That(completed, Has.Count.EqualTo(1));
        Assert.That(completed[0].Status, Is.EqualTo(CPExamStatus.Completed));
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// DBQGrainTests
// ═══════════════════════════════════════════════════════════════════════════

[TestFixture]
public class DBQGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IDBQGrain NewDbq() =>
        _cluster.GrainFactory.GetGrain<IDBQGrain>($"CP-DBQ:{Guid.NewGuid()}");

    private static async Task CreateBasicDbq(IDBQGrain grain)
    {
        await grain.CreateDBQAsync(
            examId: $"CP-EXAM:{Guid.NewGuid()}",
            patientId: "PAT-001",
            patientName: "John Doe",
            dbqType: DBQType.Musculoskeletal,
            dbqFormNumber: "21-0960M-9",
            dbqTitle: "Knee and Lower Leg Conditions",
            claimNumber: "CLM-2025-001",
            conditionClaimed: "Right knee osteoarthritis",
            diagnosisCode: "M17.11",
            diagnosisDescription: "Primary osteoarthritis, right knee");
    }

    [Test]
    public async Task DBQGrain_CanCreateDBQ()
    {
        IDBQGrain grain = NewDbq();
        await CreateBasicDbq(grain);

        DBQState state = await grain.GetDBQAsync();
        Assert.That(state.PatientId, Is.EqualTo("PAT-001"));
        Assert.That(state.DbqType, Is.EqualTo(DBQType.Musculoskeletal));
        Assert.That(state.Status, Is.EqualTo(DBQStatus.Draft));
        Assert.That(state.ConditionClaimed, Is.EqualTo("Right knee osteoarthritis"));
        Assert.That(state.DiagnosisCode, Is.EqualTo("M17.11"));
    }

    [Test]
    public async Task DBQGrain_CanUpdateSections()
    {
        IDBQGrain grain = NewDbq();
        await CreateBasicDbq(grain);

        await grain.UpdateSectionsAsync(
            historySection: "Veteran reports right knee pain since 2018 combat deployment.",
            symptomsSection: "Pain, swelling, limited ROM. Flares with activity.",
            functionalImpactSection: "Unable to walk more than 1 block. Cannot climb stairs.",
            rangeOfMotionSection: "Flexion 90°, Extension 5° (normal 0°).",
            mentalStatusSection: string.Empty,
            diagnosticTestsSection: "X-ray shows joint space narrowing, osteophytes.");

        DBQState state = await grain.GetDBQAsync();
        Assert.That(state.HistorySection, Does.Contain("2018 combat"));
        Assert.That(state.RangeOfMotionSection, Does.Contain("90°"));
    }

    [Test]
    public async Task DBQGrain_CanRecordOpinion()
    {
        IDBQGrain grain = NewDbq();
        await CreateBasicDbq(grain);

        await grain.RecordOpinionAsync(
            nexusOpinion: true,
            nexusStatement: "As likely as not (50% or greater probability) the veteran's right knee osteoarthritis is related to in-service injury.",
            opinionsSection: "Based on review of STRs, the injury occurred during active duty.",
            serviceConnectionType: ServiceConnectionType.DirectService,
            residualsPermanent: true,
            expectedImprovement: false);

        DBQState state = await grain.GetDBQAsync();
        Assert.That(state.NexusOpinion, Is.True);
        Assert.That(state.ServiceConnectionType, Is.EqualTo(ServiceConnectionType.DirectService));
        Assert.That(state.ResidualsPermanent, Is.True);
        Assert.That(state.ExpectedImprovement, Is.False);
    }

    [Test]
    public async Task DBQGrain_CanSetProposedRating()
    {
        IDBQGrain grain = NewDbq();
        await CreateBasicDbq(grain);
        await grain.SetProposedRatingAsync(30);

        DBQState state = await grain.GetDBQAsync();
        Assert.That(state.ProposedRating, Is.EqualTo(30));
    }

    [Test]
    public async Task DBQGrain_CanCompleteDBQ()
    {
        IDBQGrain grain = NewDbq();
        await CreateBasicDbq(grain);
        await grain.CompleteDBQAsync();

        DBQState state = await grain.GetDBQAsync();
        Assert.That(state.Status, Is.EqualTo(DBQStatus.Completed));
        Assert.That(state.CompletedDate, Is.Not.Null);
    }

    [Test]
    public async Task DBQGrain_CanSignDBQ()
    {
        IDBQGrain grain = NewDbq();
        await CreateBasicDbq(grain);
        await grain.CompleteDBQAsync();
        await grain.SignDBQAsync("Dr. Smith, MD", DateTime.UtcNow);

        DBQState state = await grain.GetDBQAsync();
        Assert.That(state.Status, Is.EqualTo(DBQStatus.Signed));
        Assert.That(state.SignedBy, Is.EqualTo("Dr. Smith, MD"));
        Assert.That(state.SignedDate, Is.Not.Null);
    }

    [Test]
    public async Task DBQGrain_DbqIdMatchesGrainKey()
    {
        IDBQGrain grain = NewDbq();
        await CreateBasicDbq(grain);

        DBQState state = await grain.GetDBQAsync();
        Assert.That(state.DbqId, Is.EqualTo(grain.GetPrimaryKeyString()));
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// DBQIndexGrainTests
// ═══════════════════════════════════════════════════════════════════════════

[TestFixture]
public class DBQIndexGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IDBQIndexGrain NewIndex() =>
        _cluster.GrainFactory.GetGrain<IDBQIndexGrain>($"CP-DBQ-IDX:{Guid.NewGuid()}");

    private static DBQIndexEntry MakeEntry(string examId, DBQStatus status) => new()
    {
        DbqId = $"CP-DBQ:{Guid.NewGuid()}",
        ExamId = examId,
        DbqType = DBQType.Musculoskeletal,
        DbqTitle = "Knee Conditions",
        ConditionClaimed = "Right knee OA",
        Status = status,
        ProposedRating = 30,
        ServiceConnectionType = ServiceConnectionType.DirectService,
        CompletedDate = status == DBQStatus.Draft ? null : DateTime.UtcNow
    };

    [Test]
    public async Task DBQIndexGrain_CanUpsertAndRetrieve()
    {
        IDBQIndexGrain index = NewIndex();
        string examId = $"CP-EXAM:{Guid.NewGuid()}";
        DBQIndexEntry entry = MakeEntry(examId, DBQStatus.Draft);
        await index.UpsertDBQAsync(entry);

        List<DBQIndexEntry> all = await index.GetAllDBQsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].DbqId, Is.EqualTo(entry.DbqId));
    }

    [Test]
    public async Task DBQIndexGrain_UpsertUpdatesExistingEntry()
    {
        IDBQIndexGrain index = NewIndex();
        string examId = $"CP-EXAM:{Guid.NewGuid()}";
        DBQIndexEntry entry = MakeEntry(examId, DBQStatus.Draft);
        await index.UpsertDBQAsync(entry);

        DBQIndexEntry updated = MakeEntry(examId, DBQStatus.Signed);
        updated.DbqId = entry.DbqId;
        await index.UpsertDBQAsync(updated);

        List<DBQIndexEntry> all = await index.GetAllDBQsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].Status, Is.EqualTo(DBQStatus.Signed));
    }

    [Test]
    public async Task DBQIndexGrain_CanGetDBQsForExam()
    {
        IDBQIndexGrain index = NewIndex();
        string examId1 = $"CP-EXAM:{Guid.NewGuid()}";
        string examId2 = $"CP-EXAM:{Guid.NewGuid()}";
        await index.UpsertDBQAsync(MakeEntry(examId1, DBQStatus.Draft));
        await index.UpsertDBQAsync(MakeEntry(examId1, DBQStatus.Completed));
        await index.UpsertDBQAsync(MakeEntry(examId2, DBQStatus.Signed));

        List<DBQIndexEntry> forExam1 = await index.GetDBQsForExamAsync(examId1);
        Assert.That(forExam1, Has.Count.EqualTo(2));
        Assert.That(forExam1.All(d => d.ExamId == examId1), Is.True);
    }

    [Test]
    public async Task DBQIndexGrain_CanGetCompletedDBQs()
    {
        IDBQIndexGrain index = NewIndex();
        string examId = $"CP-EXAM:{Guid.NewGuid()}";
        await index.UpsertDBQAsync(MakeEntry(examId, DBQStatus.Draft));
        await index.UpsertDBQAsync(MakeEntry(examId, DBQStatus.Completed));
        await index.UpsertDBQAsync(MakeEntry(examId, DBQStatus.Signed));

        List<DBQIndexEntry> completed = await index.GetCompletedDBQsAsync();
        Assert.That(completed, Has.Count.EqualTo(2));
        Assert.That(completed.All(d => d.Status is DBQStatus.Completed or DBQStatus.Signed), Is.True);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// CPWorkflowGrainTests
// ═══════════════════════════════════════════════════════════════════════════

[TestFixture]
public class CPWorkflowGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain NewWorkflow() =>
        _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>($"PAT-{Guid.NewGuid()}");

    private static Task<string> ScheduleExam(IPatientWorkflowGrain wf) =>
        wf.ScheduleCPExamAsync(
            "Jane Veteran",
            CPExamType.Initial,
            DateTime.UtcNow.AddDays(10),
            "Dr. Adams",
            "MD",
            CPExaminerType.VAPhysician,
            "Room 101",
            "VAMC Boston",
            "CLM-2025-XYZ",
            "Compensation",
            new List<string> { "M54.5" },
            "scheduler@va.gov");

    private static Task<string> CreateDbq(IPatientWorkflowGrain wf, string examId) =>
        wf.CreateDBQAsync(
            examId,
            "Jane Veteran",
            DBQType.Spine,
            "21-0960C-3",
            "Spine (Thoracolumbar) Conditions",
            "CLM-2025-XYZ",
            "Lumbar degenerative disc disease",
            "M51.16",
            "Intervertebral disc degeneration, lumbar region");

    [Test]
    public async Task WorkflowGrain_CanScheduleExam()
    {
        IPatientWorkflowGrain wf = NewWorkflow();
        string examId = await ScheduleExam(wf);

        Assert.That(examId, Does.StartWith("CP-EXAM:"));
        List<CPExamIndexEntry> exams = await wf.GetCPExamsAsync();
        Assert.That(exams, Has.Count.EqualTo(1));
        Assert.That(exams[0].Status, Is.EqualTo(CPExamStatus.Scheduled));
    }

    [Test]
    public async Task WorkflowGrain_CanScheduleMultipleExams()
    {
        IPatientWorkflowGrain wf = NewWorkflow();
        await ScheduleExam(wf);
        await ScheduleExam(wf);

        List<CPExamIndexEntry> exams = await wf.GetCPExamsAsync();
        Assert.That(exams, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task WorkflowGrain_CanCompleteCPExam()
    {
        IPatientWorkflowGrain wf = NewWorkflow();
        string examId = await ScheduleExam(wf);
        await wf.CompleteCPExamAsync(examId,
            new List<string> { "Lumbar DDD" },
            nexus: true,
            "As likely as not related to military service.");

        CPExamState exam = await wf.GetCPExamAsync(examId);
        Assert.That(exam.Status, Is.EqualTo(CPExamStatus.Completed));
        Assert.That(exam.Nexus, Is.True);
        List<CPExamIndexEntry> completed = await wf.GetCompletedCPExamsAsync();
        Assert.That(completed, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task WorkflowGrain_CanCancelCPExam()
    {
        IPatientWorkflowGrain wf = NewWorkflow();
        string examId = await ScheduleExam(wf);
        await wf.CancelCPExamAsync(examId, "Veteran cancelled appointment.");

        CPExamState exam = await wf.GetCPExamAsync(examId);
        Assert.That(exam.Status, Is.EqualTo(CPExamStatus.Cancelled));
        List<CPExamIndexEntry> scheduled = await wf.GetScheduledCPExamsAsync();
        Assert.That(scheduled, Has.Count.EqualTo(0));
    }

    [Test]
    public async Task WorkflowGrain_CanRescheduleCPExam()
    {
        IPatientWorkflowGrain wf = NewWorkflow();
        string examId = await ScheduleExam(wf);
        DateTime newDate = DateTime.UtcNow.AddDays(28);
        await wf.RescheduleCPExamAsync(examId, newDate, "Examiner unavailable.");

        CPExamState exam = await wf.GetCPExamAsync(examId);
        Assert.That(exam.Status, Is.EqualTo(CPExamStatus.Rescheduled));
        List<CPExamIndexEntry> scheduled = await wf.GetScheduledCPExamsAsync();
        Assert.That(scheduled, Has.Count.EqualTo(1));
        Assert.That(scheduled[0].Status, Is.EqualTo(CPExamStatus.Rescheduled));
    }

    [Test]
    public async Task WorkflowGrain_CanCreateDBQ()
    {
        IPatientWorkflowGrain wf = NewWorkflow();
        string examId = await ScheduleExam(wf);
        string dbqId = await CreateDbq(wf, examId);

        Assert.That(dbqId, Does.StartWith("CP-DBQ:"));
        List<DBQIndexEntry> dbqs = await wf.GetDBQsAsync();
        Assert.That(dbqs, Has.Count.EqualTo(1));
        Assert.That(dbqs[0].Status, Is.EqualTo(DBQStatus.Draft));
    }

    [Test]
    public async Task WorkflowGrain_CreatingDBQLinksToExam()
    {
        IPatientWorkflowGrain wf = NewWorkflow();
        string examId = await ScheduleExam(wf);
        string dbqId = await CreateDbq(wf, examId);

        // DBQ should be linked to exam
        List<DBQIndexEntry> dbqs = await wf.GetDBQsForExamAsync(examId);
        Assert.That(dbqs, Has.Count.EqualTo(1));
        Assert.That(dbqs[0].DbqId, Is.EqualTo(dbqId));

        // Exam should have 1 DBQ in its index entry
        List<CPExamIndexEntry> exams = await wf.GetCPExamsAsync();
        Assert.That(exams[0].DbqCount, Is.EqualTo(1));
    }

    [Test]
    public async Task WorkflowGrain_CanUpdateDBQSections()
    {
        IPatientWorkflowGrain wf = NewWorkflow();
        string examId = await ScheduleExam(wf);
        string dbqId = await CreateDbq(wf, examId);

        await wf.UpdateDBQSectionsAsync(dbqId,
            "History of low back pain.",
            "Pain 7/10, radiating to left leg.",
            "Cannot sit or stand for more than 20 minutes.",
            "Flexion 40°, Extension 15°.",
            string.Empty,
            "MRI: L4-L5 disc herniation.");

        DBQState state = await wf.GetDBQAsync(dbqId);
        Assert.That(state.HistorySection, Does.Contain("low back pain"));
        Assert.That(state.RangeOfMotionSection, Does.Contain("40°"));
    }

    [Test]
    public async Task WorkflowGrain_CanRecordDBQOpinion()
    {
        IPatientWorkflowGrain wf = NewWorkflow();
        string examId = await ScheduleExam(wf);
        string dbqId = await CreateDbq(wf, examId);

        await wf.RecordDBQOpinionAsync(dbqId,
            nexusOpinion: true,
            "As likely as not related to in-service injury.",
            "Reviewed STRs and service treatment records confirm injury.",
            ServiceConnectionType.DirectService,
            residualsPermanent: true,
            expectedImprovement: false);

        DBQState state = await wf.GetDBQAsync(dbqId);
        Assert.That(state.NexusOpinion, Is.True);
        Assert.That(state.ServiceConnectionType, Is.EqualTo(ServiceConnectionType.DirectService));

        List<DBQIndexEntry> dbqs = await wf.GetDBQsAsync();
        Assert.That(dbqs[0].ServiceConnectionType, Is.EqualTo(ServiceConnectionType.DirectService));
    }

    [Test]
    public async Task WorkflowGrain_CanSetDBQRating()
    {
        IPatientWorkflowGrain wf = NewWorkflow();
        string examId = await ScheduleExam(wf);
        string dbqId = await CreateDbq(wf, examId);
        await wf.SetDBQRatingAsync(dbqId, 40);

        DBQState state = await wf.GetDBQAsync(dbqId);
        Assert.That(state.ProposedRating, Is.EqualTo(40));

        List<DBQIndexEntry> dbqs = await wf.GetDBQsAsync();
        Assert.That(dbqs[0].ProposedRating, Is.EqualTo(40));
    }

    [Test]
    public async Task WorkflowGrain_CanCompleteDBQ()
    {
        IPatientWorkflowGrain wf = NewWorkflow();
        string examId = await ScheduleExam(wf);
        string dbqId = await CreateDbq(wf, examId);
        await wf.CompleteDBQAsync(dbqId);

        DBQState state = await wf.GetDBQAsync(dbqId);
        Assert.That(state.Status, Is.EqualTo(DBQStatus.Completed));
        Assert.That(state.CompletedDate, Is.Not.Null);
    }

    [Test]
    public async Task WorkflowGrain_CanSignDBQ()
    {
        IPatientWorkflowGrain wf = NewWorkflow();
        string examId = await ScheduleExam(wf);
        string dbqId = await CreateDbq(wf, examId);
        await wf.CompleteDBQAsync(dbqId);
        await wf.SignDBQAsync(dbqId, "Dr. Adams, MD");

        DBQState state = await wf.GetDBQAsync(dbqId);
        Assert.That(state.Status, Is.EqualTo(DBQStatus.Signed));
        Assert.That(state.SignedBy, Is.EqualTo("Dr. Adams, MD"));
    }

    [Test]
    public async Task WorkflowGrain_MultipleDBQsForOneExam()
    {
        IPatientWorkflowGrain wf = NewWorkflow();
        string examId = await ScheduleExam(wf);
        await CreateDbq(wf, examId);
        await wf.CreateDBQAsync(examId, "Jane Veteran", DBQType.PTSD,
            "21-0781", "PTSD DBQ", "CLM-2025-XYZ",
            "PTSD", "F43.10", "Post-traumatic stress disorder, unspecified");

        List<DBQIndexEntry> forExam = await wf.GetDBQsForExamAsync(examId);
        Assert.That(forExam, Has.Count.EqualTo(2));

        List<CPExamIndexEntry> exams = await wf.GetCPExamsAsync();
        Assert.That(exams[0].DbqCount, Is.EqualTo(2));
    }

    [Test]
    public async Task WorkflowGrain_RatingPrepReturnsCompletedDBQs()
    {
        IPatientWorkflowGrain wf = NewWorkflow();
        string examId = await ScheduleExam(wf);
        string dbq1 = await CreateDbq(wf, examId);
        string dbq2 = await wf.CreateDBQAsync(examId, "Jane Veteran", DBQType.PTSD,
            "21-0781", "PTSD DBQ", "CLM-2025-XYZ",
            "PTSD", "F43.10", "PTSD, unspecified");

        await wf.SetDBQRatingAsync(dbq1, 20);
        await wf.CompleteDBQAsync(dbq1);
        await wf.SetDBQRatingAsync(dbq2, 50);
        await wf.SignDBQAsync(dbq2, "Dr. Adams, MD");

        List<DBQIndexEntry> all = await wf.GetDBQsAsync();
        List<DBQIndexEntry> completed = all.Where(d => d.Status is DBQStatus.Completed or DBQStatus.Signed).ToList();
        Assert.That(completed, Has.Count.EqualTo(2));
        Assert.That(completed.Max(d => d.ProposedRating), Is.EqualTo(50));
    }
}
