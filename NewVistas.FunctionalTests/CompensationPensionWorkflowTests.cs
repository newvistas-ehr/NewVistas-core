// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Compensation and Pension — VistA File #396.
/// Tests end-to-end workflows via <see cref="IPatientWorkflowGrain"/>.
/// </summary>
[TestFixture]
public class CompensationPensionWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ── Helper ─────────────────────────────────────────────────────────────────

    private Task<string> ScheduleExam(IPatientWorkflowGrain wf)
        => wf.ScheduleCPExamAsync(
            patientName: "DOE, JOHN A",
            examType: CPExamType.Initial,
            scheduledDate: new DateTime(2025, 6, 15, 9, 0, 0),
            examinerName: "Dr. Sarah Williams",
            examinerTitle: "MD",
            examinerType: CPExaminerType.VAPhysician,
            examLocation: "Room 302",
            examFacility: "VA Medical Center",
            claimNumber: "CLM-2025-001",
            benefitType: "Compensation",
            disabilityClaimedCodes: new List<string> { "M54.5", "F43.10" },
            createdBy: "CLERK-001");

    private async Task<string> CreateDbq(IPatientWorkflowGrain wf, string examId)
        => await wf.CreateDBQAsync(
            examId: examId,
            patientName: "DOE, JOHN A",
            dbqType: DBQType.Musculoskeletal,
            dbqFormNumber: "21-0960M-14",
            dbqTitle: "Back (Thoracolumbar Spine) Conditions",
            claimNumber: "CLM-2025-001",
            conditionClaimed: "Chronic low back pain",
            diagnosisCode: "M54.5",
            diagnosisDescription: "Low back pain, unspecified");

    // ── Exam Scheduling Tests ──────────────────────────────────────────────────

    [Test]
    public async Task ScheduleCPExam_ReturnsId_AndAppearsInIndex()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string examId = await ScheduleExam(wf);

        Assert.That(examId, Is.Not.Null.And.Not.Empty);

        List<CPExamIndexEntry> all = await wf.GetCPExamsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].ExamId, Is.EqualTo(examId));
        Assert.That(all[0].ExamType, Is.EqualTo(CPExamType.Initial));
        Assert.That(all[0].Status, Is.EqualTo(CPExamStatus.Scheduled));
        Assert.That(all[0].ClaimNumber, Is.EqualTo("CLM-2025-001"));
        Assert.That(all[0].DisabilityCount, Is.EqualTo(2));
    }

    [Test]
    public async Task GetCPExam_ReturnsFullState()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string examId = await ScheduleExam(wf);

        CPExamState state = await wf.GetCPExamAsync(examId);

        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.PatientName, Is.EqualTo("DOE, JOHN A"));
        Assert.That(state.ExamType, Is.EqualTo(CPExamType.Initial));
        Assert.That(state.Status, Is.EqualTo(CPExamStatus.Scheduled));
        Assert.That(state.ExaminerName, Is.EqualTo("Dr. Sarah Williams"));
        Assert.That(state.ExaminerType, Is.EqualTo(CPExaminerType.VAPhysician));
        Assert.That(state.ExamLocation, Is.EqualTo("Room 302"));
        Assert.That(state.ExamFacility, Is.EqualTo("VA Medical Center"));
        Assert.That(state.ClaimNumber, Is.EqualTo("CLM-2025-001"));
        Assert.That(state.BenefitType, Is.EqualTo("Compensation"));
        Assert.That(state.DisabilityClaimedCodes, Has.Count.EqualTo(2));
        Assert.That(state.DisabilityClaimedCodes, Contains.Item("M54.5"));
        Assert.That(state.DisabilityClaimedCodes, Contains.Item("F43.10"));
    }

    [Test]
    public async Task CompleteCPExam_SetsCompletedWithDiagnoses()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string examId = await ScheduleExam(wf);

        List<string> diagnoses = new List<string>
        {
            "Lumbar degenerative disc disease",
            "PTSD, chronic"
        };

        await wf.CompleteCPExamAsync(examId, diagnoses, true,
            "In my medical opinion, it is at least as likely as not that the veteran's lumbar condition is related to in-service parachute jumps documented in STR.");

        CPExamState state = await wf.GetCPExamAsync(examId);
        Assert.That(state.Status, Is.EqualTo(CPExamStatus.Completed));
        Assert.That(state.CompletedDate, Is.Not.Null);
        Assert.That(state.Diagnoses, Has.Count.EqualTo(2));
        Assert.That(state.Nexus, Is.True);
        Assert.That(state.NexusRationale, Does.Contain("parachute jumps"));

        List<CPExamIndexEntry> index = await wf.GetCPExamsAsync();
        Assert.That(index[0].Status, Is.EqualTo(CPExamStatus.Completed));
    }

    [Test]
    public async Task CancelCPExam_SetsCancelledWithReason()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string examId = await ScheduleExam(wf);

        await wf.CancelCPExamAsync(examId, "Veteran called to cancel — will reschedule via VBA");

        CPExamState state = await wf.GetCPExamAsync(examId);
        Assert.That(state.Status, Is.EqualTo(CPExamStatus.Cancelled));
        Assert.That(state.CancellationReason, Does.Contain("Veteran called"));
    }

    [Test]
    public async Task RescheduleCPExam_UpdatesDateAndStatus()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string examId = await ScheduleExam(wf);
        DateTime newDate = new DateTime(2025, 7, 1, 10, 0, 0);

        await wf.RescheduleCPExamAsync(examId, newDate, "Examiner unavailable on original date");

        CPExamState state = await wf.GetCPExamAsync(examId);
        Assert.That(state.Status, Is.EqualTo(CPExamStatus.Rescheduled));
        Assert.That(state.ScheduledDate, Is.EqualTo(newDate));

        List<CPExamIndexEntry> index = await wf.GetCPExamsAsync();
        Assert.That(index[0].Status, Is.EqualTo(CPExamStatus.Rescheduled));
        Assert.That(index[0].ScheduledDate, Is.EqualTo(newDate));
    }

    [Test]
    public async Task GetScheduledCPExams_FiltersScheduledAndRescheduled()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string exam1 = await ScheduleExam(wf);
        string exam2 = await wf.ScheduleCPExamAsync(
            "DOE, JOHN A", CPExamType.Increase,
            new DateTime(2025, 8, 1), "Dr. Brown", "DO",
            CPExaminerType.ContractExaminer, "Room 400", "VA Outpatient Clinic",
            "CLM-2025-002", "Compensation",
            new List<string> { "G47.33" }, "CLERK-002");

        // Complete exam1
        await wf.CompleteCPExamAsync(exam1, new List<string> { "DDD" }, false, "No nexus.");

        List<CPExamIndexEntry> scheduled = await wf.GetScheduledCPExamsAsync();
        Assert.That(scheduled, Has.Count.EqualTo(1));
        Assert.That(scheduled[0].ExamId, Is.EqualTo(exam2));
    }

    [Test]
    public async Task GetCompletedCPExams_FiltersCompletedOnly()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string exam1 = await ScheduleExam(wf);
        string exam2 = await wf.ScheduleCPExamAsync(
            "DOE, JOHN A", CPExamType.Review,
            DateTime.UtcNow, "Dr. Jones", "MD",
            CPExaminerType.VAPhysician, "Room 100", "VAMC",
            "CLM-2025-003", "Compensation",
            new List<string> { "K21.0" }, "CLERK-003");

        await wf.CompleteCPExamAsync(exam1, new List<string> { "Lumbago" }, true, "Service connected.");

        List<CPExamIndexEntry> completed = await wf.GetCompletedCPExamsAsync();
        Assert.That(completed, Has.Count.EqualTo(1));
        Assert.That(completed[0].ExamId, Is.EqualTo(exam1));
    }

    // ── DBQ Tests ──────────────────────────────────────────────────────────────

    [Test]
    public async Task CreateDBQ_ReturnsId_AndAppearsInIndex()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string examId = await ScheduleExam(wf);
        string dbqId = await CreateDbq(wf, examId);

        Assert.That(dbqId, Is.Not.Null.And.Not.Empty);

        List<DBQIndexEntry> all = await wf.GetDBQsAsync();
        Assert.That(all, Has.Count.EqualTo(1));
        Assert.That(all[0].DbqId, Is.EqualTo(dbqId));
        Assert.That(all[0].ExamId, Is.EqualTo(examId));
        Assert.That(all[0].DbqType, Is.EqualTo(DBQType.Musculoskeletal));
        Assert.That(all[0].ConditionClaimed, Is.EqualTo("Chronic low back pain"));
        Assert.That(all[0].Status, Is.EqualTo(DBQStatus.Draft));
    }

    [Test]
    public async Task GetDBQ_ReturnsFullState()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string examId = await ScheduleExam(wf);
        string dbqId = await CreateDbq(wf, examId);

        DBQState state = await wf.GetDBQAsync(dbqId);

        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.ExamId, Is.EqualTo(examId));
        Assert.That(state.PatientName, Is.EqualTo("DOE, JOHN A"));
        Assert.That(state.DbqType, Is.EqualTo(DBQType.Musculoskeletal));
        Assert.That(state.DbqFormNumber, Is.EqualTo("21-0960M-14"));
        Assert.That(state.DbqTitle, Is.EqualTo("Back (Thoracolumbar Spine) Conditions"));
        Assert.That(state.DiagnosisCode, Is.EqualTo("M54.5"));
    }

    [Test]
    public async Task UpdateDBQSections_PopulatesClinicalNarrative()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string examId = await ScheduleExam(wf);
        string dbqId = await CreateDbq(wf, examId);

        await wf.UpdateDBQSectionsAsync(
            dbqId,
            historySection: "Veteran reports onset of LBP after parachute landing in 1991.",
            symptomsSection: "Constant aching pain, 6/10, worse with prolonged standing.",
            functionalImpactSection: "Unable to lift > 20 lbs. Requires sit-stand workstation.",
            rangeOfMotionSection: "Flexion 60/90 deg. Extension 15/30 deg. Pain on all movements.",
            mentalStatusSection: string.Empty,
            diagnosticTestsSection: "MRI lumbar spine: L4-L5 disc herniation, moderate central stenosis.");

        DBQState state = await wf.GetDBQAsync(dbqId);
        Assert.That(state.HistorySection, Does.Contain("parachute landing"));
        Assert.That(state.SymptomsSection, Does.Contain("6/10"));
        Assert.That(state.FunctionalImpactSection, Does.Contain("sit-stand workstation"));
        Assert.That(state.RangeOfMotionSection, Does.Contain("Flexion 60/90"));
        Assert.That(state.DiagnosticTestsSection, Does.Contain("disc herniation"));
    }

    [Test]
    public async Task RecordDBQOpinion_SetsNexusAndServiceConnection()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string examId = await ScheduleExam(wf);
        string dbqId = await CreateDbq(wf, examId);

        await wf.RecordDBQOpinionAsync(
            dbqId,
            nexusOpinion: true,
            nexusStatement: "At least as likely as not that lumbar condition is related to in-service airborne duties.",
            opinionsSection: "Based on STR documenting parachute jumps and current imaging findings, a nexus is established.",
            serviceConnectionType: ServiceConnectionType.DirectService,
            residualsPermanent: true,
            expectedImprovement: false);

        DBQState state = await wf.GetDBQAsync(dbqId);
        Assert.That(state.NexusOpinion, Is.True);
        Assert.That(state.NexusStatement, Does.Contain("At least as likely as not"));
        Assert.That(state.ServiceConnectionType, Is.EqualTo(ServiceConnectionType.DirectService));
        Assert.That(state.ResidualsPermanent, Is.True);
        Assert.That(state.ExpectedImprovement, Is.False);
    }

    [Test]
    public async Task SetDBQRating_SetsProposedRating()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string examId = await ScheduleExam(wf);
        string dbqId = await CreateDbq(wf, examId);

        await wf.SetDBQRatingAsync(dbqId, 40);

        DBQState state = await wf.GetDBQAsync(dbqId);
        Assert.That(state.ProposedRating, Is.EqualTo(40));

        List<DBQIndexEntry> index = await wf.GetDBQsAsync();
        Assert.That(index[0].ProposedRating, Is.EqualTo(40));
    }

    [Test]
    public async Task CompleteAndSignDBQ_TransitionsThroughStatuses()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string examId = await ScheduleExam(wf);
        string dbqId = await CreateDbq(wf, examId);

        // Complete
        await wf.CompleteDBQAsync(dbqId);

        DBQState completed = await wf.GetDBQAsync(dbqId);
        Assert.That(completed.Status, Is.EqualTo(DBQStatus.Completed));
        Assert.That(completed.CompletedDate, Is.Not.Null);

        // Sign
        await wf.SignDBQAsync(dbqId, "Dr. Sarah Williams, MD");

        DBQState signed = await wf.GetDBQAsync(dbqId);
        Assert.That(signed.Status, Is.EqualTo(DBQStatus.Signed));
        Assert.That(signed.SignedBy, Is.EqualTo("Dr. Sarah Williams, MD"));
        Assert.That(signed.SignedDate, Is.Not.Null);

        List<DBQIndexEntry> index = await wf.GetDBQsAsync();
        Assert.That(index[0].Status, Is.EqualTo(DBQStatus.Signed));
    }

    [Test]
    public async Task GetDBQsForExam_FiltersCorrectly()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        string exam1 = await ScheduleExam(wf);
        string exam2 = await wf.ScheduleCPExamAsync(
            "DOE, JOHN A", CPExamType.Increase,
            DateTime.UtcNow, "Dr. Brown", "DO",
            CPExaminerType.ContractExaminer, "Room 200", "VAMC",
            "CLM-2025-004", "Compensation",
            new List<string> { "F43.10" }, "CLERK-004");

        await CreateDbq(wf, exam1);
        await wf.CreateDBQAsync(exam1, "DOE, JOHN A", DBQType.PTSD,
            "21-0960P-3", "PTSD DBQ", "CLM-2025-001", "PTSD",
            "F43.10", "Post-traumatic stress disorder");
        await wf.CreateDBQAsync(exam2, "DOE, JOHN A", DBQType.HearingLoss,
            "21-0960N-3", "Hearing Loss DBQ", "CLM-2025-004", "Bilateral hearing loss",
            "H91.90", "Unspecified hearing loss");

        List<DBQIndexEntry> exam1Dbqs = await wf.GetDBQsForExamAsync(exam1);
        List<DBQIndexEntry> exam2Dbqs = await wf.GetDBQsForExamAsync(exam2);

        Assert.That(exam1Dbqs, Has.Count.EqualTo(2));
        Assert.That(exam2Dbqs, Has.Count.EqualTo(1));
        Assert.That(exam2Dbqs[0].DbqType, Is.EqualTo(DBQType.HearingLoss));
    }

    [Test]
    public async Task MultiplePatients_IndependentExamsAndDBQs()
    {
        string p1 = $"PATIENT-{Guid.NewGuid()}";
        string p2 = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf1 = Workflow(p1);
        IPatientWorkflowGrain wf2 = Workflow(p2);

        string exam1 = await ScheduleExam(wf1);
        await CreateDbq(wf1, exam1);

        string exam2 = await wf2.ScheduleCPExamAsync(
            "SMITH, JANE B", CPExamType.Review,
            DateTime.UtcNow, "Dr. Lee", "MD",
            CPExaminerType.VAPhysician, "Room 100", "VAMC",
            "CLM-2025-005", "Pension",
            new List<string> { "I10" }, "CLERK-005");

        List<CPExamIndexEntry> p1Exams = await wf1.GetCPExamsAsync();
        List<CPExamIndexEntry> p2Exams = await wf2.GetCPExamsAsync();
        List<DBQIndexEntry> p1Dbqs = await wf1.GetDBQsAsync();
        List<DBQIndexEntry> p2Dbqs = await wf2.GetDBQsAsync();

        Assert.That(p1Exams, Has.Count.EqualTo(1));
        Assert.That(p2Exams, Has.Count.EqualTo(1));
        Assert.That(p1Dbqs, Has.Count.EqualTo(1));
        Assert.That(p2Dbqs, Has.Count.EqualTo(0));
    }
}
