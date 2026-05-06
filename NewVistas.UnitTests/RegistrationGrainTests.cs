// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for Registration expansion grains:
/// PatientEnrollment (File #27.11), PrfAssignment (Files #26.11/#26.13), MstHistory (File #29.11).
/// </summary>
[TestFixture]
public class PatientEnrollmentGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientEnrollmentGrain NewGrain(string? patientId = null)
    {
        string id = patientId ?? $"PATIENT-{Guid.NewGuid()}";
        return _cluster.GrainFactory.GetGrain<IPatientEnrollmentGrain>($"ENROLLMENT:{id}");
    }

    [Test]
    public async Task EnrollmentGrain_Initialize_SetsPatientId()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientEnrollmentGrain grain = NewGrain(patientId);

        await grain.InitializeAsync(patientId, "APPLICATION", "SITE-001", "Bedford VA");

        PatientEnrollmentState state = await grain.GetAsync();
        Assert.That(state.PatientId, Is.EqualTo(patientId));
        Assert.That(state.EnrollmentSource, Is.EqualTo("APPLICATION"));
        Assert.That(state.EnrollmentSiteName, Is.EqualTo("Bedford VA"));
    }

    [Test]
    public async Task EnrollmentGrain_Initialize_IsIdempotent()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientEnrollmentGrain grain = NewGrain(patientId);

        await grain.InitializeAsync(patientId, "APPLICATION", "SITE-001", "Bedford VA");
        // Second call should be no-op
        await grain.InitializeAsync(patientId, "DIFFERENT_SOURCE", "SITE-002", "Orlando VA");

        PatientEnrollmentState state = await grain.GetAsync();
        Assert.That(state.EnrollmentSource, Is.EqualTo("APPLICATION"), "Second init should not overwrite");
    }

    [Test]
    public async Task EnrollmentGrain_UpdateStatus_ChangesStatus()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientEnrollmentGrain grain = NewGrain(patientId);
        await grain.InitializeAsync(patientId, "APPLICATION", null, null);

        await grain.UpdateStatusAsync(EnrollmentStatus.Verified, "USER-001", "Initial enrollment approved");

        PatientEnrollmentState state = await grain.GetAsync();
        Assert.That(state.EnrollmentStatus, Is.EqualTo(EnrollmentStatus.Verified));
    }

    [Test]
    public async Task EnrollmentGrain_SetPriorityGroup_PersistsAllFields()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientEnrollmentGrain grain = NewGrain(patientId);
        await grain.InitializeAsync(patientId, null, null, null);

        await grain.SetPriorityGroupAsync(
            "1", "1a", true, false, null);

        PatientEnrollmentState state = await grain.GetAsync();
        Assert.That(state.PriorityGroup, Is.EqualTo("1"));
        Assert.That(state.PrioritySubgroup, Is.EqualTo("1a"));
        Assert.That(state.MeansTestRequired, Is.True);
        Assert.That(state.CopayExempt, Is.False);
    }

    [Test]
    public async Task EnrollmentGrain_SetDates_PersistsDates()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientEnrollmentGrain grain = NewGrain(patientId);
        await grain.InitializeAsync(patientId, null, null, null);

        DateTime appDate = new DateTime(2024, 1, 15);
        DateTime effDate = new DateTime(2024, 2, 1);

        await grain.SetDatesAsync(appDate, effDate, null);

        PatientEnrollmentState state = await grain.GetAsync();
        Assert.That(state.ApplicationDate, Is.EqualTo(appDate));
        Assert.That(state.EffectiveDate, Is.EqualTo(effDate));
        Assert.That(state.TerminationDate, Is.Null);
    }

    [Test]
    public async Task EnrollmentGrain_FullEnrollmentWorkflow()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientEnrollmentGrain grain = NewGrain(patientId);

        await grain.InitializeAsync(patientId, "APPLICATION", "SITE-001", "Bedford VA");
        await grain.UpdateStatusAsync(EnrollmentStatus.PendingMeansTest, "USER-001", null);
        await grain.SetPriorityGroupAsync("5", null, true, false, null);
        await grain.SetDatesAsync(DateTime.UtcNow.AddDays(-10), null, null);
        await grain.UpdateStatusAsync(EnrollmentStatus.Verified, "USER-002", "Means test complete");

        PatientEnrollmentState state = await grain.GetAsync();
        Assert.That(state.EnrollmentStatus, Is.EqualTo(EnrollmentStatus.Verified));
        Assert.That(state.PriorityGroup, Is.EqualTo("5"));
        Assert.That(state.MeansTestRequired, Is.True);
        Assert.That(state.ApplicationDate, Is.Not.Null);
    }
}

