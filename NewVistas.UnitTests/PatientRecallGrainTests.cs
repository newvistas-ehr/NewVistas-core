// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for Patient Recall grains — IHS RPMS SC Recall (File #403.5)
/// automated recall letter functionality. Tests the IPatientRecallGrain and
/// IPatientRecallIndexGrain directly, verifying recall lifecycle, letter generation,
/// contact attempts, scheduling, completion, cancellation, and overdue marking.
/// </summary>
[TestFixture]
public class PatientRecallGrainTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientRecallGrain GetRecallGrain(string entryId) =>
        _cluster.GrainFactory.GetGrain<IPatientRecallGrain>(entryId);

    private IPatientRecallIndexGrain GetIndex() =>
        _cluster.GrainFactory.GetGrain<IPatientRecallIndexGrain>("SD-RECALL-IDX");

    private async Task<PatientRecallState> CreateTestEntryAsync(
        string entryId,
        string patientId = "PATIENT-1",
        string patientName = "DOE,JOHN",
        string clinicId = "CLINIC-PRIMARY",
        string clinicName = "Primary Care",
        string recallType = "FOLLOW-UP",
        DateTime? recallDate = null)
    {
        IPatientRecallGrain grain = GetRecallGrain(entryId);
        PatientRecallState result = await grain.CreateEntryAsync(
            patientId, patientName, clinicId, clinicName,
            recallType, recallDate ?? new DateTime(2026, 6, 15),
            "PROV-1", "Dr. Jones",
            "Hypertension", "Recheck blood pressure",
            "PROV-1", "Dr. Jones");
        return result;
    }

    [Test]
    public async Task RecallGrain_CreatesEntry()
    {
        string entryId = $"SD-RECALL:{Guid.NewGuid()}";

        PatientRecallState result = await CreateTestEntryAsync(entryId);

        Assert.That(result.EntryId, Is.EqualTo(entryId));
        Assert.That(result.PatientId, Is.EqualTo("PATIENT-1"));
        Assert.That(result.PatientName, Is.EqualTo("DOE,JOHN"));
        Assert.That(result.ClinicName, Is.EqualTo("Primary Care"));
        Assert.That(result.RecallType, Is.EqualTo("FOLLOW-UP"));
        Assert.That(result.RecallDate, Is.EqualTo(new DateTime(2026, 6, 15)));
        Assert.That(result.Status, Is.EqualTo("PENDING"));
        Assert.That(result.ProviderName, Is.EqualTo("Dr. Jones"));
        Assert.That(result.Diagnosis, Is.EqualTo("Hypertension"));
        Assert.That(result.LetterCount, Is.EqualTo(0));
        Assert.That(result.ContactAttemptCount, Is.EqualTo(0));
    }

    [Test]
    public async Task RecallGrain_GeneratesLetter()
    {
        string entryId = $"SD-RECALL:{Guid.NewGuid()}";
        await CreateTestEntryAsync(entryId);
        IPatientRecallGrain grain = GetRecallGrain(entryId);

        await grain.GenerateLetterAsync("FIRST_NOTICE", "Clerk Smith");

        PatientRecallState state = await grain.GetEntryAsync();
        Assert.That(state.Status, Is.EqualTo("LETTER_SENT"));
        Assert.That(state.LetterCount, Is.EqualTo(1));
        Assert.That(state.Letters, Has.Count.EqualTo(1));
        Assert.That(state.Letters[0].LetterType, Is.EqualTo("FIRST_NOTICE"));
        Assert.That(state.Letters[0].GeneratedByName, Is.EqualTo("Clerk Smith"));
    }

    [Test]
    public async Task RecallGrain_RecordsContactAttempt_Reached()
    {
        string entryId = $"SD-RECALL:{Guid.NewGuid()}";
        await CreateTestEntryAsync(entryId);
        IPatientRecallGrain grain = GetRecallGrain(entryId);

        await grain.RecordContactAttemptAsync("PHONE", "REACHED", "Nurse Williams", "Patient confirmed appointment");

        PatientRecallState state = await grain.GetEntryAsync();
        Assert.That(state.Status, Is.EqualTo("CONTACTED"));
        Assert.That(state.ContactAttemptCount, Is.EqualTo(1));
        Assert.That(state.ContactAttempts[0].ContactMethod, Is.EqualTo("PHONE"));
        Assert.That(state.ContactAttempts[0].Result, Is.EqualTo("REACHED"));
    }

    [Test]
    public async Task RecallGrain_RecordsContactAttempt_NoAnswer()
    {
        string entryId = $"SD-RECALL:{Guid.NewGuid()}";
        await CreateTestEntryAsync(entryId);
        IPatientRecallGrain grain = GetRecallGrain(entryId);

        await grain.RecordContactAttemptAsync("PHONE", "NO_ANSWER", "Nurse Williams", null);

        PatientRecallState state = await grain.GetEntryAsync();
        Assert.That(state.Status, Is.EqualTo("PENDING"));
        Assert.That(state.ContactAttemptCount, Is.EqualTo(1));
    }

    [Test]
    public async Task RecallGrain_SchedulesAppointment()
    {
        string entryId = $"SD-RECALL:{Guid.NewGuid()}";
        await CreateTestEntryAsync(entryId);
        IPatientRecallGrain grain = GetRecallGrain(entryId);
        DateTime apptDate = new DateTime(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc);

        await grain.MarkAppointmentScheduledAsync("APPT-123", apptDate, "Scheduler Adams");

        PatientRecallState state = await grain.GetEntryAsync();
        Assert.That(state.Status, Is.EqualTo("APPOINTMENT_SCHEDULED"));
        Assert.That(state.ScheduledAppointmentId, Is.EqualTo("APPT-123"));
        Assert.That(state.ScheduledAppointmentDateTime, Is.EqualTo(apptDate));
        Assert.That(state.ScheduledByName, Is.EqualTo("Scheduler Adams"));
    }

    [Test]
    public async Task RecallGrain_Completes()
    {
        string entryId = $"SD-RECALL:{Guid.NewGuid()}";
        await CreateTestEntryAsync(entryId);
        IPatientRecallGrain grain = GetRecallGrain(entryId);

        await grain.MarkCompletedAsync("Dr. Jones", "Patient seen, BP well-controlled");

        PatientRecallState state = await grain.GetEntryAsync();
        Assert.That(state.Status, Is.EqualTo("COMPLETED"));
        Assert.That(state.CompletionNotes, Is.EqualTo("Patient seen, BP well-controlled"));
    }

    [Test]
    public async Task RecallGrain_Cancels()
    {
        string entryId = $"SD-RECALL:{Guid.NewGuid()}";
        await CreateTestEntryAsync(entryId);
        IPatientRecallGrain grain = GetRecallGrain(entryId);

        await grain.CancelEntryAsync("Patient transferred to another facility", "Admin");

        PatientRecallState state = await grain.GetEntryAsync();
        Assert.That(state.Status, Is.EqualTo("CANCELLED"));
        Assert.That(state.CancellationReason, Is.EqualTo("Patient transferred to another facility"));
    }

    [Test]
    public async Task RecallGrain_MarksOverdue()
    {
        string entryId = $"SD-RECALL:{Guid.NewGuid()}";
        await CreateTestEntryAsync(entryId);
        IPatientRecallGrain grain = GetRecallGrain(entryId);

        await grain.MarkOverdueAsync();

        PatientRecallState state = await grain.GetEntryAsync();
        Assert.That(state.Status, Is.EqualTo("OVERDUE"));
    }

    [Test]
    public async Task RecallIndex_UpdatedOnCreate()
    {
        string entryId = $"SD-RECALL:{Guid.NewGuid()}";
        string patientId = $"PATIENT-{Guid.NewGuid()}";

        await CreateTestEntryAsync(entryId, patientId: patientId);

        IPatientRecallIndexGrain index = GetIndex();
        List<PatientRecallIndexEntry> entries = await index.GetByPatientAsync(patientId);
        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].EntryId, Is.EqualTo(entryId));
        Assert.That(entries[0].Status, Is.EqualTo("PENDING"));
    }

    [Test]
    public async Task RecallIndex_OverdueFilter()
    {
        string overdueId = $"SD-RECALL:{Guid.NewGuid()}";
        string pendingId = $"SD-RECALL:{Guid.NewGuid()}";
        string patientId = $"PATIENT-{Guid.NewGuid()}";

        await CreateTestEntryAsync(overdueId, patientId: patientId);
        await CreateTestEntryAsync(pendingId, patientId: patientId);

        IPatientRecallGrain overdueGrain = GetRecallGrain(overdueId);
        await overdueGrain.MarkOverdueAsync();

        IPatientRecallIndexGrain index = GetIndex();
        List<PatientRecallIndexEntry> overdue = await index.GetOverdueAsync();

        Assert.That(overdue.Any(e => e.EntryId == overdueId), Is.True);
        Assert.That(overdue.Any(e => e.EntryId == pendingId), Is.False);
    }
}
