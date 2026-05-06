// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for the scheduling enhancement features that go beyond core VistA/RPMS.
/// Tests the Site Flavor Architecture feature gates:
///   - PROVIDER_AVAILABILITY: Provider-level availability patterns and time blocks
///   - PROVIDER_UNAVAILABILITY_BATCH: Batch cancel/reassign when provider suddenly unavailable
///   - PATIENT_SELF_SCHEDULING: Patient portal self-scheduling
///
/// VistA scheduling is clinic-centric (File #44.005). These enhancements add provider-centric
/// availability which VistA never had. When features are disabled, the system falls back to
/// the standard VistA clinic-wide 8-17 slot grid.
/// </summary>
[TestFixture]
public class SchedulingEnhancementWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain GetWorkflow(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private IPatientGrain GetPatient(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);

    private ISiteParametersGrain GetSiteParams() =>
        _cluster.GrainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");

    private IProviderAvailabilityGrain GetAvailability(string providerId) =>
        _cluster.GrainFactory.GetGrain<IProviderAvailabilityGrain>($"PROV-AVAIL:{providerId}");

    private IClinicGrain GetClinic(string clinicId) =>
        _cluster.GrainFactory.GetGrain<IClinicGrain>(clinicId);

    private IClinicIndexGrain GetClinicIndex() =>
        _cluster.GrainFactory.GetGrain<IClinicIndexGrain>("SD-CLINIC-INDEX");

    private async Task<string> SetupPatientAsync(string name)
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientGrain grain = GetPatient(patientId);
        await grain.UpdateDemographicsAsync(name, "M", new DateTime(1970, 1, 1), "123-45-6789");

        // Set enrollment to make patient eligible for scheduling
        IPatientEnrollmentGrain enrollment = _cluster.GrainFactory.GetGrain<IPatientEnrollmentGrain>($"ENROLL:{patientId}");
        await enrollment.UpdateStatusAsync(EnrollmentStatus.Verified, "SYSTEM", null);

        return patientId;
    }

    private async Task<string> SetupClinicAsync(string name, bool acceptsPatientSelfSchedule = false)
    {
        string clinicId = $"SD-CLINIC-{Guid.NewGuid()}";
        IClinicGrain clinic = GetClinic(clinicId);
        await clinic.CreateClinicAsync(name, "MAIN", "323", null, null,
            30, 20, false, "C", null, null);

        await GetClinicIndex().AddOrUpdateClinicAsync(new ClinicEntry
        {
            ClinicId = clinicId,
            Name = name,
            Division = "MAIN",
            StopCode = "323",
            AppointmentLength = 30,
            Status = "ACTIVE",
            AcceptsPatientSelfSchedule = acceptsPatientSelfSchedule
        });

        return clinicId;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PROVIDER_AVAILABILITY Feature Gate Tests
    // ═════════════════════════════════════════════════════════════════════════

    [Test, Order(1)]
    public async Task ProviderAvailability_Disabled_ScheduleIgnoresProviderStatus()
    {
        // When PROVIDER_AVAILABILITY is disabled, scheduling should NOT check provider status.
        // This is the standard VistA behavior — clinic-centric only.
        ISiteParametersGrain siteParams = GetSiteParams();
        await siteParams.DisableFeatureAsync("PROVIDER_AVAILABILITY");

        string patientId = await SetupPatientAsync("VISTA,DEFAULT A");
        string clinicId = await SetupClinicAsync("VistA Default Clinic");
        string providerId = $"PROV-{Guid.NewGuid()}";

        // Set provider as UNAVAILABLE
        await GetAvailability(providerId).UpdateProviderStatusAsync("UNAVAILABLE", "Test", "Admin");

        // Scheduling should still succeed — VistA doesn't check provider status
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);
        DateTime apptTime = DateTime.UtcNow.Date.AddDays(7).AddHours(10);
        string appointmentId = await workflow.ScheduleAppointmentAsync(
            clinicId, "VistA Default Clinic", apptTime, 30,
            providerId, "Dr. Test", "Follow-up", "REGULAR");

        Assert.That(appointmentId, Is.Not.Empty);
    }

    [Test, Order(2)]
    public async Task ProviderAvailability_Enabled_RejectsUnavailableProvider()
    {
        // When PROVIDER_AVAILABILITY is enabled, scheduling should check provider status.
        ISiteParametersGrain siteParams = GetSiteParams();
        await siteParams.EnableFeatureAsync("PROVIDER_AVAILABILITY");

        string patientId = await SetupPatientAsync("ENHANCED,CHECK A");
        string clinicId = await SetupClinicAsync("Enhanced Check Clinic");
        string providerId = $"PROV-{Guid.NewGuid()}";

        // Set provider as UNAVAILABLE
        await GetAvailability(providerId).UpdateProviderStatusAsync("UNAVAILABLE", "Illness", "Admin");

        // Scheduling should fail with provider status check
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);
        DateTime apptTime = DateTime.UtcNow.Date.AddDays(7).AddHours(10);

        InvalidOperationException? ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await workflow.ScheduleAppointmentAsync(
                clinicId, "Enhanced Check Clinic", apptTime, 30,
                providerId, "Dr. Test", "Follow-up", "REGULAR");
        });

        Assert.That(ex!.Message, Does.Contain("UNAVAILABLE"));
    }

    [Test, Order(3)]
    public async Task ProviderAvailability_Enabled_RejectsOutsideWindow()
    {
        ISiteParametersGrain siteParams = GetSiteParams();
        await siteParams.EnableFeatureAsync("PROVIDER_AVAILABILITY");

        string patientId = await SetupPatientAsync("WINDOW,CHECK A");
        string clinicId = await SetupClinicAsync("Window Check Clinic");
        string providerId = $"PROV-{Guid.NewGuid()}";

        // Provider available 8-12 on Monday only
        DateTime monday = DateTime.UtcNow.Date.AddDays(7);
        while (monday.DayOfWeek != DayOfWeek.Monday) monday = monday.AddDays(1);

        await GetAvailability(providerId).AddWeeklyPatternAsync(new WeeklyAvailabilityPattern
        {
            ClinicId = clinicId, ClinicName = "Window Check Clinic",
            DaysOfWeek = new() { DayOfWeek.Monday },
            StartHour = 8, EndHour = 12
        });

        // Try to schedule at 3pm — outside availability window
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);
        InvalidOperationException? ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await workflow.ScheduleAppointmentAsync(
                clinicId, "Window Check Clinic", monday.AddHours(15), 30,
                providerId, "Dr. Test", "Follow-up", "REGULAR");
        });

        Assert.That(ex!.Message, Does.Contain("outside provider's availability"));
    }

    [Test, Order(4)]
    public async Task ProviderAvailability_Enabled_AcceptsWithinWindow()
    {
        ISiteParametersGrain siteParams = GetSiteParams();
        await siteParams.EnableFeatureAsync("PROVIDER_AVAILABILITY");

        string patientId = await SetupPatientAsync("WINDOW,VALID A");
        string clinicId = await SetupClinicAsync("Window Valid Clinic");
        string providerId = $"PROV-{Guid.NewGuid()}";

        DateTime monday = DateTime.UtcNow.Date.AddDays(7);
        while (monday.DayOfWeek != DayOfWeek.Monday) monday = monday.AddDays(1);

        await GetAvailability(providerId).AddWeeklyPatternAsync(new WeeklyAvailabilityPattern
        {
            ClinicId = clinicId, ClinicName = "Window Valid Clinic",
            DaysOfWeek = new() { DayOfWeek.Monday },
            StartHour = 8, EndHour = 12
        });

        // Schedule at 10am — within window
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);
        string appointmentId = await workflow.ScheduleAppointmentAsync(
            clinicId, "Window Valid Clinic", monday.AddHours(10), 30,
            providerId, "Dr. Test", "Follow-up", "REGULAR");

        Assert.That(appointmentId, Is.Not.Empty);
    }

    [Test, Order(5)]
    public async Task ProviderAvailability_Disabled_SlotsReturnDefault817Grid()
    {
        ISiteParametersGrain siteParams = GetSiteParams();
        await siteParams.DisableFeatureAsync("PROVIDER_AVAILABILITY");

        string patientId = await SetupPatientAsync("SLOT,DEFAULT A");
        string clinicId = await SetupClinicAsync("Slot Default Clinic");

        IPatientWorkflowGrain workflow = GetWorkflow(patientId);
        DateTime date = DateTime.UtcNow.Date.AddDays(14);
        List<AvailableSlot> slots = await workflow.GetAvailableSlotsAsync(clinicId, date);

        // Default VistA: 8-17, 30-min slots = 18 slots
        Assert.That(slots, Has.Count.EqualTo(18));
        Assert.That(slots[0].StartTime.Hour, Is.EqualTo(8));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PROVIDER_UNAVAILABILITY_BATCH Feature Gate Tests
    // ═════════════════════════════════════════════════════════════════════════

    [Test, Order(10)]
    public async Task ProviderUnavailabilityBatch_Disabled_ThrowsOnCreate()
    {
        ISiteParametersGrain siteParams = GetSiteParams();
        await siteParams.DisableFeatureAsync("PROVIDER_UNAVAILABILITY_BATCH");

        string eventId = $"PROV-UNAVAIL:{Guid.NewGuid()}";
        IProviderUnavailabilityGrain grain = _cluster.GrainFactory
            .GetGrain<IProviderUnavailabilityGrain>(eventId);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await grain.CreateEventAsync("PROV-1", "Dr. Test",
                DateTime.UtcNow, DateTime.UtcNow.AddDays(3),
                "ILLNESS", null, "USER-1", "Admin");
        });
    }

    [Test, Order(11)]
    public async Task ProviderUnavailabilityBatch_Enabled_CreatesAndIdentifiesAffected()
    {
        ISiteParametersGrain siteParams = GetSiteParams();
        await siteParams.EnableFeatureAsync("PROVIDER_UNAVAILABILITY_BATCH");
        await siteParams.DisableFeatureAsync("PROVIDER_AVAILABILITY"); // Don't need availability checks

        // Create a patient and schedule an appointment
        string patientId = await SetupPatientAsync("BATCH,TEST A");
        string clinicId = await SetupClinicAsync("Batch Test Clinic");
        string providerId = $"PROV-{Guid.NewGuid()}";

        IPatientWorkflowGrain workflow = GetWorkflow(patientId);
        DateTime apptTime = DateTime.UtcNow.AddDays(2).Date.AddHours(10);
        string appointmentId = await workflow.ScheduleAppointmentAsync(
            clinicId, "Batch Test Clinic", apptTime, 30,
            providerId, "Dr. Batch", "Follow-up", "REGULAR");

        // Create unavailability event covering the appointment
        string eventId = $"PROV-UNAVAIL:{Guid.NewGuid()}";
        IProviderUnavailabilityGrain unavailGrain = _cluster.GrainFactory
            .GetGrain<IProviderUnavailabilityGrain>(eventId);

        ProviderUnavailabilityState state = await unavailGrain.CreateEventAsync(
            providerId, "Dr. Batch",
            DateTime.UtcNow, DateTime.UtcNow.AddDays(5),
            "ILLNESS", "Flu", "ADMIN-1", "Admin User");

        Assert.That(state.Status, Is.EqualTo("Pending"));
        Assert.That(state.TotalAffected, Is.GreaterThanOrEqualTo(1));
        Assert.That(state.AffectedAppointments.Any(a => a.AppointmentId == appointmentId), Is.True);
    }

    [Test, Order(12)]
    public async Task ProviderUnavailabilityBatch_Enabled_BatchCancelsAppointments()
    {
        ISiteParametersGrain siteParams = GetSiteParams();
        await siteParams.EnableFeatureAsync("PROVIDER_UNAVAILABILITY_BATCH");
        await siteParams.DisableFeatureAsync("PROVIDER_AVAILABILITY");
        await siteParams.DisableFeatureAsync("APPOINTMENT_WAITLIST"); // Don't need waitlist

        string patientId = await SetupPatientAsync("BATCHCANCEL,TEST A");
        string clinicId = await SetupClinicAsync("Batch Cancel Clinic");
        string providerId = $"PROV-{Guid.NewGuid()}";

        IPatientWorkflowGrain workflow = GetWorkflow(patientId);
        DateTime apptTime = DateTime.UtcNow.AddDays(3).Date.AddHours(9);
        string appointmentId = await workflow.ScheduleAppointmentAsync(
            clinicId, "Batch Cancel Clinic", apptTime, 30,
            providerId, "Dr. Cancel", "Checkup", "REGULAR");

        // Create and execute batch cancellation
        string eventId = $"PROV-UNAVAIL:{Guid.NewGuid()}";
        IProviderUnavailabilityGrain unavailGrain = _cluster.GrainFactory
            .GetGrain<IProviderUnavailabilityGrain>(eventId);

        await unavailGrain.CreateEventAsync(
            providerId, "Dr. Cancel",
            DateTime.UtcNow, DateTime.UtcNow.AddDays(7),
            "INJURY", null, "ADMIN-1", "Admin");

        ProviderUnavailabilityResult result = await unavailGrain.ExecuteBatchCancellationAsync();

        Assert.That(result.Processed, Is.GreaterThanOrEqualTo(1));
        Assert.That(result.Failed, Is.EqualTo(0));

        // Verify appointment was cancelled
        AppointmentState appt = await workflow.GetAppointmentAsync(appointmentId);
        Assert.That(appt.Status, Is.EqualTo("Cancelled"));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PATIENT_SELF_SCHEDULING Feature Gate Tests
    // ═════════════════════════════════════════════════════════════════════════

    [Test, Order(20)]
    public async Task PatientSelfSchedule_Disabled_Throws()
    {
        ISiteParametersGrain siteParams = GetSiteParams();
        await siteParams.DisableFeatureAsync("PATIENT_SELF_SCHEDULING");

        string patientId = await SetupPatientAsync("PORTAL,DISABLED A");
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await workflow.PatientSelfScheduleAppointmentAsync(
                "CLINIC-1", DateTime.UtcNow.AddDays(7).AddHours(10), "Checkup", "REGULAR");
        });
    }

    [Test, Order(21)]
    public async Task PatientSelfSchedule_Enabled_SucceedsWithEligiblePatient()
    {
        ISiteParametersGrain siteParams = GetSiteParams();
        await siteParams.EnableFeatureAsync("PATIENT_SELF_SCHEDULING");
        await siteParams.DisableFeatureAsync("PROVIDER_AVAILABILITY");

        string patientId = await SetupPatientAsync("PORTAL,ENABLED A");
        string clinicId = await SetupClinicAsync("Portal Enabled Clinic", acceptsPatientSelfSchedule: true);

        IPatientWorkflowGrain workflow = GetWorkflow(patientId);
        DateTime apptTime = DateTime.UtcNow.Date.AddDays(7).AddHours(10);
        string appointmentId = await workflow.PatientSelfScheduleAppointmentAsync(
            clinicId, apptTime, "Annual checkup", "REGULAR");

        Assert.That(appointmentId, Is.Not.Empty);
    }

    [Test, Order(22)]
    public async Task PatientSelfSchedule_RejectsUrgentType()
    {
        ISiteParametersGrain siteParams = GetSiteParams();
        await siteParams.EnableFeatureAsync("PATIENT_SELF_SCHEDULING");

        string patientId = await SetupPatientAsync("PORTAL,URGENT A");
        string clinicId = await SetupClinicAsync("Portal Urgent Clinic", acceptsPatientSelfSchedule: true);

        IPatientWorkflowGrain workflow = GetWorkflow(patientId);
        DateTime apptTime = DateTime.UtcNow.Date.AddDays(7).AddHours(10);

        InvalidOperationException? ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await workflow.PatientSelfScheduleAppointmentAsync(
                clinicId, apptTime, "Emergency", "URGENT");
        });

        Assert.That(ex!.Message, Does.Contain("requires staff scheduling"));
    }

    [Test, Order(23)]
    public async Task PatientSelfSchedule_RejectsNonSelfScheduleClinic()
    {
        ISiteParametersGrain siteParams = GetSiteParams();
        await siteParams.EnableFeatureAsync("PATIENT_SELF_SCHEDULING");

        string patientId = await SetupPatientAsync("PORTAL,NONCLINIC A");
        string clinicId = await SetupClinicAsync("Staff Only Clinic", acceptsPatientSelfSchedule: false);

        IPatientWorkflowGrain workflow = GetWorkflow(patientId);
        DateTime apptTime = DateTime.UtcNow.Date.AddDays(7).AddHours(10);

        InvalidOperationException? ex = Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await workflow.PatientSelfScheduleAppointmentAsync(
                clinicId, apptTime, "Checkup", "REGULAR");
        });

        Assert.That(ex!.Message, Does.Contain("does not accept patient self-scheduling"));
    }

    [Test, Order(24)]
    public async Task PatientCancelAppointment_Disabled_Throws()
    {
        ISiteParametersGrain siteParams = GetSiteParams();
        await siteParams.DisableFeatureAsync("PATIENT_SELF_SCHEDULING");

        string patientId = await SetupPatientAsync("PORTAL,CANCELOFF A");
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await workflow.PatientCancelAppointmentAsync("APPT-FAKE", "Changed plans");
        });
    }

    [Test, Order(25)]
    public async Task PatientCancelAppointment_Enabled_CancelsWithPolicyResult()
    {
        ISiteParametersGrain siteParams = GetSiteParams();
        await siteParams.EnableFeatureAsync("PATIENT_SELF_SCHEDULING");
        await siteParams.DisableFeatureAsync("PROVIDER_AVAILABILITY");
        await siteParams.DisableFeatureAsync("APPOINTMENT_WAITLIST");

        string patientId = await SetupPatientAsync("PORTAL,CANCEL A");
        string clinicId = await SetupClinicAsync("Portal Cancel Clinic", acceptsPatientSelfSchedule: true);

        IPatientWorkflowGrain workflow = GetWorkflow(patientId);
        DateTime apptTime = DateTime.UtcNow.Date.AddDays(14).AddHours(10);
        string appointmentId = await workflow.PatientSelfScheduleAppointmentAsync(
            clinicId, apptTime, "Checkup", "REGULAR");

        // Cancel it
        CancellationPolicyResult result = await workflow.PatientCancelAppointmentAsync(
            appointmentId, "Schedule conflict");

        Assert.That(result.IsAllowed, Is.True);
        Assert.That(result.WasCancelled, Is.True);
        Assert.That(result.IsWithinNoticeWindow, Is.False); // 14 days out > 24h notice

        // Verify appointment is cancelled
        AppointmentState appt = await workflow.GetAppointmentAsync(appointmentId);
        Assert.That(appt.Status, Is.EqualTo("Cancelled"));
    }

    [Test, Order(26)]
    public async Task PatientReschedule_Disabled_Throws()
    {
        ISiteParametersGrain siteParams = GetSiteParams();
        await siteParams.DisableFeatureAsync("PATIENT_SELF_SCHEDULING");

        string patientId = await SetupPatientAsync("PORTAL,RESCHEDOFF A");
        IPatientWorkflowGrain workflow = GetWorkflow(patientId);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await workflow.PatientRescheduleAppointmentAsync(
                "APPT-FAKE", DateTime.UtcNow.AddDays(10), "Need different time");
        });
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Default VistA Behavior (all features disabled)
    // ═════════════════════════════════════════════════════════════════════════

    [Test, Order(30)]
    public async Task VistADefault_AllFeaturesDisabled_CoreSchedulingWorks()
    {
        // When all enhancement features are disabled, the core VistA scheduling
        // workflow should work exactly as before.
        ISiteParametersGrain siteParams = GetSiteParams();
        await siteParams.DisableFeatureAsync("PROVIDER_AVAILABILITY");
        await siteParams.DisableFeatureAsync("PROVIDER_UNAVAILABILITY_BATCH");
        await siteParams.DisableFeatureAsync("PATIENT_SELF_SCHEDULING");
        await siteParams.DisableFeatureAsync("APPOINTMENT_WAITLIST");

        string patientId = await SetupPatientAsync("VISTA,CORE A");
        string clinicId = await SetupClinicAsync("Core VistA Clinic");

        IPatientWorkflowGrain workflow = GetWorkflow(patientId);

        // Schedule
        DateTime apptTime = DateTime.UtcNow.Date.AddDays(7).AddHours(10);
        string apptId = await workflow.ScheduleAppointmentAsync(
            clinicId, "Core VistA Clinic", apptTime, 30,
            null, null, "Follow-up", "REGULAR");
        Assert.That(apptId, Is.Not.Empty);

        // Check in
        await workflow.CheckInAsync(apptId, DateTime.UtcNow);
        AppointmentState state = await workflow.GetAppointmentAsync(apptId);
        Assert.That(state.Status, Is.EqualTo("Checked In"));

        // Check out
        await workflow.CheckOutAsync(apptId, DateTime.UtcNow);
        state = await workflow.GetAppointmentAsync(apptId);
        Assert.That(state.Status, Is.EqualTo("Completed"));
    }

    [Test, Order(31)]
    public async Task VistADefault_CancelOneAtATime_Works()
    {
        // In VistA, you cancel appointments one at a time. This should always work.
        ISiteParametersGrain siteParams = GetSiteParams();
        await siteParams.DisableFeatureAsync("PROVIDER_AVAILABILITY");
        await siteParams.DisableFeatureAsync("APPOINTMENT_WAITLIST");

        string patientId = await SetupPatientAsync("VISTA,CANCEL A");
        string clinicId = await SetupClinicAsync("VistA Cancel Clinic");

        IPatientWorkflowGrain workflow = GetWorkflow(patientId);
        DateTime apptTime = DateTime.UtcNow.Date.AddDays(7).AddHours(14);
        string apptId = await workflow.ScheduleAppointmentAsync(
            clinicId, "VistA Cancel Clinic", apptTime, 30,
            null, null, null, "REGULAR");

        // Cancel individually — standard VistA workflow
        await workflow.CancelAppointmentAsync(apptId);
        AppointmentState state = await workflow.GetAppointmentAsync(apptId);
        Assert.That(state.Status, Is.EqualTo("Cancelled"));
    }
}