[TestFixture]
public class PrfAssignmentGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPrfAssignmentGrain NewGrain() =>
        _cluster.GrainFactory.GetGrain<IPrfAssignmentGrain>($"PRF-ASSIGN:{Guid.NewGuid()}");

    [Test]
    public async Task PrfGrain_AssignFlag_AppearsInActiveFlags()
    {
        IPrfAssignmentGrain grain = NewGrain();

        await grain.AssignFlagAsync(
            "FLAG-001", "BEHAVIORAL", "NATIONAL", true,
            "USER-001", "Smith, John",
            "Patient has history of disruptive behavior in clinical settings");

        List<PrfFlagAssignment> active = await grain.GetActiveFlagsAsync();
        Assert.That(active, Has.Count.EqualTo(1));
        Assert.That(active[0].FlagName, Is.EqualTo("BEHAVIORAL"));
        Assert.That(active[0].IsNational, Is.True);
        Assert.That(active[0].AssignedByUserName, Is.EqualTo("Smith, John"));
        Assert.That(active[0].IsActive, Is.True);
    }

    [Test]
    public async Task PrfGrain_AssignMultipleFlags_AllActive()
    {
        IPrfAssignmentGrain grain = NewGrain();

        await grain.AssignFlagAsync(
            "FLAG-001", "BEHAVIORAL", "NATIONAL", true,
            "USER-001", "Smith, John", null);
        await grain.AssignFlagAsync(
            "FLAG-002", "HIGH RISK FOR SUICIDE", "NATIONAL", true,
            "USER-001", "Smith, John", "Recent suicide attempt");

        List<PrfFlagAssignment> active = await grain.GetActiveFlagsAsync();
        Assert.That(active, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task PrfGrain_DeactivateFlag_RemovesFromActiveList()
    {
        IPrfAssignmentGrain grain = NewGrain();
        await grain.AssignFlagAsync(
            "FLAG-001", "BEHAVIORAL", "NATIONAL", true,
            "USER-001", "Smith, John", null);

        await grain.DeactivateFlagAsync("FLAG-001", "Behavior no longer an issue", "USER-002");

        List<PrfFlagAssignment> active = await grain.GetActiveFlagsAsync();
        Assert.That(active, Is.Empty);

        // Full state should still show the deactivated flag
        PrfAssignmentState state = await grain.GetAsync();
        Assert.That(state.Assignments, Has.Count.EqualTo(1));
        Assert.That(state.Assignments[0].IsActive, Is.False);
    }

    [Test]
    public async Task PrfGrain_RecordReview_AddsReviewEntry()
    {
        IPrfAssignmentGrain grain = NewGrain();
        await grain.AssignFlagAsync(
            "FLAG-001", "BEHAVIORAL", "NATIONAL", true,
            "USER-001", "Smith, John", null);

        DateTime reviewDate = DateTime.UtcNow;
        await grain.RecordReviewAsync(
            "FLAG-001", "USER-003", "Jones, Mary",
            reviewDate, "Flag remains clinically appropriate");

        PrfAssignmentState state = await grain.GetAsync();
        PrfFlagAssignment assignment = state.Assignments.First(a => a.FlagId == "FLAG-001");
        Assert.That(assignment.ReviewedByUserName, Is.EqualTo("Jones, Mary"));
        Assert.That(assignment.ReviewDate, Is.EqualTo(reviewDate));
        Assert.That(assignment.Narrative, Does.Contain("clinically appropriate"));
    }

    [Test]
    public async Task PrfGrain_GetActiveFlags_ExcludesDeactivated()
    {
        IPrfAssignmentGrain grain = NewGrain();
        await grain.AssignFlagAsync(
            "FLAG-A", "BEHAVIORAL", "NATIONAL", true,
            "USER-001", "Smith, John", null);
        await grain.AssignFlagAsync(
            "FLAG-B", "HIGH RISK FOR SUICIDE", "NATIONAL", true,
            "USER-001", "Smith, John", null);

        await grain.DeactivateFlagAsync("FLAG-A", "Resolved", "USER-002");

        List<PrfFlagAssignment> active = await grain.GetActiveFlagsAsync();
        Assert.That(active, Has.Count.EqualTo(1));
        Assert.That(active[0].FlagId, Is.EqualTo("FLAG-B"));
    }
}

[TestFixture]
public class MstHistoryGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IMstHistoryGrain NewGrain() =>
        _cluster.GrainFactory.GetGrain<IMstHistoryGrain>($"MST:{Guid.NewGuid()}");

    [Test]
    public async Task MstGrain_RecordScreening_PersistsEntry()
    {
        IMstHistoryGrain grain = NewGrain();
        DateTime screenDate = new DateTime(2024, 3, 15);

        await grain.RecordScreeningAsync(
            screenDate, MstStatus.Verified,
            "USER-001", "Dr. Adams",
            "Primary Care Clinic", "Patient disclosed at annual screening");

        MstHistoryState state = await grain.GetAsync();
        Assert.That(state.Screenings, Has.Count.EqualTo(1));
        Assert.That(state.Screenings[0].ScreeningStatus, Is.EqualTo(MstStatus.Verified));
        Assert.That(state.Screenings[0].ScreenedByUserName, Is.EqualTo("Dr. Adams"));
        Assert.That(state.Screenings[0].ScreeningDate, Is.EqualTo(screenDate));
    }

    [Test]
    public async Task MstGrain_VerifiedScreening_SetsMstPositive()
    {
        IMstHistoryGrain grain = NewGrain();

        await grain.RecordScreeningAsync(
            DateTime.UtcNow, MstStatus.Verified,
            "USER-001", "Dr. Adams", null, null);

        MstHistoryState state = await grain.GetAsync();
        Assert.That(state.MstPositive, Is.True);
        Assert.That(state.CurrentStatus, Is.EqualTo(MstStatus.Verified));
    }

    [Test]
    public async Task MstGrain_DeniedScreening_DoesNotSetMstPositive()
    {
        IMstHistoryGrain grain = NewGrain();

        await grain.RecordScreeningAsync(
            DateTime.UtcNow, MstStatus.Denied,
            "USER-001", "Dr. Baker", null, null);

        MstHistoryState state = await grain.GetAsync();
        Assert.That(state.MstPositive, Is.False);
    }

    [Test]
    public async Task MstGrain_MultipleScreenings_AllRecorded()
    {
        IMstHistoryGrain grain = NewGrain();

        await grain.RecordScreeningAsync(
            new DateTime(2020, 1, 1), MstStatus.Unasked,
            "USER-001", "Dr. Adams", "Primary Care", null);
        await grain.RecordScreeningAsync(
            new DateTime(2021, 1, 1), MstStatus.Denied,
            "USER-001", "Dr. Adams", "Primary Care", null);
        await grain.RecordScreeningAsync(
            new DateTime(2024, 1, 1), MstStatus.Verified,
            "USER-002", "Dr. Baker", "Primary Care", "Now willing to disclose");

        MstHistoryState state = await grain.GetAsync();
        Assert.That(state.Screenings, Has.Count.EqualTo(3));
        Assert.That(state.CurrentStatus, Is.EqualTo(MstStatus.Verified));
        Assert.That(state.MstPositive, Is.True);
    }

    [Test]
    public async Task MstGrain_SetCurrentStatus_UpdatesWithoutAddingScreening()
    {
        IMstHistoryGrain grain = NewGrain();

        await grain.SetCurrentStatusAsync(MstStatus.NoResponse);

        MstHistoryState state = await grain.GetAsync();
        Assert.That(state.CurrentStatus, Is.EqualTo(MstStatus.NoResponse));
        Assert.That(state.Screenings, Is.Empty);
    }

    [Test]
    public async Task MstGrain_SetDisclosure_PersistsLocationAndDate()
    {
        IMstHistoryGrain grain = NewGrain();
        DateTime disclosureDate = new DateTime(2023, 6, 20);

        await grain.SetDisclosureAsync("Vet Center Counseling Session", disclosureDate);

        MstHistoryState state = await grain.GetAsync();
        Assert.That(state.DisclosureLocation, Is.EqualTo("Vet Center Counseling Session"));
        Assert.That(state.DisclosureDate, Is.EqualTo(disclosureDate));
    }
}
