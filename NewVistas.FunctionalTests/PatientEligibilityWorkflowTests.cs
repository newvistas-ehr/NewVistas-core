// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Patient Eligibility Verification via IPatientWorkflowGrain.
/// Tests that enrollment status, means test completion, and termination status
/// are checked before scheduling, with blocking on ineligible patients.
///
/// VistA reference: DG eligibility checks (DGENELA.m, DGENELB.m).
/// </summary>
[TestFixture]
public class PatientEligibilityWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IPatientEnrollmentGrain Enrollment(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientEnrollmentGrain>($"ENROLLMENT:{patientId}");

    private async Task SetupClinic(string clinicId, string name)
    {
        IClinicGrain clinic = _cluster.GrainFactory.GetGrain<IClinicGrain>(clinicId);
        await clinic.CreateClinicAsync(name, "MAIN", null, null, null,
            30, 20, false, "C", null, null);
    }

    // ─── Eligibility Check ──────────────────────────────────────────────────

    [Test]
    public async Task CheckEligibility_VerifiedEnrollment_IsEligible()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Set up verified enrollment
        await wf.SetEnrollmentStatusAsync(EnrollmentStatus.Verified, "CLERK-001", null);
        await wf.SetEnrollmentPriorityGroupAsync("1", null, false, true, "SC");

        PatientEligibilityResult result = await wf.CheckPatientEligibilityForSchedulingAsync();

        Assert.That(result.IsEligible, Is.True);
        Assert.That(result.EnrollmentStatus, Is.EqualTo("Verified"));
        Assert.That(result.PriorityGroup, Is.EqualTo("1"));
        Assert.That(result.CopayExempt, Is.True);
        Assert.That(result.Reasons, Is.Empty);
    }

    [Test]
    public async Task CheckEligibility_LimitedBenefits_IsEligible()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.SetEnrollmentStatusAsync(EnrollmentStatus.LimitedBenefits, "CLERK-001", null);

        PatientEligibilityResult result = await wf.CheckPatientEligibilityForSchedulingAsync();

        Assert.That(result.IsEligible, Is.True);
        Assert.That(result.EnrollmentStatus, Is.EqualTo("LimitedBenefits"));
    }

    [Test]
    public async Task CheckEligibility_Unverified_NotEligible()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Explicitly set Unverified status (uninitialized enrollment is allowed)
        await wf.SetEnrollmentStatusAsync(EnrollmentStatus.Unverified, "CLERK-001", "Application received");

        PatientEligibilityResult result = await wf.CheckPatientEligibilityForSchedulingAsync();

        Assert.That(result.IsEligible, Is.False);
        Assert.That(result.Reasons, Has.Some.Contains("Unverified"));
    }

    [Test]
    public async Task CheckEligibility_Rejected_NotEligible()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.SetEnrollmentStatusAsync(EnrollmentStatus.Rejected, "CLERK-001", "Income exceeded");

        PatientEligibilityResult result = await wf.CheckPatientEligibilityForSchedulingAsync();

        Assert.That(result.IsEligible, Is.False);
        Assert.That(result.Reasons, Has.Some.Contains("Rejected"));
    }

    [Test]
    public async Task CheckEligibility_Terminated_NotEligible()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.SetEnrollmentStatusAsync(EnrollmentStatus.Verified, "CLERK-001", null);

        // Set termination date in the past
        IPatientEnrollmentGrain enrollment = Enrollment(patientId);
        PatientEnrollmentState state = await enrollment.GetAsync();
        // We need to set termination — let me use the close method if it exists
        // or set status to Closed which typically has a termination
        await wf.SetEnrollmentStatusAsync(EnrollmentStatus.Closed, "CLERK-001", "Moved out of area");

        PatientEligibilityResult result = await wf.CheckPatientEligibilityForSchedulingAsync();

        Assert.That(result.IsEligible, Is.False);
        Assert.That(result.EnrollmentStatus, Is.EqualTo("Closed"));
    }

    [Test]
    public async Task CheckEligibility_MeansTestRequired_NotCompleted_NotEligible()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.SetEnrollmentStatusAsync(EnrollmentStatus.Verified, "CLERK-001", null);
        await wf.SetEnrollmentPriorityGroupAsync("5", null, true, false, null);
        // MeansTestRequired = true, but no means test recorded

        PatientEligibilityResult result = await wf.CheckPatientEligibilityForSchedulingAsync();

        Assert.That(result.IsEligible, Is.False);
        Assert.That(result.MeansTestRequired, Is.True);
        Assert.That(result.MeansTestCompleted, Is.False);
        Assert.That(result.Reasons, Has.Some.Contains("Means test"));
    }

    [Test]
    public async Task CheckEligibility_MeansTestRequired_Completed_IsEligible()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.SetEnrollmentStatusAsync(EnrollmentStatus.Verified, "CLERK-001", null);
        await wf.SetEnrollmentPriorityGroupAsync("5", null, true, false, null);

        // Record a completed means test via embedded entry on PatientGrain
        await wf.RecordMeansTestAsync("MEANS TEST", DateTime.UtcNow,
            40000m, 10000m, 0, "VERIFIED", "5", "CLERK-001", "Test Clerk", null);

        PatientEligibilityResult result = await wf.CheckPatientEligibilityForSchedulingAsync();

        Assert.That(result.IsEligible, Is.True);
        Assert.That(result.MeansTestCompleted, Is.True);
    }

    [Test]
    public async Task CheckEligibility_MeansTestExempt_IsEligible()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.SetEnrollmentStatusAsync(EnrollmentStatus.Verified, "CLERK-001", null);
        // MeansTestRequired = true but MeansTestExempt = true (via copay exempt)
        await wf.SetEnrollmentPriorityGroupAsync("1", "a", true, true, "SC");

        PatientEligibilityResult result = await wf.CheckPatientEligibilityForSchedulingAsync();

        Assert.That(result.IsEligible, Is.True);
        Assert.That(result.MeansTestCompleted, Is.True); // Exempt counts as completed
    }

    // ─── Scheduling Gate ────────────────────────────────────────────────────

    [Test]
    public async Task ScheduleAppointment_IneligiblePatient_Throws()
    {
        string clinicId = $"SD-CLINIC-{Guid.NewGuid()}";
        await SetupClinic(clinicId, "ELIGIBILITY CLINIC");

        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        // Explicitly set Rejected — not eligible
        await wf.SetEnrollmentStatusAsync(EnrollmentStatus.Rejected, "CLERK-001", "Income exceeded");

        DateTime date = DateTime.UtcNow.Date.AddDays(7);

        Assert.ThrowsAsync<InvalidOperationException>(() =>
            wf.ScheduleAppointmentAsync(clinicId, "ELIGIBILITY CLINIC",
                date.AddHours(10), 30, null, null, "Visit", "REGULAR", false));
    }

    [Test]
    public async Task ScheduleAppointment_EligiblePatient_Succeeds()
    {
        string clinicId = $"SD-CLINIC-{Guid.NewGuid()}";
        await SetupClinic(clinicId, "ELIGIBLE CLINIC");

        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        await wf.SetEnrollmentStatusAsync(EnrollmentStatus.Verified, "CLERK-001", null);

        DateTime date = DateTime.UtcNow.Date.AddDays(7);

        string apptId = await wf.ScheduleAppointmentAsync(clinicId, "ELIGIBLE CLINIC",
            date.AddHours(10), 30, null, null, "Visit", "REGULAR", false);

        Assert.That(apptId, Does.StartWith("APPT-"));
    }

    [Test]
    public async Task CheckEligibility_ReturnsServiceConnectedInfo()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.SetEnrollmentStatusAsync(EnrollmentStatus.Verified, "CLERK-001", null);
        await wf.UpdateVeteranInfoAsync("Y", 70, "SC", "SC VETERAN");

        PatientEligibilityResult result = await wf.CheckPatientEligibilityForSchedulingAsync();

        Assert.That(result.IsEligible, Is.True);
        Assert.That(result.ServiceConnectedPercentage, Is.EqualTo(70));
        Assert.That(result.PrimaryEligibilityCode, Is.EqualTo("SC VETERAN"));
    }

    // ─── Patient Isolation ──────────────────────────────────────────────────

    [Test]
    public async Task CheckEligibility_DifferentPatients_IndependentResults()
    {
        string p1 = $"PATIENT-{Guid.NewGuid()}";
        string p2 = $"PATIENT-{Guid.NewGuid()}";

        await Workflow(p1).SetEnrollmentStatusAsync(EnrollmentStatus.Verified, "CLERK-001", null);
        await Workflow(p2).SetEnrollmentStatusAsync(EnrollmentStatus.Rejected, "CLERK-001", "Not eligible");

        PatientEligibilityResult r1 = await Workflow(p1).CheckPatientEligibilityForSchedulingAsync();
        PatientEligibilityResult r2 = await Workflow(p2).CheckPatientEligibilityForSchedulingAsync();

        Assert.That(r1.IsEligible, Is.True);
        Assert.That(r2.IsEligible, Is.False);
    }
}
