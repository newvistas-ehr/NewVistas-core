// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.TestingHost;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Events.Clinical.Adt;
using NewVistas.Abstractions.Events.Clinical.Consults;
using NewVistas.Abstractions.Events.Clinical.Scheduling;
using NewVistas.Abstractions.Events.Clinical.Vitals;
using NewVistas.Abstractions.EventSourcing;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Event-sourcing tests for the remaining domains: VITALS (single record),
/// CONSULTS (request → complete), SCHEDULING (schedule → check-in/out, cancel),
/// and ADT (admission → transfer → discharge with length-of-stay computed at
/// discharge). Each domain emits causal envelopes into the patient stream and
/// replay reproduces the live state.
/// </summary>
[TestFixture]
public class RemainingDomainsEventSourcingTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IPatientClinicalEventStreamGrain Stream(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientClinicalEventStreamGrain>(patientId);

    // ── VITALS ────────────────────────────────────────────────────────────

    private IVitalGrain Vital(string vitalId) =>
        _cluster.GrainFactory.GetGrain<IVitalGrain>(vitalId);

    [Test]
    public async Task RecordVital_EmitsVitalRecordedV1()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        string vitalId = $"VITAL-{Guid.NewGuid()}";

        await Vital(vitalId).RecordVitalAsync(
            patientId, "BLOOD PRESSURE", "120/80", "mmHg",
            DateTime.UtcNow, "LOC-1", "Clinic A",
            "USR-1", "Smith,Jane",
            new List<string> { "SITTING" }, "calm");
        await WaitForStreamVersionAsync(patientId, expected: 1);

        IReadOnlyList<EventEnvelope> events = await Stream(patientId).ReadAsync(0, 10);
        Assert.That(events, Has.Count.EqualTo(1));
        Assert.That(events[0].EventType, Is.EqualTo(nameof(VitalRecordedV1)));
        var payload = events[0].Payload as VitalRecordedV1;
        Assert.That(payload, Is.Not.Null);
        Assert.That(payload!.Snapshot.Value, Is.EqualTo("120/80"));

        Assert.That(await Stream(patientId).VerifyChainAsync(), Is.True);
    }

    [Test]
    public async Task RecordVital_Idempotent_OnSecondRecord()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        string vitalId = $"VITAL-{Guid.NewGuid()}";

        await Vital(vitalId).RecordVitalAsync(
            patientId, "PULSE", "72", "bpm", DateTime.UtcNow,
            null, null, null, null, null, null);
        await WaitForStreamVersionAsync(patientId, expected: 1);

        await Vital(vitalId).RecordVitalAsync(
            "OTHER-PAT", "PULSE", "999", "bpm", DateTime.UtcNow,
            null, null, null, null, null, null);
        await Task.Delay(150);

        Assert.That(await Stream(patientId).GetVersionAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task ReplayUntilAsync_Vital_RebuildsLiveState()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        string vitalId = $"VITAL-{Guid.NewGuid()}";
        await Vital(vitalId).RecordVitalAsync(
            patientId, "TEMPERATURE", "98.6", "F", DateTime.UtcNow,
            null, null, null, null, null, null);
        await WaitForStreamVersionAsync(patientId, expected: 1);

        PatientStateSnapshot replayed =
            await Stream(patientId).ReplayUntilAsync(DateTime.UtcNow);
        Assert.That(replayed.Vitals, Has.Count.EqualTo(1));
        Assert.That(replayed.Vitals[0].Value, Is.EqualTo("98.6"));
    }

    // ── CONSULTS ──────────────────────────────────────────────────────────

    private IConsultGrain Consult(string consultId) =>
        _cluster.GrainFactory.GetGrain<IConsultGrain>(consultId);

    private async Task<(string patientId, string consultId)> RequestConsultAsync()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        string consultId = $"CONSULT-{Guid.NewGuid()}";
        await Consult(consultId).RequestConsultAsync(
            patientId, "CARDIOLOGY", "SVC-1",
            "PRIMARY CARE", "SVC-2", "URGENT",
            "PROV-1", "Smith,Jane",
            "PROV-2", "Brown,Bob",
            "Persistent chest pain", "Possible angina",
            "ORDER-1", "LOC-1", "Clinic A");
        await WaitForStreamVersionAsync(patientId, expected: 1);
        return (patientId, consultId);
    }

    [Test]
    public async Task RequestConsult_EmitsConsultRequestedV1()
    {
        var (patientId, consultId) = await RequestConsultAsync();

        IReadOnlyList<EventEnvelope> events = await Stream(patientId).ReadAsync(0, 10);
        Assert.That(events[0].EventType, Is.EqualTo(nameof(ConsultRequestedV1)));
        var payload = events[0].Payload as ConsultRequestedV1;
        Assert.That(payload!.ConsultId, Is.EqualTo(consultId));
        Assert.That(payload.Snapshot.ToService, Is.EqualTo("CARDIOLOGY"));
        Assert.That(payload.Snapshot.Status, Is.EqualTo("PENDING"));
    }

    [Test]
    public async Task CompleteConsult_EmitsConsultCompletedV1()
    {
        var (patientId, consultId) = await RequestConsultAsync();

        DateTime completed = DateTime.UtcNow;
        await Consult(consultId).CompleteAsync(completed, "TIU-DOC-1");
        await WaitForStreamVersionAsync(patientId, expected: 2);

        IReadOnlyList<EventEnvelope> events = await Stream(patientId).ReadAsync(0, 10);
        Assert.That(events[1].EventType, Is.EqualTo(nameof(ConsultCompletedV1)));
        var payload = events[1].Payload as ConsultCompletedV1;
        Assert.That(payload!.ResultDocumentId, Is.EqualTo("TIU-DOC-1"));

        ConsultState live = await Consult(consultId).GetConsultAsync();
        Assert.That(live.Status, Is.EqualTo("COMPLETE"));
        Assert.That(await Stream(patientId).VerifyChainAsync(), Is.True);
    }

    [Test]
    public async Task ReplayUntilAsync_Consult_FullLifecycle()
    {
        var (patientId, consultId) = await RequestConsultAsync();
        await Consult(consultId).CompleteAsync(DateTime.UtcNow, "TIU-1");
        await WaitForStreamVersionAsync(patientId, expected: 2);

        PatientStateSnapshot replayed =
            await Stream(patientId).ReplayUntilAsync(DateTime.UtcNow);
        Assert.That(replayed.Consults, Has.Count.EqualTo(1));
        Assert.That(replayed.Consults[0].Status, Is.EqualTo("COMPLETE"));
        Assert.That(replayed.Consults[0].ResultDocumentId, Is.EqualTo("TIU-1"));
    }

    // ── SCHEDULING ────────────────────────────────────────────────────────

    private IAppointmentGrain Appt(string apptId) =>
        _cluster.GrainFactory.GetGrain<IAppointmentGrain>(apptId);

    private async Task<(string patientId, string apptId)> ScheduleApptAsync()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        string apptId = $"APPT-{Guid.NewGuid()}";
        await Appt(apptId).ScheduleAppointmentAsync(
            patientId, "CLINIC-1", "Primary Care",
            DateTime.UtcNow.AddDays(1), 30,
            "PROV-1", "Smith,Jane",
            "Annual physical", "ROUTINE",
            "USR-1", isDoubleBook: false);
        await WaitForStreamVersionAsync(patientId, expected: 1);
        return (patientId, apptId);
    }

    [Test]
    public async Task ScheduleAppointment_EmitsScheduledV1()
    {
        var (patientId, apptId) = await ScheduleApptAsync();

        IReadOnlyList<EventEnvelope> events = await Stream(patientId).ReadAsync(0, 10);
        Assert.That(events[0].EventType, Is.EqualTo(nameof(AppointmentScheduledV1)));
        var payload = events[0].Payload as AppointmentScheduledV1;
        Assert.That(payload!.AppointmentId, Is.EqualTo(apptId));
        Assert.That(payload.Snapshot.ClinicId, Is.EqualTo("CLINIC-1"));
    }

    [Test]
    public async Task CheckInAndOut_EmitTwoEvents()
    {
        var (patientId, apptId) = await ScheduleApptAsync();

        await Appt(apptId).CheckInAsync(DateTime.UtcNow);
        await Appt(apptId).CheckOutAsync(DateTime.UtcNow);
        await WaitForStreamVersionAsync(patientId, expected: 3);

        IReadOnlyList<EventEnvelope> events = await Stream(patientId).ReadAsync(0, 10);
        Assert.That(events[1].EventType, Is.EqualTo(nameof(AppointmentCheckedInV1)));
        Assert.That(events[2].EventType, Is.EqualTo(nameof(AppointmentCheckedOutV1)));

        AppointmentState live = await Appt(apptId).GetAppointmentAsync();
        Assert.That(live.Status, Is.EqualTo("Checked Out"));
        Assert.That(await Stream(patientId).VerifyChainAsync(), Is.True);
    }

    [Test]
    public async Task CancelAppointment_EmitsCancelledV1()
    {
        var (patientId, apptId) = await ScheduleApptAsync();

        await Appt(apptId).CancelAppointmentAsync("Patient called", "USR-2");
        await WaitForStreamVersionAsync(patientId, expected: 2);

        IReadOnlyList<EventEnvelope> events = await Stream(patientId).ReadAsync(0, 10);
        var payload = events[1].Payload as AppointmentCancelledV1;
        Assert.That(payload!.CancellationReason, Is.EqualTo("Patient called"));
    }

    [Test]
    public async Task ReplayUntilAsync_Appointment_FullCycle()
    {
        var (patientId, apptId) = await ScheduleApptAsync();
        await Appt(apptId).CheckInAsync(DateTime.UtcNow);
        await Appt(apptId).CheckOutAsync(DateTime.UtcNow);
        await WaitForStreamVersionAsync(patientId, expected: 3);

        PatientStateSnapshot replayed =
            await Stream(patientId).ReplayUntilAsync(DateTime.UtcNow);
        Assert.That(replayed.Appointments, Has.Count.EqualTo(1));
        Assert.That(replayed.Appointments[0].Status, Is.EqualTo("Checked Out"));
        Assert.That(replayed.Appointments[0].CheckInDateTime, Is.Not.Null);
    }

    // ── ADT ───────────────────────────────────────────────────────────────

    private IAdtGrain Adt(string movementId) =>
        _cluster.GrainFactory.GetGrain<IAdtGrain>(movementId);

    [Test]
    public async Task RecordAdmission_EmitsAdmissionRecordedV1()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        string movementId = $"ADT-{Guid.NewGuid()}";
        await Adt(movementId).RecordAdmissionAsync(
            patientId, DateTime.UtcNow,
            "WARD-3W", "3-WEST", "302-A",
            "SVC-MED", "Internal Medicine",
            "PROV-1", "Smith,Jane",
            "INPATIENT", "Pneumonia, community-acquired",
            null);
        await WaitForStreamVersionAsync(patientId, expected: 1);

        IReadOnlyList<EventEnvelope> events = await Stream(patientId).ReadAsync(0, 10);
        Assert.That(events[0].EventType, Is.EqualTo(nameof(AdmissionRecordedV1)));
        var payload = events[0].Payload as AdmissionRecordedV1;
        Assert.That(payload!.Snapshot.AdmissionDiagnosis, Does.Contain("Pneumonia"));
        Assert.That(payload.Snapshot.TransactionType, Is.EqualTo("ADMISSION"));
    }

    [Test]
    public async Task RecordDischarge_EmitsDischargeV1_WithLengthOfStay()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        string movementId = $"ADT-{Guid.NewGuid()}";

        DateTime admitted = DateTime.UtcNow.AddDays(-3);
        await Adt(movementId).RecordAdmissionAsync(
            patientId, admitted,
            "WARD-3W", "3-WEST", null, null, null, null, null,
            "INPATIENT", "Asthma exacerbation", null);

        DateTime discharged = DateTime.UtcNow;
        await Adt(movementId).RecordDischargeAsync(
            discharged, "Asthma resolved", "REGULAR", "Discharge to home");
        await WaitForStreamVersionAsync(patientId, expected: 2);

        IReadOnlyList<EventEnvelope> events = await Stream(patientId).ReadAsync(0, 10);
        Assert.That(events[1].EventType, Is.EqualTo(nameof(DischargeRecordedV1)));
        var payload = events[1].Payload as DischargeRecordedV1;
        Assert.That(payload!.LengthOfStay, Is.EqualTo(3));
        Assert.That(payload.Disposition, Is.EqualTo("REGULAR"));

        AdtState live = await Adt(movementId).GetMovementAsync();
        Assert.That(live.LengthOfStay, Is.EqualTo(3));
    }

    [Test]
    public async Task RecordTransfer_EmitsTransferRecordedV1()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        string transferId = $"ADT-{Guid.NewGuid()}";

        DateTime admitted = DateTime.UtcNow.AddDays(-2);
        DateTime transferred = DateTime.UtcNow;

        await Adt(transferId).RecordAsTransferAsync(
            patientId, admitted, transferred,
            "WARD-ICU", "ICU", "ICU-7",
            "SVC-CCU", "Critical Care",
            "PROV-2", "Other,Doc",
            "Respiratory failure");
        await WaitForStreamVersionAsync(patientId, expected: 1);

        IReadOnlyList<EventEnvelope> events = await Stream(patientId).ReadAsync(0, 10);
        Assert.That(events[0].EventType, Is.EqualTo(nameof(TransferRecordedV1)));
        var payload = events[0].Payload as TransferRecordedV1;
        Assert.That(payload!.Snapshot.TransactionType, Is.EqualTo("TRANSFER"));
        Assert.That(payload.Snapshot.WardLocationId, Is.EqualTo("WARD-ICU"));
    }

    [Test]
    public async Task ReplayUntilAsync_AdtMovements_AdmissionAndTransferOnSamePatient()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        string admitId = $"ADT-{Guid.NewGuid()}";
        string transferId = $"ADT-{Guid.NewGuid()}";

        await Adt(admitId).RecordAdmissionAsync(
            patientId, DateTime.UtcNow.AddDays(-1),
            "WARD-3W", "3-WEST", null, null, null, null, null,
            "INPATIENT", "MI", null);
        await Adt(transferId).RecordAsTransferAsync(
            patientId, DateTime.UtcNow.AddDays(-1), DateTime.UtcNow,
            "WARD-ICU", "ICU", null, null, null, null, null, null);
        await WaitForStreamVersionAsync(patientId, expected: 2);

        PatientStateSnapshot replayed =
            await Stream(patientId).ReplayUntilAsync(DateTime.UtcNow);
        Assert.That(replayed.AdtMovements, Has.Count.EqualTo(2));
        Assert.That(replayed.AdtMovements.Any(m => m.MovementId == admitId), Is.True);
        Assert.That(replayed.AdtMovements.Any(m => m.MovementId == transferId), Is.True);
    }

    private async Task WaitForStreamVersionAsync(
        string patientId, int expected, int timeoutMs = 5000)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        IPatientClinicalEventStreamGrain stream = Stream(patientId);
        while (DateTime.UtcNow < deadline)
        {
            int v = await stream.GetVersionAsync();
            if (v >= expected) return;
            await Task.Delay(50);
        }
        int finalVersion = await stream.GetVersionAsync();
        Assert.Fail(
            $"Stream for {patientId} did not reach version {expected} within {timeoutMs}ms (current={finalVersion}).");
    }
}
