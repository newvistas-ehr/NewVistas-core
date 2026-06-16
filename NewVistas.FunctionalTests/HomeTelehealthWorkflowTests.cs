// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Functional tests for Home Telehealth / Remote Patient Monitoring — VistA Files #720-720.9.
/// Tests end-to-end workflows via <see cref="IPatientWorkflowGrain"/>.
/// </summary>
[TestFixture]
public class HomeTelehealthWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain Workflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ── Enrollment ────────────────────────────────────────────────────────────

    [Test]
    public async Task EnrollPatient_SetsIsEnrolled()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.EnrollInHomeTelehealthAsync(
            "CC-001", "Nurse Adams",
            "PCP-001", "Dr. Primary",
            HtCareProtocol.Hypertension,
            "New enrollment for BP monitoring");

        HomeTelehealthPatientState state = await wf.GetHtPatientAsync();

        Assert.That(state.IsEnrolled, Is.True);
        Assert.That(state.CareCoordinatorId, Is.EqualTo("CC-001"));
        Assert.That(state.CareCoordinatorName, Is.EqualTo("Nurse Adams"));
        Assert.That(state.PrimaryCareProviderName, Is.EqualTo("Dr. Primary"));
        Assert.That(state.Protocol, Is.EqualTo(HtCareProtocol.Hypertension));
        Assert.That(state.EnrollmentDate, Is.Not.Null);
    }

    [Test]
    public async Task DisenrollPatient_ClearsIsEnrolled()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.EnrollInHomeTelehealthAsync(
            null, null, null, null,
            HtCareProtocol.Diabetes, null);

        await wf.DisenrollFromHomeTelehealthAsync("Patient request — no longer needs monitoring");

        HomeTelehealthPatientState state = await wf.GetHtPatientAsync();
        Assert.That(state.IsEnrolled, Is.False);
        Assert.That(state.DisenrollmentDate, Is.Not.Null);
        Assert.That(state.DisenrollmentReason, Does.Contain("Patient request"));
    }

    // ── Device management ─────────────────────────────────────────────────────

    [Test]
    public async Task AssignDevice_AppearsInPatientDeviceList()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.EnrollInHomeTelehealthAsync(
            null, null, null, null,
            HtCareProtocol.CongestiveHeartFailure, null);

        string deviceId = $"DEV-{Guid.NewGuid()}";
        await wf.AssignHtDeviceAsync(deviceId, "A&D UA-651BLE BP Monitor", HtDeviceType.BloodPressureMonitor);

        HomeTelehealthPatientState state = await wf.GetHtPatientAsync();
        Assert.That(state.AssignedDevices, Has.Count.EqualTo(1));
        Assert.That(state.AssignedDevices[0].DeviceId, Is.EqualTo(deviceId));
        Assert.That(state.AssignedDevices[0].DeviceType, Is.EqualTo(HtDeviceType.BloodPressureMonitor));
        Assert.That(state.AssignedDevices[0].ReturnedDate, Is.Null);
    }

    [Test]
    public async Task ReturnDevice_MarksDeviceReturned()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.EnrollInHomeTelehealthAsync(
            null, null, null, null,
            HtCareProtocol.Standard, null);

        string deviceId = $"DEV-{Guid.NewGuid()}";
        await wf.AssignHtDeviceAsync(deviceId, "Withings Scale", HtDeviceType.Scale);
        await wf.ReturnHtDeviceAsync(deviceId);

        HomeTelehealthPatientState state = await wf.GetHtPatientAsync();
        HtAssignedDevice? returned = state.AssignedDevices.Find(d => d.DeviceId == deviceId);
        Assert.That(returned, Is.Not.Null);
        Assert.That(returned!.ReturnedDate, Is.Not.Null);
    }

    // ── Thresholds ────────────────────────────────────────────────────────────

    [Test]
    public async Task SetAlertThresholds_PersistsOnPatient()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.EnrollInHomeTelehealthAsync(
            null, null, null, null,
            HtCareProtocol.Hypertension, null);

        List<HtAlertThreshold> thresholds = new()
        {
            new HtAlertThreshold
            {
                MeasurementType = HtMeasurementType.BloodPressure,
                LowValue = 90m, HighValue = 160m,
                LowValue2 = 60m, HighValue2 = 100m
            },
            new HtAlertThreshold
            {
                MeasurementType = HtMeasurementType.Weight,
                LowValue = 120m, HighValue = 250m
            }
        };

        await wf.SetHtAlertThresholdsAsync(thresholds);

        HomeTelehealthPatientState state = await wf.GetHtPatientAsync();
        Assert.That(state.AlertThresholds, Has.Count.EqualTo(2));
        Assert.That(state.AlertThresholds[0].MeasurementType, Is.EqualTo(HtMeasurementType.BloodPressure));
        Assert.That(state.AlertThresholds[0].HighValue, Is.EqualTo(160m));
    }

    // ── Readings ──────────────────────────────────────────────────────────────

    [Test]
    public async Task RecordReading_AppearsInReadingIndex()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.EnrollInHomeTelehealthAsync(
            null, null, null, null,
            HtCareProtocol.Standard, null);

        string readingId = await wf.RecordHtReadingAsync(
            HtMeasurementType.Weight,
            185.5m, null, "lbs",
            DateTime.UtcNow,
            HtReadingSource.PatientEntry,
            null, "Morning weight");

        Assert.That(readingId, Does.StartWith("HT-READING-"));

        List<HtReadingIndexEntry> readings = await wf.GetHtReadingsAsync(null, null, 10);
        Assert.That(readings, Has.Count.EqualTo(1));
        Assert.That(readings[0].ReadingId, Is.EqualTo(readingId));
        Assert.That(readings[0].MeasurementType, Is.EqualTo(HtMeasurementType.Weight));
        Assert.That(readings[0].Value1, Is.EqualTo(185.5m));
    }

    [Test]
    public async Task RecordReading_WithinThreshold_NoAlertGenerated()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.EnrollInHomeTelehealthAsync(
            null, null, null, null,
            HtCareProtocol.Hypertension, null);

        await wf.SetHtAlertThresholdsAsync(new List<HtAlertThreshold>
        {
            new HtAlertThreshold
            {
                MeasurementType = HtMeasurementType.BloodPressure,
                LowValue = 90m, HighValue = 160m,
                LowValue2 = 60m, HighValue2 = 100m
            }
        });

        await wf.RecordHtReadingAsync(
            HtMeasurementType.BloodPressure,
            120m, 80m, "mmHg",
            DateTime.UtcNow,
            HtReadingSource.DeviceTransmission,
            null, null);

        List<HtAlertIndexEntry> alerts = await wf.GetHtAlertsAsync(null);
        Assert.That(alerts, Has.Count.EqualTo(0));
    }

    [Test]
    public async Task RecordReading_OutOfRange_GeneratesAlert()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.EnrollInHomeTelehealthAsync(
            null, null, null, null,
            HtCareProtocol.Hypertension, null);

        await wf.SetHtAlertThresholdsAsync(new List<HtAlertThreshold>
        {
            new HtAlertThreshold
            {
                MeasurementType = HtMeasurementType.BloodPressure,
                LowValue = 90m, HighValue = 160m,
                LowValue2 = 60m, HighValue2 = 100m
            }
        });

        await wf.RecordHtReadingAsync(
            HtMeasurementType.BloodPressure,
            180m, 110m, "mmHg",
            DateTime.UtcNow,
            HtReadingSource.DeviceTransmission,
            null, "Patient reports headache");

        List<HtAlertIndexEntry> alerts = await wf.GetHtAlertsAsync(null);
        Assert.That(alerts, Has.Count.EqualTo(1));
        Assert.That(alerts[0].MeasurementType, Is.EqualTo(HtMeasurementType.BloodPressure));
        Assert.That(alerts[0].Status, Is.EqualTo(HtAlertStatus.Active));
    }

    // ── Reading review ────────────────────────────────────────────────────────

    [Test]
    public async Task ReviewReading_MarksAsReviewed()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.EnrollInHomeTelehealthAsync(
            null, null, null, null,
            HtCareProtocol.Diabetes, null);

        string readingId = await wf.RecordHtReadingAsync(
            HtMeasurementType.BloodGlucose,
            145m, null, "mg/dL",
            DateTime.UtcNow,
            HtReadingSource.PatientEntry,
            null, null);

        await wf.ReviewHtReadingAsync(readingId, "CLIN-001", "Dr. Reviewer");

        List<HtReadingIndexEntry> readings = await wf.GetHtReadingsAsync(null, null, 10);
        Assert.That(readings[0].IsReviewed, Is.True);
    }

    // ── Alert workflows ───────────────────────────────────────────────────────

    [Test]
    public async Task AcknowledgeAlert_UpdatesStatus()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.EnrollInHomeTelehealthAsync(
            null, null, null, null,
            HtCareProtocol.CongestiveHeartFailure, null);

        await wf.SetHtAlertThresholdsAsync(new List<HtAlertThreshold>
        {
            new HtAlertThreshold
            {
                MeasurementType = HtMeasurementType.Weight,
                LowValue = 150m, HighValue = 200m
            }
        });

        await wf.RecordHtReadingAsync(
            HtMeasurementType.Weight,
            215m, null, "lbs",
            DateTime.UtcNow,
            HtReadingSource.DeviceTransmission,
            null, null);

        List<HtAlertIndexEntry> alerts = await wf.GetHtAlertsAsync(HtAlertStatus.Active);
        Assert.That(alerts, Has.Count.EqualTo(1));

        string alertId = alerts[0].AlertId;
        await wf.AcknowledgeHtAlertAsync(alertId, "CLIN-002", "Dr. Cardio", "Patient advised to restrict fluids");

        List<HtAlertIndexEntry> acknowledged = await wf.GetHtAlertsAsync(HtAlertStatus.Acknowledged);
        Assert.That(acknowledged, Has.Count.EqualTo(1));
        Assert.That(acknowledged[0].AlertId, Is.EqualTo(alertId));
    }

    [Test]
    public async Task ResolveAlert_UpdatesStatus()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.EnrollInHomeTelehealthAsync(
            null, null, null, null,
            HtCareProtocol.COPD, null);

        await wf.SetHtAlertThresholdsAsync(new List<HtAlertThreshold>
        {
            new HtAlertThreshold
            {
                MeasurementType = HtMeasurementType.PulseOximetry,
                LowValue = 90m, HighValue = 100m
            }
        });

        await wf.RecordHtReadingAsync(
            HtMeasurementType.PulseOximetry,
            85m, null, "%",
            DateTime.UtcNow,
            HtReadingSource.DeviceTransmission,
            null, null);

        List<HtAlertIndexEntry> alerts = await wf.GetHtAlertsAsync(HtAlertStatus.Active);
        string alertId = alerts[0].AlertId;

        await wf.ResolveHtAlertAsync(alertId, "CLIN-003", "Dr. Pulmo", "Supplemental O2 titrated");

        List<HtAlertIndexEntry> resolved = await wf.GetHtAlertsAsync(HtAlertStatus.Resolved);
        Assert.That(resolved, Has.Count.EqualTo(1));
        Assert.That(resolved[0].AlertId, Is.EqualTo(alertId));
    }

    [Test]
    public async Task DismissAlert_UpdatesStatus()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.EnrollInHomeTelehealthAsync(
            null, null, null, null,
            HtCareProtocol.Standard, null);

        await wf.SetHtAlertThresholdsAsync(new List<HtAlertThreshold>
        {
            new HtAlertThreshold
            {
                MeasurementType = HtMeasurementType.Temperature,
                LowValue = 96m, HighValue = 100m
            }
        });

        await wf.RecordHtReadingAsync(
            HtMeasurementType.Temperature,
            101.5m, null, "F",
            DateTime.UtcNow,
            HtReadingSource.PatientEntry,
            null, null);

        List<HtAlertIndexEntry> alerts = await wf.GetHtAlertsAsync(HtAlertStatus.Active);
        string alertId = alerts[0].AlertId;

        await wf.DismissHtAlertAsync(alertId, "CLIN-004", "Nurse Triage", "Known post-vaccination fever, non-actionable");

        List<HtAlertIndexEntry> dismissed = await wf.GetHtAlertsAsync(HtAlertStatus.Dismissed);
        Assert.That(dismissed, Has.Count.EqualTo(1));
    }

    // ── Reading filter by type ────────────────────────────────────────────────

    [Test]
    public async Task GetReadings_FiltersByMeasurementType()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf = Workflow(patientId);

        await wf.EnrollInHomeTelehealthAsync(
            null, null, null, null,
            HtCareProtocol.Diabetes, null);

        await wf.RecordHtReadingAsync(
            HtMeasurementType.BloodGlucose, 110m, null, "mg/dL",
            DateTime.UtcNow, HtReadingSource.PatientEntry, null, null);

        await wf.RecordHtReadingAsync(
            HtMeasurementType.Weight, 180m, null, "lbs",
            DateTime.UtcNow, HtReadingSource.PatientEntry, null, null);

        await wf.RecordHtReadingAsync(
            HtMeasurementType.BloodGlucose, 130m, null, "mg/dL",
            DateTime.UtcNow, HtReadingSource.PatientEntry, null, null);

        List<HtReadingIndexEntry> glucoseOnly = await wf.GetHtReadingsAsync(
            HtMeasurementType.BloodGlucose, null, 10);
        Assert.That(glucoseOnly, Has.Count.EqualTo(2));
        Assert.That(glucoseOnly.TrueForAll(r => r.MeasurementType == HtMeasurementType.BloodGlucose), Is.True);
    }

    // ── Independent patients ──────────────────────────────────────────────────

    [Test]
    public async Task DifferentPatients_HaveIndependentData()
    {
        string p1 = $"PATIENT-{Guid.NewGuid()}";
        string p2 = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain wf1 = Workflow(p1);
        IPatientWorkflowGrain wf2 = Workflow(p2);

        await wf1.EnrollInHomeTelehealthAsync(
            null, null, null, null,
            HtCareProtocol.Hypertension, null);

        await wf2.EnrollInHomeTelehealthAsync(
            null, null, null, null,
            HtCareProtocol.Diabetes, null);

        await wf1.RecordHtReadingAsync(
            HtMeasurementType.BloodPressure, 130m, 85m, "mmHg",
            DateTime.UtcNow, HtReadingSource.DeviceTransmission, null, null);

        await wf2.RecordHtReadingAsync(
            HtMeasurementType.BloodGlucose, 120m, null, "mg/dL",
            DateTime.UtcNow, HtReadingSource.PatientEntry, null, null);
        await wf2.RecordHtReadingAsync(
            HtMeasurementType.BloodGlucose, 140m, null, "mg/dL",
            DateTime.UtcNow, HtReadingSource.PatientEntry, null, null);

        List<HtReadingIndexEntry> p1Readings = await wf1.GetHtReadingsAsync(null, null, 10);
        List<HtReadingIndexEntry> p2Readings = await wf2.GetHtReadingsAsync(null, null, 10);

        Assert.That(p1Readings, Has.Count.EqualTo(1));
        Assert.That(p2Readings, Has.Count.EqualTo(2));
    }
}
