// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Appointment Letter Printing and Reminder Batch Processing
/// via IPatientWorkflowGrain — grain-to-grain orchestration.
///
/// VistA reference: SD appointment letters, SD reminder processing.
/// </summary>
[TestFixture]
public class AppointmentLetterReminderWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private async Task<string> SetupClinic(string clinicId, string name)
    {
        IClinicGrain clinic = _cluster.GrainFactory.GetGrain<IClinicGrain>(clinicId);
        await clinic.CreateClinicAsync(name, "MAIN", null, "555-0100", "Room 201",
            30, 20, false, "C", null, null);
        return clinicId;
    }

    private async Task<(string patientId, string apptId)> CreatePatientWithAppointment(
        string clinicId, string clinicName, int daysFromNow = 7)
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        // Set up patient demographics
        await wf.UpdateDemographicsAsync("SMITH, JOHN Q", "M", new DateTime(1970, 3, 15), "123-45-6789");
        await wf.UpdateAddressAsync("123 Main St", null, null, "Anytown", "VA", "22030");
        await wf.UpdateContactInfoAsync("555-0199", null, "john.smith@email.com");

        // Set enrollment to verified
        await wf.SetEnrollmentStatusAsync(EnrollmentStatus.Verified, "CLERK-001", null);

        // Schedule an appointment
        DateTime apptDate = DateTime.UtcNow.Date.AddDays(daysFromNow).AddHours(10);
        string apptId = await wf.ScheduleAppointmentAsync(
            clinicId, clinicName, apptDate, 30,
            "PROV-001", "Dr. Jones", "Annual physical", "REGULAR", false);

        return (patientId, apptId);
    }

    // ═══ APPOINTMENT LETTER GENERATION ══════════════════════════════════════

    [Test]
    public async Task GenerateAppointmentLetter_Confirmation_IncludesAllFields()
    {
        string clinicId = $"SD-CLINIC-{Guid.NewGuid()}";
        await SetupClinic(clinicId, "PRIMARY CARE");

        (string patientId, string apptId) = await CreatePatientWithAppointment(clinicId, "PRIMARY CARE");

        IPatientWorkflowGrain wf = Workflow(patientId);
        AppointmentLetterContent letter = await wf.GenerateAppointmentLetterAsync(apptId, "CONFIRMATION");

        Assert.That(letter.AppointmentId, Is.EqualTo(apptId));
        Assert.That(letter.PatientName, Is.EqualTo("SMITH, JOHN Q"));
        Assert.That(letter.StreetAddress1, Is.EqualTo("123 Main St"));
        Assert.That(letter.City, Is.EqualTo("Anytown"));
        Assert.That(letter.State, Is.EqualTo("VA"));
        Assert.That(letter.ZipCode, Is.EqualTo("22030"));
        Assert.That(letter.PhoneNumber, Is.EqualTo("555-0199"));
        Assert.That(letter.ClinicName, Is.EqualTo("PRIMARY CARE"));
        Assert.That(letter.ClinicPhone, Is.EqualTo("555-0100"));
        Assert.That(letter.ClinicLocation, Is.EqualTo("Room 201"));
        Assert.That(letter.ProviderName, Is.EqualTo("Dr. Jones"));
        Assert.That(letter.Purpose, Is.EqualTo("Annual physical"));
        Assert.That(letter.DurationMinutes, Is.EqualTo(30));
        Assert.That(letter.LetterType, Is.EqualTo("CONFIRMATION"));
        Assert.That(letter.Instructions, Does.Contain("confirmed"));
    }

    [Test]
    public async Task GenerateAppointmentLetter_Reminder_HasReminderInstructions()
    {
        string clinicId = $"SD-CLINIC-{Guid.NewGuid()}";
        await SetupClinic(clinicId, "CARDIOLOGY");

        (string patientId, string apptId) = await CreatePatientWithAppointment(clinicId, "CARDIOLOGY");

        IPatientWorkflowGrain wf = Workflow(patientId);
        AppointmentLetterContent letter = await wf.GenerateAppointmentLetterAsync(apptId, "REMINDER");

        Assert.That(letter.LetterType, Is.EqualTo("REMINDER"));
        Assert.That(letter.Instructions, Does.Contain("15 minutes early"));
        Assert.That(letter.Instructions, Does.Contain("medications"));
    }

    [Test]
    public async Task GenerateAppointmentLetter_IncludesAppointmentDateTime()
    {
        string clinicId = $"SD-CLINIC-{Guid.NewGuid()}";
        await SetupClinic(clinicId, "DERMATOLOGY");

        (string patientId, string apptId) = await CreatePatientWithAppointment(clinicId, "DERMATOLOGY", daysFromNow: 14);

        IPatientWorkflowGrain wf = Workflow(patientId);
        AppointmentLetterContent letter = await wf.GenerateAppointmentLetterAsync(apptId, "CONFIRMATION");

        Assert.That(letter.AppointmentDateTime.Date, Is.EqualTo(DateTime.UtcNow.Date.AddDays(14)));
        Assert.That(letter.AppointmentType, Is.EqualTo("REGULAR"));
    }

    // ═══ REMINDER BATCH PROCESSING ══════════════════════════════════════════

    [Test]
    public async Task ProcessReminderBatch_NoAppointments_EmptyResult()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        ReminderBatchResult result = await wf.ProcessReminderBatchAsync(7);

        Assert.That(result.TotalEvaluated, Is.EqualTo(0));
        Assert.That(result.RemindersSent, Is.EqualTo(0));
    }

    [Test]
    public async Task ProcessReminderBatch_UpcomingAppointment_SendsReminder()
    {
        string clinicId = $"SD-CLINIC-{Guid.NewGuid()}";
        await SetupClinic(clinicId, "MENTAL HEALTH");

        (string patientId, string apptId) = await CreatePatientWithAppointment(
            clinicId, "MENTAL HEALTH", daysFromNow: 3);

        IPatientWorkflowGrain wf = Workflow(patientId);
        ReminderBatchResult result = await wf.ProcessReminderBatchAsync(7);

        Assert.That(result.RemindersSent, Is.GreaterThanOrEqualTo(1));

        ReminderBatchEntry sentEntry = result.Entries.First(e => e.AppointmentId == apptId);
        Assert.That(sentEntry.Status, Is.EqualTo("SENT"));
        Assert.That(sentEntry.ReminderSent, Is.True);

        // Verify the grain flag is set
        IAppointmentGrain apptGrain = _cluster.GrainFactory.GetGrain<IAppointmentGrain>(apptId);
        AppointmentState apptState = await apptGrain.GetAppointmentAsync();
        Assert.That(apptState.ReminderSent, Is.True);
    }

    [Test]
    public async Task ProcessReminderBatch_AlreadySent_Skips()
    {
        string clinicId = $"SD-CLINIC-{Guid.NewGuid()}";
        await SetupClinic(clinicId, "ORTHOPEDICS");

        (string patientId, string apptId) = await CreatePatientWithAppointment(
            clinicId, "ORTHOPEDICS", daysFromNow: 3);

        IPatientWorkflowGrain wf = Workflow(patientId);

        // First batch — sends reminder
        ReminderBatchResult first = await wf.ProcessReminderBatchAsync(7);
        Assert.That(first.RemindersSent, Is.GreaterThanOrEqualTo(1));

        // Second batch — should skip (already sent)
        ReminderBatchResult second = await wf.ProcessReminderBatchAsync(7);
        Assert.That(second.RemindersSent, Is.EqualTo(0));
        Assert.That(second.AlreadySent, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task ProcessReminderBatch_BeyondWindow_Skips()
    {
        string clinicId = $"SD-CLINIC-{Guid.NewGuid()}";
        await SetupClinic(clinicId, "PHARMACY MTAC");

        (string patientId, string apptId) = await CreatePatientWithAppointment(
            clinicId, "PHARMACY MTAC", daysFromNow: 30);

        IPatientWorkflowGrain wf = Workflow(patientId);

        // Process with 7-day window — 30-day appointment should be skipped
        ReminderBatchResult result = await wf.ProcessReminderBatchAsync(7);

        Assert.That(result.RemindersSent, Is.EqualTo(0));
        Assert.That(result.Skipped, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public async Task GetAppointmentsNeedingReminders_ReturnsUnsentOnly()
    {
        string clinicId = $"SD-CLINIC-{Guid.NewGuid()}";
        await SetupClinic(clinicId, "NEUROLOGY");

        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        await wf.SetEnrollmentStatusAsync(EnrollmentStatus.Verified, "CLERK-001", null);

        // Schedule two appointments within the reminder window
        DateTime date1 = DateTime.UtcNow.Date.AddDays(3).AddHours(9);
        DateTime date2 = DateTime.UtcNow.Date.AddDays(5).AddHours(14);
        string appt1 = await wf.ScheduleAppointmentAsync(clinicId, "NEUROLOGY",
            date1, 30, null, null, "Follow-up", "FOLLOW-UP", false);
        string appt2 = await wf.ScheduleAppointmentAsync(clinicId, "NEUROLOGY",
            date2, 30, null, null, "New patient", "REGULAR", false);

        // Mark one as sent
        await _cluster.GrainFactory.GetGrain<IAppointmentGrain>(appt1).MarkReminderSentAsync();

        List<AppointmentEntry> pending = await wf.GetAppointmentsNeedingRemindersAsync(7);

        Assert.That(pending, Has.Count.EqualTo(1));
        Assert.That(pending[0].AppointmentId, Is.EqualTo(appt2));
    }

    [Test]
    public async Task ProcessReminderBatch_CancelledAppointment_Skips()
    {
        string clinicId = $"SD-CLINIC-{Guid.NewGuid()}";
        await SetupClinic(clinicId, "UROLOGY");

        (string patientId, string apptId) = await CreatePatientWithAppointment(
            clinicId, "UROLOGY", daysFromNow: 3);

        IPatientWorkflowGrain wf = Workflow(patientId);

        // Cancel the appointment
        await wf.CancelAppointmentAsync(apptId);

        ReminderBatchResult result = await wf.ProcessReminderBatchAsync(7);

        // Cancelled should be skipped
        Assert.That(result.RemindersSent, Is.EqualTo(0));
    }

    // ═══ MULTIPLE APPOINTMENTS ══════════════════════════════════════════════

    [Test]
    public async Task ProcessReminderBatch_MultipleAppointments_ProcessesAll()
    {
        string clinicId = $"SD-CLINIC-{Guid.NewGuid()}";
        await SetupClinic(clinicId, "GI CLINIC");

        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);
        await wf.SetEnrollmentStatusAsync(EnrollmentStatus.Verified, "CLERK-001", null);

        // Schedule 3 appointments within window
        for (int i = 1; i <= 3; i++)
        {
            await wf.ScheduleAppointmentAsync(clinicId, "GI CLINIC",
                DateTime.UtcNow.Date.AddDays(i + 1).AddHours(9 + i), 30,
                null, null, $"Visit {i}", "REGULAR", false);
        }

        ReminderBatchResult result = await wf.ProcessReminderBatchAsync(7);

        Assert.That(result.RemindersSent, Is.EqualTo(3));
        Assert.That(result.Entries.Count(e => e.Status == "SENT"), Is.EqualTo(3));
    }
}
