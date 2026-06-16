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
/// Functional tests for FHIR data mapping — verifies that patient data created
/// via Orleans grains is correctly returned by workflow methods that the FHIR
/// gateway controller would consume.
///
/// Tests cover FHIR resource representations: Patient, Condition, AllergyIntolerance,
/// Observation (vitals), MedicationRequest, Encounter, and Appointment.
/// </summary>
[TestFixture]
public class FhirGatewayWorkflowTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private IPatientWorkflowGrain NewWorkflow()
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>($"PATIENT-{Guid.NewGuid()}");

    private IPatientWorkflowGrain GetWorkflow(string patientId)
        => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    // ─── 1 ── FHIR Patient: Get Patient Returns Name ─────────────────────

    [Test]
    public async Task FhirPatient_GetPatient_ReturnsName()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        await w.UpdateDemographicsAsync("SMITH,JOHN A", "M",
            new DateTime(1960, 5, 15, 0, 0, 0, DateTimeKind.Utc), "123-45-6789");

        PatientState patient = await w.GetPatientAsync();
        Assert.That(patient.Name, Is.EqualTo("SMITH,JOHN A"));
    }

    // ─── 2 ── FHIR Patient: Get Patient Returns Demographics ─────────────

    [Test]
    public async Task FhirPatient_GetPatient_ReturnsDemographics()
    {
        IPatientWorkflowGrain w = NewWorkflow();
        DateTime dob = new DateTime(1955, 3, 22, 0, 0, 0, DateTimeKind.Utc);

        await w.UpdateDemographicsAsync("DOE,JANE B", "F", dob, "987-65-4321");

        PatientState patient = await w.GetPatientAsync();
        Assert.That(patient.Sex, Is.EqualTo("F"));
        Assert.That(patient.DateOfBirth, Is.EqualTo(dob));
        Assert.That(patient.SocialSecurityNumber, Is.EqualTo("987-65-4321"));
    }

    // ─── 3 ── FHIR Patient: Get Patient Returns Address ──────────────────

    [Test]
    public async Task FhirPatient_GetPatient_ReturnsAddress()
    {
        IPatientWorkflowGrain w = NewWorkflow();
        await w.UpdateDemographicsAsync("JONES,ROBERT C", "M", null, null);

        await w.UpdateAddressAsync("123 Main St", null, null, "Anytown", "VA", "22031");

        PatientState patient = await w.GetPatientAsync();
        Assert.That(patient.StreetAddress1, Is.EqualTo("123 Main St"));
        Assert.That(patient.City, Is.EqualTo("Anytown"));
        Assert.That(patient.State, Is.EqualTo("VA"));
        Assert.That(patient.ZipCode, Is.EqualTo("22031"));
    }

    // ─── 4 ── FHIR Patient: Get Patient Returns Veteran Status ──────────

    [Test]
    public async Task FhirPatient_GetPatient_ReturnsVeteranStatus()
    {
        IPatientWorkflowGrain w = NewWorkflow();
        await w.UpdateDemographicsAsync("WILLIAMS,MARK D", "M", null, null);

        await w.UpdateVeteranInfoAsync("Y", 30, "SC", "SC<50%");

        PatientState patient = await w.GetPatientAsync();
        Assert.That(patient.Veteran, Is.EqualTo("Y"));
        Assert.That(patient.ServiceConnectedPercentage, Is.EqualTo(30));
    }

    // ─── 5 ── FHIR Condition: Get Active Problems Returns List ──────────

    [Test]
    public async Task FhirCondition_GetActiveProblems_ReturnsList()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        await w.AddProblemAsync("Essential hypertension", "I10", null, null,
            null, null, null, null, null, false, null);
        await w.AddProblemAsync("Type 2 diabetes mellitus", "E11.9", null, null,
            null, null, null, null, null, false, null);

        List<ProblemSummary> problems = await w.GetActiveProblemsAsync();
        Assert.That(problems, Has.Count.EqualTo(2));
    }

    // ─── 6 ── FHIR Condition: Get Active Problems Includes Diagnosis Code ─

    [Test]
    public async Task FhirCondition_GetActiveProblems_IncludesDiagnosisCode()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        await w.AddProblemAsync("Essential hypertension", "I10", null, null,
            null, null, null, null, null, false, null);

        List<ProblemSummary> problems = await w.GetActiveProblemsAsync();
        Assert.That(problems, Has.Count.EqualTo(1));
        Assert.That(problems[0].DiagnosisCode, Is.EqualTo("I10"));
    }

    // ─── 7 ── FHIR Allergy: Get Allergies Returns List ──────────────────

    [Test]
    public async Task FhirAllergy_GetAllergies_ReturnsList()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        await w.RecordAllergyAsync("Penicillin", "Drug", null, "O",
            new List<string> { "Rash" }, "Moderate", null, null, null);
        await w.RecordAllergyAsync("Sulfa", "Drug", null, "H",
            new List<string> { "Itching" }, "Mild", null, null, null);

        List<AllergySummary> allergies = await w.GetAllergiesAsync();
        Assert.That(allergies, Has.Count.EqualTo(2));
    }

    // ─── 8 ── FHIR Allergy: Get Allergies Includes Reactions ─────────────

    [Test]
    public async Task FhirAllergy_GetAllergies_IncludesReactions()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        await w.RecordAllergyAsync("Aspirin", "Drug", null, "O",
            new List<string> { "Hives", "Angioedema" }, "Severe", null, null, null);

        List<AllergySummary> allergies = await w.GetAllergiesAsync();
        Assert.That(allergies, Has.Count.EqualTo(1));
        Assert.That(allergies[0].Reactions, Has.Count.EqualTo(2));
        Assert.That(allergies[0].Reactions, Contains.Item("Hives"));
        Assert.That(allergies[0].Reactions, Contains.Item("Angioedema"));
    }

    // ─── 9 ── FHIR Vitals: Get Latest Vitals Returns List ───────────────

    [Test]
    public async Task FhirVitals_GetLatestVitals_ReturnsList()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        await w.RecordVitalsAsync(null, null, null, null, DateTime.UtcNow,
            new Dictionary<string, string>
            {
                ["TEMPERATURE"] = "98.6",
                ["PULSE"] = "72",
                ["BLOOD PRESSURE"] = "120/80"
            }, null);

        List<VitalSummary> vitals = await w.GetLatestVitalsAsync();
        Assert.That(vitals, Has.Count.EqualTo(3));
    }

    // ─── 10 ── FHIR Vitals: Get Latest Vitals Returns Correct Values ────

    [Test]
    public async Task FhirVitals_GetLatestVitals_ReturnsCorrectValues()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        await w.RecordVitalsAsync(null, null, null, null, DateTime.UtcNow,
            new Dictionary<string, string>
            {
                ["BLOOD PRESSURE"] = "120/80"
            }, null);

        List<VitalSummary> vitals = await w.GetLatestVitalsAsync();
        VitalSummary? bp = vitals.FirstOrDefault(v => v.VitalType == "BLOOD PRESSURE");
        Assert.That(bp, Is.Not.Null);
        Assert.That(bp!.Value, Is.EqualTo("120/80"));
    }

    // ─── 11 ── FHIR Meds: Get Active Meds Returns List (empty) ──────────

    [Test]
    public async Task FhirMeds_GetActiveMeds_ReturnsEmptyForNewPatient()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        List<MedicationSummary> meds = await w.GetActiveMedicationsAsync();

        Assert.That(meds, Is.Empty);
    }

    // ─── 12 ── FHIR Encounter: Get Encounter List Returns List ──────────

    [Test]
    public async Task FhirEncounter_GetEncounterList_ReturnsList()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        await w.CreateEncounterAsync(DateTime.UtcNow, "A",
            "LOC-001", "Primary Care", "ESTABLISHED", "323",
            "PROV-001", "Dr. Jones", null, "Routine visit");

        List<PceVisitEntry> encounters = await w.GetEncounterListAsync(10);
        Assert.That(encounters, Has.Count.EqualTo(1));
    }

    // ─── 13 ── FHIR Encounter: Get Encounter List Includes Status ───────

    [Test]
    public async Task FhirEncounter_GetEncounterList_IncludesStatus()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        await w.CreateEncounterAsync(DateTime.UtcNow, "A",
            null, null, null, null, null, null, null, null);

        List<PceVisitEntry> encounters = await w.GetEncounterListAsync(10);
        Assert.That(encounters, Has.Count.EqualTo(1));
        Assert.That(encounters[0].Status, Is.EqualTo("OPEN"));
    }

    // ─── 14 ── FHIR Appointment: Get All Appointments Returns List ──────

    [Test]
    public async Task FhirAppointment_GetAllAppointments_ReturnsList()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        await w.ScheduleAppointmentAsync("SD-CLINIC-001", "PRIMARY CARE",
            DateTime.UtcNow.AddDays(7), 30, null, null, "Annual check-up", "REGULAR");

        List<AppointmentEntry> appointments = await w.GetAllAppointmentsAsync();
        Assert.That(appointments, Has.Count.EqualTo(1));
    }

    // ─── 15 ── FHIR Appointment: Get All Appointments Includes Clinic ───

    [Test]
    public async Task FhirAppointment_GetAllAppointments_IncludesClinic()
    {
        IPatientWorkflowGrain w = NewWorkflow();

        await w.ScheduleAppointmentAsync("SD-CLINIC-002", "CARDIOLOGY",
            DateTime.UtcNow.AddDays(14), 45, null, null, "Follow-up", "REGULAR");

        List<AppointmentEntry> appointments = await w.GetAllAppointmentsAsync();
        Assert.That(appointments, Has.Count.EqualTo(1));
        Assert.That(appointments[0].ClinicName, Is.EqualTo("CARDIOLOGY"));
    }

    // ─── 16 ── FHIR Patient: Search Patients Finds By Name ──────────────

    [Test]
    public async Task FhirPatient_SearchPatients_FindsByName()
    {
        string patientId = $"PATIENT-{Guid.NewGuid()}";
        IPatientWorkflowGrain w = GetWorkflow(patientId);

        await w.SetDfnAsync($"DFN-{Guid.NewGuid()}");
        await w.UpdateDemographicsAsync("ZZTESTFHIR,UNIQUE Q", "M",
            new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc), "000-00-9999");

        List<PatientIndexEntry> results = await w.SearchPatientsAsync("ZZTESTFHIR", 10);
        Assert.That(results, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(results.Any(r => r.PatientId == patientId), Is.True);
    }
}
