// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
﻿using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.FunctionalTests;

[TestFixture]
public class PatientWorkflowFunctionalTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    [Test]
    public async Task Patient_CanHaveCompleteWorkflow_WithAppointmentsAndTests()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        var now = DateTime.UtcNow;

        await patient.UpdateDemographicsAsync("John Doe", "M", new DateTime(1980, 1, 15), "123-45-6789");

        var appointmentId = $"APPT-{Guid.NewGuid()}";
        var appointment = _cluster.GrainFactory.GetGrain<IAppointmentGrain>(appointmentId);
        await appointment.ScheduleAppointmentAsync(
            patientId, "CLINIC-001", "Cardiology Clinic", now.AddDays(7), 30,
            "PROVIDER-123", "Dr. Smith", "Annual Checkup", "ROUTINE", "USER-001");
        await patient.AddAppointmentIdAsync(appointmentId);

        var labTestId = $"LAB-{Guid.NewGuid()}";
        var labTest = _cluster.GrainFactory.GetGrain<ILabTestGrain>(labTestId);
        await labTest.OrderLabTestAsync(patientId, "60-1", "WBC", "WBC", null,
            "PROVIDER-123", "Dr. Smith", "Blood", "HEMATOLOGY");
        await patient.AddLabTestIdAsync(labTestId);

        var allergyEntry = new AllergyEntry
        {
            AllergyId = $"ALLERGY-{Guid.NewGuid()}",
            Allergen = "Penicillin",
            AllergenType = "Drug",
            ReactionType = "ALLERGY",
            Reactions = new List<string> { "Rash", "Itching" },
            Severity = "Moderate",
            ReactionDateTime = now,
            ObservedHistorical = "O",
            OriginatorId = "USER-001",
            OriginatorName = "Nurse Jane",
            Comments = "Patient reported reaction during previous visit"
        };
        await patient.AddAllergyAsync(allergyEntry);

        var patientState = await patient.GetPatientAsync();
        Assert.That(patientState.Name, Is.EqualTo("John Doe"));
        Assert.That(patientState.AppointmentIds, Has.Count.EqualTo(1));
        Assert.That(patientState.LabTestIds, Has.Count.EqualTo(1));
        Assert.That(patientState.Allergies, Has.Count.EqualTo(1));

        var appointmentState = await appointment.GetAppointmentAsync();
        Assert.That(appointmentState.PatientId, Is.EqualTo(patientId));
        Assert.That(appointmentState.Status, Is.EqualTo("Scheduled"));

        var labTestState = await labTest.GetLabTestAsync();
        Assert.That(labTestState.PatientId, Is.EqualTo(patientId));
        Assert.That(labTestState.Status, Is.EqualTo("Ordered"));

        List<AllergyEntry> allergyEntries = await patient.GetAllergiesAsync();
        Assert.That(allergyEntries[0].Allergen, Is.EqualTo("Penicillin"));
    }

    [Test]
    public async Task Appointment_CompleteLifecycle_FromScheduleToCompletion()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var appointmentId = $"APPT-{Guid.NewGuid()}";
        var appointment = _cluster.GrainFactory.GetGrain<IAppointmentGrain>(appointmentId);
        var now = DateTime.UtcNow;

        await appointment.ScheduleAppointmentAsync(patientId, "CLINIC-001", "Primary Care",
            now.AddDays(1), 45, "PROVIDER-456", "Dr. Jones", "Follow-up", "FOLLOW_UP", "USER-001");
        var initialState = await appointment.GetAppointmentAsync();

        await appointment.CheckInAsync(now);
        var checkedInState = await appointment.GetAppointmentAsync();

        await appointment.CheckOutAsync(now.AddMinutes(45));
        var checkedOutState = await appointment.GetAppointmentAsync();

        await appointment.CompleteAppointmentAsync();
        var completedState = await appointment.GetAppointmentAsync();

        Assert.That(initialState.Status, Is.EqualTo("Scheduled"));
        Assert.That(checkedInState.Status, Is.EqualTo("Checked In"));
        Assert.That(checkedInState.CheckInDateTime, Is.Not.Null);
        Assert.That(checkedOutState.Status, Is.EqualTo("Checked Out"));
        Assert.That(checkedOutState.CheckOutDateTime, Is.Not.Null);
        Assert.That(completedState.Status, Is.EqualTo("Completed"));
    }

    [Test]
    public async Task LabTest_CompleteWorkflow_FromOrderToVerification()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var labTestId = $"LAB-{Guid.NewGuid()}";
        var labTest = _cluster.GrainFactory.GetGrain<ILabTestGrain>(labTestId);
        var now = DateTime.UtcNow;

        await labTest.OrderLabTestAsync(patientId, "60-1", "WBC", "WBC", null,
            "PROVIDER-123", "Dr. Smith", "Blood", "HEMATOLOGY");
        var orderedState = await labTest.GetLabTestAsync();

        await labTest.CollectSpecimenAsync(now.AddHours(1), "LAVENDER", "Main Lab");
        var collectedState = await labTest.GetLabTestAsync();

        await labTest.RecordResultAsync(now.AddHours(4), "7.5", "K/cmm", "3.4", "8.3", "Normal");
        var resultState = await labTest.GetLabTestAsync();

        await labTest.VerifyResultAsync("PROVIDER-123", "Dr. Smith", now.AddHours(4).AddMinutes(30));
        var verifiedState = await labTest.GetLabTestAsync();

        Assert.That(orderedState.Status, Is.EqualTo("Ordered"));
        Assert.That(collectedState.Status, Is.EqualTo("Collected"));
        Assert.That(collectedState.CollectionSample, Is.EqualTo("LAVENDER"));
        Assert.That(resultState.Status, Is.EqualTo("Pending"));
        Assert.That(resultState.ResultValue, Is.EqualTo("7.5"));
        Assert.That(verifiedState.Status, Is.EqualTo("Completed"));
        Assert.That(verifiedState.VerifyingProviderName, Is.EqualTo("Dr. Smith"));
    }

    [Test]
    public async Task Order_CanBeCreatedAndDiscontinued()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var orderId = $"ORDER-{Guid.NewGuid()}";
        var order = _cluster.GrainFactory.GetGrain<IOrderGrain>(orderId);
        var now = DateTime.UtcNow;

        await order.CreateOrderAsync(patientId, "Pharmacy", "Lisinopril 10mg", "DRUG-001",
            "PROVIDER-123", "Dr. Smith", now, "CLINIC-001", "Primary Care",
            "ROUTINE", "Take once daily", "Hypertension", "NEW", "USER-001");
        var createdState = await order.GetOrderAsync();

        await order.SignOrderAsync("DR_SMITH_SIGNATURE", now.AddMinutes(5));
        var signedState = await order.GetOrderAsync();

        await order.ReleaseOrderAsync(now.AddMinutes(10));
        var releasedState = await order.GetOrderAsync();

        await order.DiscontinueOrderAsync(now.AddDays(1), "Patient switched medications", "PROVIDER-123");
        var discontinuedState = await order.GetOrderAsync();

        Assert.That(createdState.Status, Is.EqualTo("Pending"));
        Assert.That(createdState.OrderType, Is.EqualTo("Pharmacy"));
        Assert.That(signedState.ElectronicSignature, Is.EqualTo("DR_SMITH_SIGNATURE"));
        Assert.That(releasedState.Status, Is.EqualTo("Active"));
        Assert.That(discontinuedState.Status, Is.EqualTo("Discontinued"));
        Assert.That(discontinuedState.DiscontinuedReason, Is.EqualTo("Patient switched medications"));
    }

    [Test]
    public async Task MultiplePatients_CanExistIndependently()
    {
        var patient1Id = $"PATIENT-{Guid.NewGuid()}";
        var patient2Id = $"PATIENT-{Guid.NewGuid()}";
        var patient1 = _cluster.GrainFactory.GetGrain<IPatientGrain>(patient1Id);
        var patient2 = _cluster.GrainFactory.GetGrain<IPatientGrain>(patient2Id);

        await patient1.UpdateDemographicsAsync("Alice Smith", "F", new DateTime(1975, 5, 10), "111-11-1111");
        await patient2.UpdateDemographicsAsync("Bob Johnson", "M", new DateTime(1982, 8, 20), "222-22-2222");

        var state1 = await patient1.GetPatientAsync();
        var state2 = await patient2.GetPatientAsync();

        Assert.That(state1.Name, Is.EqualTo("Alice Smith"));
        Assert.That(state2.Name, Is.EqualTo("Bob Johnson"));
        Assert.That(state1.PatientId, Is.Not.EqualTo(state2.PatientId));
    }

    [Test]
    public async Task Problem_CanBeRecordedAndInactivated()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);

        var problemId = await workflow.AddProblemAsync(
            "Essential Hypertension", "I10", "CHRONIC", "ROUTINE",
            new DateTime(2020, 3, 1), "PROVIDER-123", "Dr. Smith",
            "CLINIC-001", "Primary Care", false, null);

        ProblemEntry? activeEntry = await patient.GetProblemAsync(problemId);
        Assert.That(activeEntry!.Status, Is.EqualTo("ACTIVE"));
        Assert.That(activeEntry.Diagnosis, Is.EqualTo("Essential Hypertension"));
        Assert.That(activeEntry.DiagnosisCode, Is.EqualTo("I10"));
        Assert.That(activeEntry.Condition, Is.EqualTo("CHRONIC"));

        await workflow.InactivateProblemAsync(problemId, DateTime.UtcNow);
        ProblemEntry? inactiveEntry = await patient.GetProblemAsync(problemId);
        Assert.That(inactiveEntry!.Status, Is.EqualTo("INACTIVE"));
        Assert.That(inactiveEntry.DateResolved, Is.Not.Null);
    }

    [Test]
    public async Task Pharmacy_PrescriptionLifecycle()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var rxId = $"RX-{Guid.NewGuid()}";
        var rx = _cluster.GrainFactory.GetGrain<IPharmacyGrain>(rxId);
        var now = DateTime.UtcNow;

        await rx.CreatePrescriptionAsync(patientId, "Lisinopril 10mg", "DRUG-001",
            "10mg", "ORAL", "DAILY", "Take one tablet by mouth daily",
            30, 30, 3, "PROVIDER-123", "Dr. Smith", "PHARM-001", "Main Pharmacy",
            null, null);
        var createdState = await rx.GetPrescriptionAsync();

        await rx.VerifyAsync("RPH-001");
        await rx.FillPrescriptionAsync(now);
        var filledState = await rx.GetPrescriptionAsync();

        await rx.PlaceOnHoldAsync("Patient hospitalized");
        var heldState = await rx.GetPrescriptionAsync();

        await rx.ResumeAsync();
        var resumedState = await rx.GetPrescriptionAsync();

        await rx.RefillAsync(now.AddDays(25));
        var refilledState = await rx.GetPrescriptionAsync();

        await rx.DiscontinueAsync("No longer needed");
        var dcState = await rx.GetPrescriptionAsync();

        Assert.That(createdState.Status, Is.EqualTo("ACTIVE"));
        Assert.That(createdState.DrugName, Is.EqualTo("Lisinopril 10mg"));
        Assert.That(createdState.Refills, Is.EqualTo(3));
        Assert.That(filledState.FillDate, Is.Not.Null);
        Assert.That(heldState.Status, Is.EqualTo("HOLD"));
        Assert.That(resumedState.Status, Is.EqualTo("ACTIVE"));
        Assert.That(refilledState.RefillsRemaining, Is.EqualTo(2));
        Assert.That(dcState.Status, Is.EqualTo("DISCONTINUED"));
    }

    [Test]
    public async Task Bcma_MedicationAdministrationRecord()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var adminId = $"BCMA-{Guid.NewGuid()}";
        var bcma = _cluster.GrainFactory.GetGrain<IBcmaGrain>(adminId);
        var now = DateTime.UtcNow;

        await bcma.RecordAdministrationAsync(patientId, "Morphine 4mg", "DRUG-050",
            "4mg", "IV", "GIVEN", now.AddMinutes(-30), now,
            "NURSE-001", "Nurse Jane", "Left Forearm", "RX-001", "ORDER-001", null);
        var givenState = await bcma.GetAdministrationAsync();

        await bcma.RecordWitnessAsync("NURSE-002", "Nurse Bob");
        var witnessedState = await bcma.GetAdministrationAsync();

        await bcma.RecordPrnReasonAsync("Pain level 7/10");
        await bcma.RecordPrnEffectivenessAsync("Pain reduced to 3/10 within 30 minutes");
        var prnState = await bcma.GetAdministrationAsync();

        Assert.That(givenState.ActionStatus, Is.EqualTo("GIVEN"));
        Assert.That(givenState.DrugName, Is.EqualTo("Morphine 4mg"));
        Assert.That(givenState.InjectionSite, Is.EqualTo("Left Forearm"));
        Assert.That(witnessedState.WitnessName, Is.EqualTo("Nurse Bob"));
        Assert.That(prnState.PrnReason, Is.EqualTo("Pain level 7/10"));
        Assert.That(prnState.PrnEffectiveness, Is.EqualTo("Pain reduced to 3/10 within 30 minutes"));
    }

    [Test]
    public async Task Bcma_MedicationNotGiven()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var adminId = $"BCMA-{Guid.NewGuid()}";
        var bcma = _cluster.GrainFactory.GetGrain<IBcmaGrain>(adminId);
        var now = DateTime.UtcNow;

        await bcma.RecordAdministrationAsync(patientId, "Metoprolol 25mg", "DRUG-060",
            "25mg", "ORAL", "GIVEN", now, now,
            "NURSE-001", "Nurse Jane", null, null, null, null);

        await bcma.MarkNotGivenAsync("Patient refused medication");
        var state = await bcma.GetAdministrationAsync();

        Assert.That(state.ActionStatus, Is.EqualTo("NOT GIVEN"));
        Assert.That(state.ReasonNotGiven, Is.EqualTo("Patient refused medication"));
    }

    [Test]
    public async Task Radiology_CompleteWorkflow_FromOrderToReport()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var radId = $"RAD-{Guid.NewGuid()}";
        var rad = _cluster.GrainFactory.GetGrain<IRadiologyGrain>(radId);
        var now = DateTime.UtcNow;

        await rad.OrderStudyAsync(patientId, "Chest X-Ray PA and Lateral", "PROC-001",
            "71046", "GENERAL RADIOLOGY", "PROVIDER-123", "Dr. Smith",
            "ROUTINE", "Cough for 2 weeks", "Rule out pneumonia",
            null, "RAD-DEPT-001", "Radiology Department");
        var orderedState = await rad.GetRadiologyAsync();

        await rad.RecordExamAsync(now);
        var examinedState = await rad.GetRadiologyAsync();

        await rad.RecordReportAsync(
            "PA and lateral views of the chest demonstrate clear lung fields bilaterally.",
            "No acute cardiopulmonary disease.", null,
            "PROVIDER-456", "Dr. Radiologist", now.AddHours(1));
        var reportedState = await rad.GetRadiologyAsync();

        await rad.CompleteAsync();
        var completedState = await rad.GetRadiologyAsync();

        Assert.That(orderedState.Status, Is.EqualTo("PENDING"));
        Assert.That(orderedState.ProcedureName, Is.EqualTo("Chest X-Ray PA and Lateral"));
        Assert.That(examinedState.Status, Is.EqualTo("EXAMINED"));
        Assert.That(reportedState.Impression, Is.EqualTo("No acute cardiopulmonary disease."));
        Assert.That(completedState.Status, Is.EqualTo("COMPLETE"));
    }

    [Test]
    public async Task Vital_RecordAndMarkAbnormal()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var vitalId = $"VITAL-{Guid.NewGuid()}";
        var vital = _cluster.GrainFactory.GetGrain<IVitalGrain>(vitalId);
        var now = DateTime.UtcNow;

        await vital.RecordVitalAsync(patientId, "BLOOD PRESSURE", "180/110", "mmHg",
            now, "CLINIC-001", "Primary Care", "NURSE-001", "Nurse Jane",
            new List<string> { "SITTING" }, null);
        var state = await vital.GetVitalAsync();

        await vital.MarkAbnormalAsync("CRITICAL HIGH");
        var abnormalState = await vital.GetVitalAsync();

        Assert.That(state.VitalType, Is.EqualTo("BLOOD PRESSURE"));
        Assert.That(state.Value, Is.EqualTo("180/110"));
        Assert.That(state.Qualifiers, Has.Count.EqualTo(1));
        Assert.That(state.Qualifiers[0], Is.EqualTo("SITTING"));
        Assert.That(abnormalState.AbnormalFlag, Is.EqualTo("CRITICAL HIGH"));
    }

    [Test]
    public async Task Vital_MarkEnteredInError()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var vitalId = $"VITAL-{Guid.NewGuid()}";
        var vital = _cluster.GrainFactory.GetGrain<IVitalGrain>(vitalId);

        await vital.RecordVitalAsync(patientId, "TEMPERATURE", "98.6", "F",
            DateTime.UtcNow, null, null, null, null, null, null);

        await vital.MarkEnteredInErrorAsync("Wrong patient");
        var state = await vital.GetVitalAsync();

        Assert.That(state.IsEnteredInError, Is.True);
        Assert.That(state.EnteredInErrorReason, Is.EqualTo("Wrong patient"));
    }

    [Test]
    public async Task TiuDocument_ProgressNote_SignAndCosign()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var docId = $"TIU-{Guid.NewGuid()}";
        var doc = _cluster.GrainFactory.GetGrain<ITiuDocumentGrain>(docId);
        var now = DateTime.UtcNow;

        await doc.CreateDocumentAsync(patientId, "PROGRESS NOTE", "DOC-TYPE-001",
            "Patient presents with complaint of headache for 3 days.",
            "Headache Evaluation", "RESIDENT-001", "Dr. Resident",
            "ATTENDING-001", "Dr. Attending", "CLINIC-001", "Primary Care",
            null, now);
        var unsignedState = await doc.GetDocumentAsync();

        await doc.SignDocumentAsync(now.AddMinutes(30));
        var signedState = await doc.GetDocumentAsync();

        await doc.CosignDocumentAsync(now.AddHours(2));
        var cosignedState = await doc.GetDocumentAsync();

        Assert.That(unsignedState.Status, Is.EqualTo("UNSIGNED"));
        Assert.That(unsignedState.DocumentType, Is.EqualTo("PROGRESS NOTE"));
        Assert.That(unsignedState.AuthorName, Is.EqualTo("Dr. Resident"));
        Assert.That(signedState.Status, Is.EqualTo("UNCOSIGNED"));
        Assert.That(signedState.SignedDateTime, Is.Not.Null);
        Assert.That(cosignedState.Status, Is.EqualTo("COMPLETED"));
        Assert.That(cosignedState.CosignedDateTime, Is.Not.Null);
    }

    [Test]
    public async Task TiuDocument_AmendAndAddAddendum()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var docId = $"TIU-{Guid.NewGuid()}";
        var doc = _cluster.GrainFactory.GetGrain<ITiuDocumentGrain>(docId);

        await doc.CreateDocumentAsync(patientId, "DISCHARGE SUMMARY", null,
            "Patient discharged in stable condition.",
            "Discharge", "PROVIDER-123", "Dr. Smith",
            null, null, null, null, null, DateTime.UtcNow);

        await doc.SignDocumentAsync(DateTime.UtcNow);
        await doc.AmendDocumentAsync("Patient discharged in stable condition. Follow-up in 2 weeks.");
        var amendedState = await doc.GetDocumentAsync();

        await doc.AddAddendumAsync("ADDENDUM-001");
        var withAddendumState = await doc.GetDocumentAsync();

        Assert.That(amendedState.Status, Is.EqualTo("AMENDED"));
        Assert.That(amendedState.ReportText, Does.Contain("Follow-up in 2 weeks"));
        Assert.That(withAddendumState.AddendumIds, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Consult_CompleteLifecycle()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var consultId = $"CONSULT-{Guid.NewGuid()}";
        var consult = _cluster.GrainFactory.GetGrain<IConsultGrain>(consultId);
        var now = DateTime.UtcNow;

        await consult.RequestConsultAsync(patientId, "Cardiology", "SVC-CARD",
            "Primary Care", "SVC-PC", "ROUTINE",
            "PROVIDER-123", "Dr. Smith", "PROVIDER-456", "Dr. Cardiologist",
            "Evaluate chest pain on exertion", "Angina pectoris",
            null, "CLINIC-001", "Primary Care");
        var pendingState = await consult.GetConsultAsync();

        await consult.AcceptAsync();
        var activeState = await consult.GetConsultAsync();

        await consult.ScheduleAsync();
        var scheduledState = await consult.GetConsultAsync();

        await consult.CompleteAsync(now, "TIU-DOC-001");
        var completedState = await consult.GetConsultAsync();

        Assert.That(pendingState.Status, Is.EqualTo("PENDING"));
        Assert.That(pendingState.ToService, Is.EqualTo("Cardiology"));
        Assert.That(pendingState.Urgency, Is.EqualTo("ROUTINE"));
        Assert.That(activeState.Status, Is.EqualTo("ACTIVE"));
        Assert.That(scheduledState.Status, Is.EqualTo("SCHEDULED"));
        Assert.That(completedState.Status, Is.EqualTo("COMPLETE"));
        Assert.That(completedState.ResultDocumentId, Is.EqualTo("TIU-DOC-001"));
    }

    [Test]
    public async Task Surgery_CompleteWorkflow()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var surgeryId = $"SURG-{Guid.NewGuid()}";
        var surgery = _cluster.GrainFactory.GetGrain<ISurgeryGrain>(surgeryId);
        var now = DateTime.UtcNow;

        await surgery.ScheduleSurgeryAsync(patientId, "Laparoscopic Cholecystectomy", "47562",
            now.AddDays(7), "SURGEON-001", "Dr. Surgeon", "GENERAL",
            "General Surgery", "Cholelithiasis", "OR-001", "Operating Room 1", null);
        var scheduledState = await surgery.GetSurgeryAsync();

        await surgery.AddAssistantAsync("ASST-001", "Dr. Assistant");
        await surgery.AddOtherProcedureAsync("Intraoperative Cholangiogram");

        await surgery.BeginOperationAsync(now, "ANES-001", "Dr. Anesthesiologist");
        var inProgressState = await surgery.GetSurgeryAsync();

        await surgery.EndOperationAsync(now.AddHours(2));
        await surgery.RecordOperativeReportAsync(
            "Procedure performed without complications. Gallbladder removed intact.",
            "Cholelithiasis, confirmed", "CLEAN");

        await surgery.CompleteAsync();
        var completedState = await surgery.GetSurgeryAsync();

        Assert.That(scheduledState.Status, Is.EqualTo("SCHEDULED"));
        Assert.That(scheduledState.PrincipalProcedure, Is.EqualTo("Laparoscopic Cholecystectomy"));
        Assert.That(inProgressState.Status, Is.EqualTo("IN PROGRESS"));
        Assert.That(inProgressState.AnesthesiologistName, Is.EqualTo("Dr. Anesthesiologist"));
        Assert.That(completedState.Status, Is.EqualTo("COMPLETED"));
        Assert.That(completedState.OperativeReport, Does.Contain("without complications"));
        Assert.That(completedState.PostOpDiagnosis, Is.EqualTo("Cholelithiasis, confirmed"));
        Assert.That(completedState.OtherProcedures, Has.Count.EqualTo(1));
        Assert.That(completedState.FirstAssistantName, Is.EqualTo("Dr. Assistant"));
    }

    [Test]
    public async Task ClinicalReminder_DueToResolved()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var reminderId = $"REM-{Guid.NewGuid()}";
        var reminder = _cluster.GrainFactory.GetGrain<IClinicalReminderGrain>(reminderId);

        await reminder.CreateReminderAsync(patientId, "Influenza Vaccine", "REM-DEF-001",
            "IMMUNIZATION", "NORMAL", "1Y", DateTime.UtcNow.AddDays(-30));
        var dueState = await reminder.GetReminderAsync();

        await reminder.MarkDoneAsync(DateTime.UtcNow, "PROVIDER-123", "Dr. Smith");
        var doneState = await reminder.GetReminderAsync();

        await reminder.UpdateDueDateAsync(DateTime.UtcNow.AddYears(1));
        var nextDueState = await reminder.GetReminderAsync();

        Assert.That(dueState.Status, Is.EqualTo("DUE"));
        Assert.That(dueState.ReminderName, Is.EqualTo("Influenza Vaccine"));
        Assert.That(dueState.Category, Is.EqualTo("IMMUNIZATION"));
        Assert.That(doneState.Status, Is.EqualTo("DONE"));
        Assert.That(doneState.EvaluatedByProviderName, Is.EqualTo("Dr. Smith"));
        Assert.That(nextDueState.Status, Is.EqualTo("DUE"));
        Assert.That(nextDueState.NextDueDate, Is.Not.Null);
    }

    [Test]
    public async Task Immunization_RecordViaWorkflow()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);

        var immId = await workflow.RecordImmunizationAsync(
            "COVID-19 mRNA Vaccine", "213",
            DateTime.UtcNow, "1",
            "LOT-ABC-123", "Moderna",
            "NURSE-001", "Nurse Jane",
            "LEFT DELTOID", "INTRAMUSCULAR",
            "0.5 mL",
            "CLINIC-001", "Immunization Clinic", null);

        ImmunizationEntry? entry = await patient.GetImmunizationAsync(immId);
        Assert.That(entry!.ImmunizationName, Is.EqualTo("COVID-19 mRNA Vaccine"));
        Assert.That(entry.CvxCode, Is.EqualTo("213"));
        Assert.That(entry.Series, Is.EqualTo("1"));
        Assert.That(entry.Manufacturer, Is.EqualTo("Moderna"));
        Assert.That(entry.AdministrationSite, Is.EqualTo("LEFT DELTOID"));
    }

    [Test]
    public async Task HealthFactor_RecordSocialHistory()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var hfId = $"HF-{Guid.NewGuid()}";
        var hf = _cluster.GrainFactory.GetGrain<IHealthFactorGrain>(hfId);

        await hf.RecordHealthFactorAsync(patientId, "CURRENT SMOKER", "HF-DEF-001",
            "SOCIAL HISTORY", DateTime.UtcNow, "HEAVY/SEVERE",
            null, "CLINIC-001", "Primary Care",
            "PROVIDER-123", "Dr. Smith", "1 pack per day for 20 years");
        var state = await hf.GetHealthFactorAsync();

        Assert.That(state.HealthFactorName, Is.EqualTo("CURRENT SMOKER"));
        Assert.That(state.Category, Is.EqualTo("SOCIAL HISTORY"));
        Assert.That(state.LevelSeverity, Is.EqualTo("HEAVY/SEVERE"));
        Assert.That(state.Comments, Is.EqualTo("1 pack per day for 20 years"));
    }

    [Test]
    public async Task MentalHealth_PHQ9Screening()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var mhId = $"MH-{Guid.NewGuid()}";
        var mh = _cluster.GrainFactory.GetGrain<IMentalHealthGrain>(mhId);
        var now = DateTime.UtcNow;

        var responses = new Dictionary<string, string>
        {
            { "Q1", "2" }, { "Q2", "3" }, { "Q3", "1" }, { "Q4", "2" },
            { "Q5", "1" }, { "Q6", "2" }, { "Q7", "1" }, { "Q8", "0" }, { "Q9", "0" }
        };

        await mh.RecordInstrumentAsync(patientId, "PHQ-9", "MH-DEF-PHQ9",
            now, 12m, "MODERATE", true, responses,
            "NURSE-001", "Nurse Jane", "PROVIDER-123", "Dr. Smith",
            "CLINIC-001", "Primary Care", null, null);
        var state = await mh.GetInstrumentAsync();

        Assert.That(state.InstrumentName, Is.EqualTo("PHQ-9"));
        Assert.That(state.TotalScore, Is.EqualTo(12m));
        Assert.That(state.ScoreInterpretation, Is.EqualTo("MODERATE"));
        Assert.That(state.IsPositiveScreen, Is.True);
        Assert.That(state.Responses, Has.Count.EqualTo(9));
        Assert.That(state.Status, Is.EqualTo("COMPLETED"));
    }

    [Test]
    public async Task MentalHealth_CancelledScreening()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var mhId = $"MH-{Guid.NewGuid()}";
        var mh = _cluster.GrainFactory.GetGrain<IMentalHealthGrain>(mhId);

        await mh.RecordInstrumentAsync(patientId, "GAD-7", null,
            DateTime.UtcNow, null, null, null, null,
            null, null, null, null, null, null, null, null);

        await mh.CancelAsync();
        var state = await mh.GetInstrumentAsync();

        Assert.That(state.Status, Is.EqualTo("CANCELLED"));
    }

    [Test]
    public async Task Dietetics_DietOrderViaWorkflow()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);

        var dietId = await workflow.CreateDietOrderAsync(
            "REGULAR", "Regular Diet",
            new List<string> { "LOW SODIUM", "DIABETIC" },
            "REGULAR", "THIN", "2000 kcal",
            "No pork products", DateTime.UtcNow,
            "PROVIDER-123", "Dr. Smith", null);

        DieteticsEntry? activeEntry = await patient.GetDietOrderAsync(dietId);
        Assert.That(activeEntry!.Status, Is.EqualTo("ACTIVE"));
        Assert.That(activeEntry.DietType, Is.EqualTo("REGULAR"));
        Assert.That(activeEntry.Modifications, Has.Count.EqualTo(2));

        await workflow.DiscontinueDietOrderAsync(dietId);
        DieteticsEntry? dcEntry = await patient.GetDietOrderAsync(dietId);
        Assert.That(dcEntry!.Status, Is.EqualTo("DISCONTINUED"));
    }

    [Test]
    public async Task Prosthetics_IssueViaWorkflow()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);

        var prosthId = await workflow.IssueProstheticAsync(
            "Power Wheelchair", "K0856", "ORTHOTIC",
            DateTime.UtcNow, 1, 5500.00m,
            "PROVIDER-123", "Dr. Smith",
            "PROSTH-CLINIC", "Prosthetics Clinic",
            true, null);

        ProstheticsEntry? issuedEntry = await patient.GetProstheticsItemAsync(prosthId);
        Assert.That(issuedEntry!.Status, Is.EqualTo("ISSUED"));
        Assert.That(issuedEntry.ItemDescription, Is.EqualTo("Power Wheelchair"));
        Assert.That(issuedEntry.Cost, Is.EqualTo(5500.00m));
        Assert.That(issuedEntry.IsServiceConnected, Is.True);
    }

    [Test]
    public async Task Imaging_CaptureAndReview()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var imageId = $"IMG-{Guid.NewGuid()}";
        var image = _cluster.GrainFactory.GetGrain<IImagingGrain>(imageId);
        var now = DateTime.UtcNow;

        // Using Azure Blob Storage URLs as example
        var imageUrl = "https://mystorageaccount.blob.core.windows.net/dicom-images/chest_001.dcm";
        var thumbnailUrl = "https://cdn.example.com/thumbnails/chest_001.jpg";

        await image.CaptureImageAsync(patientId, "XRAY", "Chest X-Ray",
            "RADIOLOGY", imageUrl, thumbnailUrl,
            "1.2.3.4.5", "1.2.3.4", now, now, 2,
            "RAD-001", null, "TECH-001", "Tech Johnson",
            "RAD-DEPT", "Radiology", null);
        var viewableState = await image.GetImageAsync();

        await image.MarkForReviewAsync();
        var reviewState = await image.GetImageAsync();

        await image.QaReviewAsync();
        var qaState = await image.GetImageAsync();

        Assert.That(viewableState.Status, Is.EqualTo("VIEWABLE"));
        Assert.That(viewableState.ObjectType, Is.EqualTo("XRAY"));
        Assert.That(viewableState.ImageCount, Is.EqualTo(2));
        Assert.That(viewableState.DicomStudyUid, Is.EqualTo("1.2.3.4"));
        Assert.That(viewableState.ImageUrl, Is.EqualTo(imageUrl));
        Assert.That(viewableState.ThumbnailUrl, Is.EqualTo(thumbnailUrl));
        Assert.That(reviewState.Status, Is.EqualTo("NEEDS REVIEW"));
        Assert.That(qaState.Status, Is.EqualTo("QA REVIEWED"));
    }

    [Test]
    public async Task Adt_AdmissionTransferDischarge()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var adtId = $"ADT-{Guid.NewGuid()}";
        var adt = _cluster.GrainFactory.GetGrain<IAdtGrain>(adtId);
        var admitTime = DateTime.UtcNow;

        await adt.RecordAdmissionAsync(patientId, admitTime,
            "WARD-3A", "Medical Ward 3A", "301-A",
            "TS-MED", "MEDICINE", "PROVIDER-123", "Dr. Smith",
            "INPATIENT", "Pneumonia", null);
        var admittedState = await adt.GetMovementAsync();

        await adt.RecordTransferAsync("WARD-ICU", "Intensive Care", "ICU-5",
            "TS-ICU", "CRITICAL CARE", admitTime.AddDays(1));
        var transferredState = await adt.GetMovementAsync();

        await adt.RecordDischargeAsync(admitTime.AddDays(5),
            "Community-acquired pneumonia, resolved", "REGULAR", null);
        var dischargedState = await adt.GetMovementAsync();

        Assert.That(admittedState.TransactionType, Is.EqualTo("ADMISSION"));
        Assert.That(admittedState.WardLocationName, Is.EqualTo("Medical Ward 3A"));
        Assert.That(admittedState.RoomBed, Is.EqualTo("301-A"));
        Assert.That(transferredState.TransactionType, Is.EqualTo("TRANSFER"));
        Assert.That(transferredState.WardLocationName, Is.EqualTo("Intensive Care"));
        Assert.That(dischargedState.TransactionType, Is.EqualTo("DISCHARGE"));
        Assert.That(dischargedState.LengthOfStay, Is.EqualTo(5));
        Assert.That(dischargedState.Disposition, Is.EqualTo("REGULAR"));
    }

    [Test]
    public async Task MeansTest_RecordViaWorkflow()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);

        var mtId = await workflow.RecordMeansTestAsync(
            "MEANS TEST", DateTime.UtcNow,
            35000m, 10000m, 2,
            "VERIFIED", "1",
            "CLERK-001", "Clerk Smith", null);

        MeansTestEntry? entry = await patient.GetMeansTestAsync(mtId);
        Assert.That(entry, Is.Not.Null);
        Assert.That(entry!.TestType, Is.EqualTo("MEANS TEST"));
        Assert.That(entry.EligibilityStatus, Is.EqualTo("VERIFIED"));
        Assert.That(entry.PriorityGroup, Is.EqualTo("1"));
    }

    [Test]
    public async Task ServiceConnectedCondition_RecordViaWorkflow()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);

        var scId = await workflow.RecordServiceConnectedConditionAsync(
            "Post-Traumatic Stress Disorder", "F43.10",
            30, true,
            new DateTime(2015, 6, 1), null, "Combat-related PTSD");

        ScConditionEntry? activeEntry = await patient.GetScConditionAsync(scId);
        Assert.That(activeEntry!.Status, Is.EqualTo("ACTIVE"));
        Assert.That(activeEntry.Condition, Is.EqualTo("Post-Traumatic Stress Disorder"));
        Assert.That(activeEntry.DiagnosisCode, Is.EqualTo("F43.10"));
        Assert.That(activeEntry.DisabilityPercentage, Is.EqualTo(30));
        Assert.That(activeEntry.IsServiceConnected, Is.True);

        await workflow.SetServiceConnectedPercentageAsync(scId, 50);
        ScConditionEntry? updatedEntry = await patient.GetScConditionAsync(scId);
        Assert.That(updatedEntry!.ServiceConnectedPercentage, Is.EqualTo(50));
    }

    [Test]
    public async Task Patient_CanLinkAllGrainTypes()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        await patient.UpdateDemographicsAsync("Test Patient", "M", new DateTime(1970, 1, 1), null);

        await patient.AddPharmacyIdAsync("RX-1");
        await patient.AddBcmaIdAsync("BCMA-1");
        await patient.AddRadiologyIdAsync("RAD-1");
        await patient.AddVitalIdAsync("VITAL-1");
        await patient.AddTiuDocumentIdAsync("TIU-1");
        await patient.AddConsultIdAsync("CONSULT-1");
        await patient.AddSurgeryIdAsync("SURG-1");
        await patient.AddClinicalReminderIdAsync("REM-1");
        await patient.AddHealthFactorIdAsync("HF-1");
        await patient.AddMentalHealthIdAsync("MH-1");
        await patient.AddImagingIdAsync("IMG-1");
        await patient.AddAdtIdAsync("ADT-1");

        var state = await patient.GetPatientAsync();

        Assert.That(state.PharmacyIds, Has.Count.EqualTo(1));
        Assert.That(state.BcmaIds, Has.Count.EqualTo(1));
        Assert.That(state.RadiologyIds, Has.Count.EqualTo(1));
        Assert.That(state.VitalIds, Has.Count.EqualTo(1));
        Assert.That(state.TiuDocumentIds, Has.Count.EqualTo(1));
        Assert.That(state.ConsultIds, Has.Count.EqualTo(1));
        Assert.That(state.SurgeryIds, Has.Count.EqualTo(1));
        Assert.That(state.ClinicalReminderIds, Has.Count.EqualTo(1));
        Assert.That(state.HealthFactorIds, Has.Count.EqualTo(1));
        Assert.That(state.MentalHealthIds, Has.Count.EqualTo(1));
        Assert.That(state.ImagingIds, Has.Count.EqualTo(1));
        Assert.That(state.AdtIds, Has.Count.EqualTo(1));

        // Verify idempotency — adding same ID again should not duplicate
        await patient.AddPharmacyIdAsync("RX-1");
        var stateAfterDupe = await patient.GetPatientAsync();
        Assert.That(stateAfterDupe.PharmacyIds, Has.Count.EqualTo(1));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Workflow-Level Functional Tests for All 13 Subsystems
    //
    // These exercise the PatientWorkflowGrain orchestration end-to-end:
    //   • Creates data via workflow grain
    //   • Verifies state linkage to PatientGrain ID lists
    //   • Confirms retrieval lists are correct
    //   • Tests lifecycle transitions (complete, cancel, discontinue)
    // ═══════════════════════════════════════════════════════════════════════

    [Test]
    public async Task Workflow_Surgery_ScheduleCompleteCancel()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        await patient.UpdateDemographicsAsync("Surgery Patient", "M", new DateTime(1965, 4, 15), null);

        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        var surg1 = await workflow.ScheduleSurgeryAsync("Total Hip Replacement", "27130",
            DateTime.UtcNow.AddDays(14), "SURG-1", "Dr. Ortho", "GENERAL",
            "Orthopedics", "OA Left Hip", "OR-1", "OR Suite 1", null);

        var surg2 = await workflow.ScheduleSurgeryAsync("Appendectomy", "44970",
            DateTime.UtcNow.AddDays(1), null, null, null, "General Surgery", null, null, null, null);

        // Verify PatientGrain linkage
        var pState = await patient.GetPatientAsync();
        Assert.That(pState.SurgeryIds, Has.Count.EqualTo(2));

        // Complete first surgery with operative report
        await workflow.CompleteSurgeryAsync(surg1,
            "Total hip arthroplasty performed without complications. Anterior approach.",
            "OA Left Hip, post-op");
        var state1 = await workflow.GetSurgeryAsync(surg1);
        Assert.That(state1.Status, Is.EqualTo("COMPLETED"));
        Assert.That(state1.OperativeReport, Does.Contain("Anterior approach"));

        // Cancel second surgery
        await workflow.CancelSurgeryAsync(surg2, "Patient declined");
        var state2 = await workflow.GetSurgeryAsync(surg2);
        Assert.That(state2.Status, Is.EqualTo("CANCELLED"));

        // List should have both
        var list = await workflow.GetSurgeriesAsync(50);
        Assert.That(list, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Workflow_Radiology_OrderAndComplete()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        await patient.UpdateDemographicsAsync("Radiology Patient", "F", new DateTime(1972, 8, 10), null);

        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        var radId = await workflow.OrderRadiologyStudyAsync("CT Abdomen with Contrast", null,
            "74178", "CT SCAN", "PROV-1", "Dr. Smith", "ROUTINE",
            "Abdominal pain", "R/O appendicitis", null, "RAD-DEPT", "Radiology");

        var pState = await patient.GetPatientAsync();
        Assert.That(pState.RadiologyIds, Has.Count.EqualTo(1));

        await workflow.CompleteRadiologyAsync(radId,
            "No evidence of acute appendicitis. Normal appendix visualized.",
            "Normal CT abdomen", "RAD-1", "Dr. Radiologist");

        var state = await workflow.GetRadiologyStudyAsync(radId);
        Assert.That(state.Status, Is.EqualTo("COMPLETE"));
        Assert.That(state.ReportText, Does.Contain("Normal appendix"));

        var list = await workflow.GetRadiologyStudiesAsync(50);
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].HasReport, Is.True);
    }

    [Test]
    public async Task Workflow_Bcma_RecordAdministrations()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        await patient.UpdateDemographicsAsync("BCMA Patient", "M", new DateTime(1950, 2, 1), null);

        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        await workflow.RecordMedicationAdministrationAsync(
            "Insulin Lispro 10 units", "DRUG-INS", "10 units", "SUBQ",
            "GIVEN", DateTime.UtcNow.AddHours(-4), DateTime.UtcNow.AddHours(-4),
            "RN-1", "Nurse Adams", "Abdomen", null, null, null);

        await workflow.RecordMedicationAdministrationAsync(
            "Metoprolol 50mg", "DRUG-MET", "50mg", "PO",
            "GIVEN", DateTime.UtcNow, DateTime.UtcNow,
            "RN-1", "Nurse Adams", null, "RX-100", null, null);

        var pState = await patient.GetPatientAsync();
        Assert.That(pState.BcmaIds, Has.Count.EqualTo(2));

        var list = await workflow.GetMedicationAdministrationsAsync(50);
        Assert.That(list, Has.Count.EqualTo(2));
        Assert.That(list.All(x => x.ActionStatus == "GIVEN"), Is.True);
    }

    [Test]
    public async Task Workflow_Imaging_CaptureAndList()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        await patient.UpdateDemographicsAsync("Imaging Patient", "F", new DateTime(1980, 11, 30), null);

        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        var imgId = await workflow.CaptureImageAsync("XRAY", "Chest PA", "RAD",
            "https://blob.storage/img.dcm", "https://cdn/thumb.jpg",
            "1.2.3", "1.2.4", DateTime.UtcNow, DateTime.UtcNow, 2,
            null, null, "TECH-1", "Tech Wilson", "RAD-DEPT", "Radiology", null);

        var pState = await patient.GetPatientAsync();
        Assert.That(pState.ImagingIds, Has.Count.EqualTo(1));

        var list = await workflow.GetImagesAsync(50);
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].ObjectType, Is.EqualTo("XRAY"));
        Assert.That(list[0].ImageCount, Is.EqualTo(2));
    }

    [Test]
    public async Task Workflow_Reminders_CreateAndComplete()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        await patient.UpdateDemographicsAsync("Reminder Patient", "M", new DateTime(1960, 7, 4), null);

        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        var rem1 = await workflow.CreateReminderAsync("Colonoscopy Screening", null,
            "PREVENTIVE", "HIGH", "10Y", DateTime.UtcNow.AddDays(-60));

        var rem2 = await workflow.CreateReminderAsync("Fall Risk Assessment", null,
            "SAFETY", "NORMAL", "1Y", DateTime.UtcNow.AddDays(30));

        var pState = await patient.GetPatientAsync();
        Assert.That(pState.ClinicalReminderIds, Has.Count.EqualTo(2));

        // Complete the overdue one
        await workflow.CompleteReminderAsync(rem1, "PROV-1", "Dr. Smith");

        var list = await workflow.GetRemindersAsync();
        Assert.That(list, Has.Count.EqualTo(2));
        var done = list.First(r => r.ReminderId == rem1);
        Assert.That(done.Status, Is.EqualTo("DONE"));
    }

    [Test]
    public async Task Workflow_Immunizations_RecordAndList()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        await patient.UpdateDemographicsAsync("Imm Patient", "F", new DateTime(1975, 3, 20), null);

        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        await workflow.RecordImmunizationAsync("Influenza Vaccine", "158",
            DateTime.UtcNow, null, "LOT-FLU-2024", "Sanofi",
            "RN-1", "Nurse Kim", "LEFT DELTOID", "IM", "0.5 mL",
            "CL-IMM", "Immunization Clinic", null);

        await workflow.RecordImmunizationAsync("Tdap", "115",
            DateTime.UtcNow.AddMonths(-6), "Booster", "LOT-TDAP", "GSK",
            "RN-2", "Nurse Lee", "RIGHT DELTOID", "IM", "0.5 mL",
            null, null, null);

        List<ImmunizationEntry> entries = await patient.GetImmunizationsAsync();
        Assert.That(entries, Has.Count.EqualTo(2));

        var list = await workflow.GetImmunizationsAsync();
        Assert.That(list, Has.Count.EqualTo(2));
        Assert.That(list[0].ImmunizationName, Is.EqualTo("Influenza Vaccine")); // Most recent first
    }

    [Test]
    public async Task Workflow_HealthFactors_RecordAndList()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        await patient.UpdateDemographicsAsync("HF Patient", "M", new DateTime(1958, 1, 1), null);

        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        await workflow.RecordHealthFactorAsync("PREVIOUS SMOKER", "TOBACCO",
            DateTime.UtcNow, "MODERATE", null, "CL-1", "Primary Care",
            "PROV-1", "Dr. Smith", "Quit 5 years ago");

        await workflow.RecordHealthFactorAsync("ALCOHOL - CURRENT USER", "ALCOHOL",
            DateTime.UtcNow, "LIGHT", null, null, null, null, null, null);

        var pState = await patient.GetPatientAsync();
        Assert.That(pState.HealthFactorIds, Has.Count.EqualTo(2));

        var list = await workflow.GetHealthFactorsAsync();
        Assert.That(list, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Workflow_MentalHealth_RecordScreens()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        await patient.UpdateDemographicsAsync("MH Patient", "M", new DateTime(1970, 5, 15), null);

        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        var responses = new Dictionary<string, string>
        {
            { "Q1", "Not at all" }, { "Q2", "Several days" }
        };

        await workflow.RecordMentalHealthScreenAsync("PHQ-2", DateTime.UtcNow,
            1m, "MINIMAL", false, responses, "PROV-1", "Dr. Smith",
            "CL-MH", "Mental Health Clinic", null);

        var pState = await patient.GetPatientAsync();
        Assert.That(pState.MentalHealthIds, Has.Count.EqualTo(1));

        var list = await workflow.GetMentalHealthScreensAsync();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].InstrumentName, Is.EqualTo("PHQ-2"));
        Assert.That(list[0].TotalScore, Is.EqualTo(1));
        Assert.That(list[0].IsPositiveScreen, Is.False);
    }

    [Test]
    public async Task Workflow_Dietetics_CreateAndDiscontinue()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        await patient.UpdateDemographicsAsync("Diet Patient", "F", new DateTime(1945, 12, 25), null);

        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        var dietId = await workflow.CreateDietOrderAsync("CARDIAC", "Low Sodium Cardiac",
            new List<string> { "LOW SODIUM", "LOW FAT" }, "REGULAR", "THIN",
            "1800 kcal", "No added salt", DateTime.UtcNow,
            "PROV-1", "Dr. Nutrition", null);

        List<DieteticsEntry> entries = await patient.GetDietOrdersAsync();
        Assert.That(entries, Has.Count.EqualTo(1));

        var list = await workflow.GetDietOrdersAsync();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].DietType, Is.EqualTo("CARDIAC"));
        Assert.That(list[0].Status, Is.EqualTo("ACTIVE"));

        await workflow.DiscontinueDietOrderAsync(dietId);
        list = await workflow.GetDietOrdersAsync();
        Assert.That(list[0].Status, Is.EqualTo("DISCONTINUED"));
    }

    [Test]
    public async Task Workflow_Prosthetics_IssueAndList()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        await patient.UpdateDemographicsAsync("Prosthetics Patient", "M", new DateTime(1955, 9, 11), null);

        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        await workflow.IssueProstheticAsync("Below-Knee Prosthesis", "L5301", "PROSTHETIC",
            DateTime.UtcNow, 1, 12000m, "PROV-1", "Dr. Rehab",
            "PROS-CL", "Prosthetics Lab", true, "Custom fit");

        List<ProstheticsEntry> prosthEntries = await patient.GetProstheticsItemsAsync();
        Assert.That(prosthEntries, Has.Count.EqualTo(1));

        var list = await workflow.GetProstheticsAsync();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].ItemDescription, Is.EqualTo("Below-Knee Prosthesis"));
        Assert.That(list[0].IsServiceConnected, Is.True);
    }

    [Test]
    public async Task Workflow_MeansTest_RecordAndList()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        await patient.UpdateDemographicsAsync("MT Patient", "F", new DateTime(1960, 6, 15), null);

        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        await workflow.RecordMeansTestAsync("MEANS TEST", DateTime.UtcNow,
            52000m, 15000m, 3, "VERIFIED", "GROUP 5",
            "CLERK-1", "Clerk Jones", null);

        List<MeansTestEntry> mtEntries = await patient.GetMeansTestsAsync();
        Assert.That(mtEntries, Has.Count.EqualTo(1));

        var list = await workflow.GetMeansTestsAsync();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].TestType, Is.EqualTo("MEANS TEST"));
        Assert.That(list[0].PriorityGroup, Is.EqualTo("GROUP 5"));
    }

    [Test]
    public async Task Workflow_ServiceConnected_RecordAndList()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        await patient.UpdateDemographicsAsync("SC Patient", "M", new DateTime(1968, 3, 1), null);

        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        await workflow.RecordServiceConnectedConditionAsync(
            "Hearing Loss, Bilateral", "H90.3", 40, true,
            new DateTime(2018, 1, 15), null, "Noise exposure during service");

        await workflow.RecordServiceConnectedConditionAsync(
            "Tinnitus", "H93.19", 10, true,
            new DateTime(2018, 1, 15), null, "Secondary to hearing loss");

        List<ScConditionEntry> scEntries = await patient.GetScConditionsAsync();
        Assert.That(scEntries, Has.Count.EqualTo(2));

        var list = await workflow.GetServiceConnectedConditionsAsync();
        Assert.That(list, Has.Count.EqualTo(2));
        Assert.That(list.All(x => x.IsServiceConnected), Is.True);
    }

    [Test]
    public async Task Workflow_Adt_AdmitAndDischarge()
    {
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        await patient.UpdateDemographicsAsync("ADT Patient", "F", new DateTime(1940, 7, 4), null);

        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        // The unit must exist and be active before any admission (unit owns bed truth).
        var institutionId = $"INST-{Guid.NewGuid():N}";
        var unitId = $"U-{Guid.NewGuid():N}";
        var unit = _cluster.GrainFactory.GetGrain<IInpatientUnitGrain>($"UNIT:{institutionId}:{unitId}");
        await unit.ConfigureUnitAsync("Medical Ward 4B", "MEDICINE", "Internal Medicine");
        await unit.AddBedAsync("405-B", null, BedType.Regular);

        var adtId = await workflow.RecordAdmissionAsync(DateTime.UtcNow.AddDays(-3),
            institutionId, unitId, "405-B", "Internal Medicine",
            "PROV-1", "Dr. Attending", "CHF Exacerbation", null);

        var pState = await patient.GetPatientAsync();
        Assert.That(pState.AdtIds, Has.Count.EqualTo(1));

        var list = await workflow.GetAdtMovementsAsync();
        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].MovementType, Is.EqualTo("ADMISSION"));
        Assert.That(list[0].Status, Is.EqualTo("ADMITTED"));

        await workflow.RecordDischargeAsync(adtId, DateTime.UtcNow,
            "CHF exacerbation, improved", "REGULAR", "Discharge to home with home health");

        list = await workflow.GetAdtMovementsAsync();
        Assert.That(list[0].Status, Is.EqualTo("DISCHARGED"));
    }

    [Test]
    public async Task Workflow_CoverSheet_IncludesAllSubsystems()
    {
        // End-to-end: create a patient with data in EVERY subsystem,
        // then verify the cover sheet returns the correct summaries
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        await patient.UpdateDemographicsAsync("Full Workup,Patient", "M",
            new DateTime(1950, 1, 1), "999-99-9999");
        await patient.UpdateVeteranInfoAsync("Y", 70, null, null);

        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        // Populate every cover sheet section
        await workflow.AddProblemAsync("Hypertension", "I10", "CHRONIC", "ROUTINE",
            DateTime.UtcNow, "P1", "Dr. A", "C1", "PC", false, null);
        await workflow.RecordAllergyAsync("ASA", "Drug", null, "O",
            new List<string> { "GI Upset" }, "Mild", "U1", "N1", null);
        await workflow.PlaceOrderAsync("Lab", "BMP", null, "P1", "Dr. A",
            "C1", "PC", "ROUTINE", null, null);
        await workflow.ScheduleAppointmentAsync("C1", "PC", DateTime.UtcNow.AddDays(7),
            30, "P1", "Dr. A", "Follow-up", "ROUTINE");
        await workflow.RecordVitalsAsync("C1", "PC", "U1", "N1", DateTime.UtcNow,
            new Dictionary<string, string> { { "BLOOD PRESSURE", "130/85" } }, null);
        await workflow.CreateNoteAsync("PROGRESS NOTE", null,
            "Annual exam completed.", "Annual", "P1", "Dr. A",
            null, null, "C1", "PC", null, DateTime.UtcNow);
        await workflow.RequestConsultAsync("Cardiology", null, "PC", null, "ROUTINE",
            "P1", "Dr. A", null, null, "Evaluate murmur", null, null, "C1", "PC");
        await workflow.CreateReminderAsync("Flu Shot", null, "PREVENTIVE",
            "HIGH", "1Y", DateTime.UtcNow.AddDays(-30));

        var cs = await workflow.GetCoverSheetAsync();

        Assert.That(cs.PatientId, Is.EqualTo(patientId));
        Assert.That(cs.Demographics.Name, Is.EqualTo("Full Workup,Patient"));
        Assert.That(cs.Demographics.IsVeteran, Is.True);
        Assert.That(cs.ActiveProblems, Has.Count.EqualTo(1));
        Assert.That(cs.Allergies, Has.Count.EqualTo(1));
        Assert.That(cs.Cwad.HasAllergies, Is.True);
        Assert.That(cs.ActiveOrders, Has.Count.EqualTo(1));
        Assert.That(cs.RecentVisits, Has.Count.EqualTo(1));
        Assert.That(cs.RecentVitals, Has.Count.EqualTo(1));
        Assert.That(cs.RecentNotes, Has.Count.EqualTo(1));
        Assert.That(cs.ActiveConsults, Has.Count.EqualTo(1));
        Assert.That(cs.ClinicalReminders, Has.Count.EqualTo(1));
        Assert.That(cs.LastRefreshed, Is.Not.EqualTo(default(DateTime)));
    }

    [Test]
    public async Task Workflow_ConcurrentFanOut_DoesNotDeadlock()
    {
        // Verifies that [Reentrant] on PatientWorkflowGrain allows
        // concurrent cover-sheet builds without deadlocking
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        await patient.UpdateDemographicsAsync("Concurrent Patient", "M", new DateTime(1970, 1, 1), null);

        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        // Populate some data
        await workflow.AddProblemAsync("DM2", "E11.9", "CHRONIC", "ROUTINE",
            DateTime.UtcNow, "P1", "Dr. A", null, null, false, null);
        await workflow.RecordAllergyAsync("Codeine", "Drug", null, "O",
            new List<string> { "Nausea" }, "Moderate", null, null, null);

        // Fire multiple concurrent cover sheet requests — should not deadlock
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => workflow.GetCoverSheetAsync())
            .ToArray();

        var results = await Task.WhenAll(tasks);
        Assert.That(results, Has.Length.EqualTo(5));
        Assert.That(results.All(cs => cs.ActiveProblems.Count == 1), Is.True);
        Assert.That(results.All(cs => cs.Allergies.Count == 1), Is.True);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Patient Workflow Grain Tests — derived from VistA MUMPS routines:
    //   ORWCV.m   (Cover Sheet), ORWPT.m (Patient Select),
    //   ORWDX.m   (Order Entry), ORWDXA.m (Order Actions),
    //   ORWORR.m  (Order Retrieval), GMPLSAVE.m (Problem List),
    //   SDAM2.m   (Check-In), SDAMEVT.m (Appointment Events)
    // ═══════════════════════════════════════════════════════════════════════

    [Test]
    public async Task Workflow_CoverSheet_GathersAllSections()
    {
        // Setup patient with data across multiple domains
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        await patient.UpdateDemographicsAsync("Jane Veteran", "F", new DateTime(1965, 3, 20), "555-12-3456");
        await patient.UpdateVeteranInfoAsync("Y", 30, null, null);

        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        // Add a problem (GMPLSAVE)
        var probId = await workflow.AddProblemAsync(
            "Essential Hypertension", "I10", "CHRONIC", "ROUTINE",
            new DateTime(2020, 1, 1), "PROV-1", "Dr. Smith",
            "CL-1", "Primary Care", false, null);

        // Add an allergy (CWAD "A" flag)
        await workflow.RecordAllergyAsync(
            "Penicillin", "Drug", null, "O",
            new List<string> { "Rash" }, "Moderate", "USR-1", "Nurse Jane", null);

        // Place an order (ORWDX SAVE)
        var orderId = await workflow.PlaceOrderAsync(
            "Pharmacy", "Lisinopril 10mg", "DRUG-001",
            "PROV-1", "Dr. Smith", "CL-1", "Primary Care",
            "ROUTINE", "Take once daily", "Hypertension");

        // Schedule an appointment (SDAMEVT MAKE)
        await workflow.ScheduleAppointmentAsync(
            "CL-1", "Primary Care", DateTime.UtcNow.AddDays(7),
            30, "PROV-1", "Dr. Smith", "Follow-up", "ROUTINE");

        // Record vitals (GMRVED)
        await workflow.RecordVitalsAsync(
            "CL-1", "Primary Care", "USR-1", "Nurse Jane", DateTime.UtcNow,
            new Dictionary<string, string>
            {
                { "BLOOD PRESSURE", "140/90" },
                { "PULSE", "72" },
                { "TEMPERATURE", "98.6" }
            }, null);

        // Now build cover sheet (ORWCV START/BUILD/POLL)
        var coverSheet = await workflow.GetCoverSheetAsync();

        // Verify all CPRS cover sheet sections present
        Assert.That(coverSheet.PatientId, Is.EqualTo(patientId));
        Assert.That(coverSheet.Demographics.Name, Is.EqualTo("Jane Veteran"));
        Assert.That(coverSheet.Demographics.IsVeteran, Is.True);
        Assert.That(coverSheet.Demographics.ServiceConnectedPercent, Is.EqualTo(30));
        Assert.That(coverSheet.Cwad.HasAllergies, Is.True);
        Assert.That(coverSheet.Cwad.ToString(), Does.Contain("A"));
        Assert.That(coverSheet.ActiveProblems, Has.Count.EqualTo(1));
        Assert.That(coverSheet.ActiveProblems[0].Diagnosis, Is.EqualTo("Essential Hypertension"));
        Assert.That(coverSheet.Allergies, Has.Count.EqualTo(1));
        Assert.That(coverSheet.Allergies[0].Allergen, Is.EqualTo("Penicillin"));
        Assert.That(coverSheet.ActiveOrders, Has.Count.EqualTo(1));
        Assert.That(coverSheet.RecentVisits, Has.Count.EqualTo(1));
        Assert.That(coverSheet.RecentVitals, Has.Count.EqualTo(3));
        Assert.That(coverSheet.LastRefreshed, Is.Not.EqualTo(default(DateTime)));
    }

    [Test]
    public async Task Workflow_OrderEntry_PlaceSignAndDiscontinue()
    {
        // Mirrors ORWDX SAVE → ORWDXA ES → ORWDXA DC
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        await patient.UpdateDemographicsAsync("Order Test Patient", "M", new DateTime(1970, 1, 1), null);

        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        // ORWDX SAVE: place order
        var orderId = await workflow.PlaceOrderAsync(
            "Pharmacy", "Metoprolol 25mg", "DRUG-002",
            "PROV-1", "Dr. Smith", "CL-1", "Primary Care",
            "ROUTINE", "Take twice daily", "Hypertension");

        // Verify pending (ORDER STATUS #5 = PENDING)
        var pending = await workflow.GetOrdersByFilterAsync(7); // 7 = Pending
        Assert.That(pending, Has.Count.EqualTo(1));
        Assert.That(pending[0].OrderText, Is.EqualTo("Metoprolol 25mg"));

        // ORWDXA ES: sign order → auto-releases to Active
        await workflow.SignOrderAsync(orderId, "DR_SMITH_ESIG");

        // Verify active (ORDER STATUS #6 = ACTIVE)
        var active = await workflow.GetOrdersByFilterAsync(2); // 2 = Current
        Assert.That(active, Has.Count.EqualTo(1));
        Assert.That(active[0].Status, Is.EqualTo("Active"));

        // ORWDXA DC: discontinue order
        await workflow.DiscontinueOrderAsync(orderId, "No longer needed");

        // Verify discontinued (ORDER STATUS #1 = DISCONTINUED)
        var discontinued = await workflow.GetOrdersByFilterAsync(3); // 3 = Discontinued
        Assert.That(discontinued, Has.Count.EqualTo(1));
        Assert.That(discontinued[0].Status, Is.EqualTo("Discontinued"));

        // Current should now be empty
        var currentAfterDc = await workflow.GetOrdersByFilterAsync(2);
        Assert.That(currentAfterDc, Has.Count.EqualTo(0));
    }

    [Test]
    public async Task Workflow_OrderHoldAndRelease()
    {
        // Mirrors ORWDXA HOLD / UNHOLD
        // Per ORDER STATUS #3: "Pharmacy orders may be placed on hold,
        // but Lab orders cannot be placed on hold."
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        await patient.UpdateDemographicsAsync("Hold Test Patient", "F", new DateTime(1985, 6, 15), null);

        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        var orderId = await workflow.PlaceOrderAsync(
            "Pharmacy", "Lisinopril 10mg", "DRUG-001",
            "PROV-1", "Dr. Smith", null, null, "ROUTINE", null, null);

        await workflow.SignOrderAsync(orderId, "ESIG");

        // ORWDXA HOLD
        await workflow.HoldOrderAsync(orderId);
        var held = await workflow.GetOrdersByFilterAsync(2); // Current includes Hold
        Assert.That(held, Has.Count.EqualTo(1));
        Assert.That(held[0].Status, Is.EqualTo("Hold"));

        // ORWDXA UNHOLD
        await workflow.ReleaseOrderAsync(orderId);
        var released = await workflow.GetOrdersByFilterAsync(2);
        Assert.That(released, Has.Count.EqualTo(1));
        Assert.That(released[0].Status, Is.EqualTo("Active"));
    }

    [Test]
    public async Task Workflow_AppointmentCheckIn_MirrorsSDAM2()
    {
        // Mirrors SDAM2 ONE (check-in) with SDAMEVT BEFORE/AFTER event capture
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        await patient.UpdateDemographicsAsync("CheckIn Patient", "M", new DateTime(1960, 12, 1), null);

        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        // SDAMEVT MAKE event #1
        var apptId = await workflow.ScheduleAppointmentAsync(
            "CL-PC", "Primary Care", DateTime.UtcNow.AddHours(1),
            30, "PROV-1", "Dr. Jones", "Annual Exam", "ROUTINE");

        var upcoming = await workflow.GetUpcomingAppointmentsAsync();
        Assert.That(upcoming, Has.Count.EqualTo(1));
        Assert.That(upcoming[0].Status, Is.EqualTo("Scheduled"));

        // SDAM2 ONE: check in
        await workflow.CheckInAsync(apptId, DateTime.UtcNow);
        var afterCi = await workflow.GetUpcomingAppointmentsAsync();
        Assert.That(afterCi[0].Status, Is.EqualTo("Checked In"));

        // Check out and complete
        await workflow.CheckOutAsync(apptId, DateTime.UtcNow);
        var completedAppt = await workflow.GetAppointmentAsync(apptId);
        Assert.That(completedAppt.Status, Is.EqualTo("Completed"));
    }

    [Test]
    public async Task Workflow_AppointmentCancel_MirrorsSDAMEVT()
    {
        // Mirrors SDAMEVT CANCEL event #2
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        await patient.UpdateDemographicsAsync("Cancel Patient", "F", new DateTime(1990, 7, 4), null);

        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        var apptId = await workflow.ScheduleAppointmentAsync(
            "CL-CARD", "Cardiology", DateTime.UtcNow.AddDays(3),
            45, "PROV-2", "Dr. Heart", "Consult", "ROUTINE");

        await workflow.CancelAppointmentAsync(apptId);

        var appt = _cluster.GrainFactory.GetGrain<IAppointmentGrain>(apptId);
        var state = await appt.GetAppointmentAsync();
        Assert.That(state.Status, Is.EqualTo("Cancelled"));
    }

    [Test]
    public async Task Workflow_AppointmentNoShow_MirrorsSDAMEVT()
    {
        // Mirrors SDAMEVT NOSHOW event #3
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        await patient.UpdateDemographicsAsync("NoShow Patient", "M", new DateTime(1975, 3, 15), null);

        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        var apptId = await workflow.ScheduleAppointmentAsync(
            "CL-MH", "Mental Health", DateTime.UtcNow.AddDays(-1),
            60, "PROV-3", "Dr. Mind", "Screening", "ROUTINE");

        await workflow.NoShowAppointmentAsync(apptId);

        var appt = _cluster.GrainFactory.GetGrain<IAppointmentGrain>(apptId);
        var state = await appt.GetAppointmentAsync();
        Assert.That(state.Status, Is.EqualTo("No-Show"));
    }

    [Test]
    public async Task Workflow_ProblemList_AddAndInactivate()
    {
        // Mirrors GMPLSAVE EN (save) and GMPLEDIT status change A→I
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        await patient.UpdateDemographicsAsync("Problem Patient", "M", new DateTime(1950, 11, 11), null);

        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        // GMPLSAVE: add two problems
        var prob1 = await workflow.AddProblemAsync(
            "Type 2 Diabetes", "E11.9", "CHRONIC", "ROUTINE",
            new DateTime(2015, 6, 1), "PROV-1", "Dr. Smith",
            "CL-1", "Primary Care", false, "Diet controlled");

        var prob2 = await workflow.AddProblemAsync(
            "Acute Bronchitis", "J20.9", "ACUTE", "ROUTINE",
            DateTime.UtcNow.AddDays(-5), "PROV-1", "Dr. Smith",
            "CL-1", "Primary Care", false, null);

        var active = await workflow.GetActiveProblemsAsync();
        Assert.That(active, Has.Count.EqualTo(2));

        // GMPLSAVE status change with audit: A → I
        await workflow.InactivateProblemAsync(prob2, DateTime.UtcNow);

        var activeAfter = await workflow.GetActiveProblemsAsync();
        Assert.That(activeAfter, Has.Count.EqualTo(1));
        Assert.That(activeAfter[0].Diagnosis, Is.EqualTo("Type 2 Diabetes"));

        var all = await workflow.GetAllProblemsAsync();
        Assert.That(all, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Workflow_VitalsEntry_RecordAndRetrieveLatest()
    {
        // Mirrors GMRVED vitals entry into file 120.5
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        await patient.UpdateDemographicsAsync("Vitals Patient", "F", new DateTime(1988, 2, 14), null);

        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

        // Record first set of vitals
        await workflow.RecordVitalsAsync(
            "CL-1", "Primary Care", "USR-1", "Nurse Kim",
            DateTime.UtcNow.AddHours(-2),
            new Dictionary<string, string>
            {
                { "BLOOD PRESSURE", "120/80" },
                { "PULSE", "68" },
                { "TEMPERATURE", "98.4" },
                { "RESPIRATION", "16" },
                { "PAIN", "0" }
            }, null);

        // Record a second BP reading later
        await workflow.RecordVitalsAsync(
            "CL-1", "Primary Care", "USR-1", "Nurse Kim",
            DateTime.UtcNow,
            new Dictionary<string, string>
            {
                { "BLOOD PRESSURE", "118/76" }
            }, null);

        // Get latest — should show most recent per type
        var latest = await workflow.GetLatestVitalsAsync();

        // Should have 5 unique vital types, with BP being the latest reading
        Assert.That(latest, Has.Count.EqualTo(5));
        var latestBp = latest.First(v => v.VitalType == "BLOOD PRESSURE");
        Assert.That(latestBp.Value, Is.EqualTo("118/76"));
    }

    [Test]
    public async Task Workflow_PatientInfo_MirrorsORWPTSELECT()
    {
        // Mirrors ORWPT SELECT return format:
        // NAME^SEX^DOB^SSN^LOC^WARD^RMBD^CWAD^SENSITIVE^ADMITTED^SC^SC%^ICN^AGE
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        await patient.UpdateDemographicsAsync("Smith,John A", "M", new DateTime(1970, 6, 15), "123-45-6789");
        await patient.UpdateVeteranInfoAsync("Y", 50, null, null);

        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        var info = await workflow.GetPatientInfoAsync();

        Assert.That(info.Name, Is.EqualTo("Smith,John A"));
        Assert.That(info.Sex, Is.EqualTo("M"));
        Assert.That(info.DateOfBirth, Is.EqualTo(new DateTime(1970, 6, 15)));
        Assert.That(info.Ssn, Is.EqualTo("123-45-6789"));
        Assert.That(info.IsVeteran, Is.True);
        Assert.That(info.IsServiceConnected, Is.True);
        Assert.That(info.ServiceConnectedPercent, Is.EqualTo(50));
        Assert.That(info.Age, Is.GreaterThan(50));
        Assert.That(info.IsAdmitted, Is.False);
    }

    [Test]
    public async Task Workflow_CompleteClinicVisit()
    {
        // Full clinic visit workflow derived from CPRS workflows:
        // 1. Schedule appointment (SDAMEVT MAKE)
        // 2. Check in (SDAM2 ONE)
        // 3. Record vitals (GMRVED)
        // 4. Update problems (GMPLSAVE)
        // 5. Place orders (ORWDX SAVE)
        // 6. Record allergy (CWAD update)
        // 7. Check out (SDAMEVT)
        // 8. View cover sheet (ORWCV)
        var patientId = $"PATIENT-{Guid.NewGuid()}";
        var patient = _cluster.GrainFactory.GetGrain<IPatientGrain>(patientId);
        await patient.UpdateDemographicsAsync("Visit,Complete", "M", new DateTime(1955, 8, 22), "999-88-7777");
        await patient.UpdateVeteranInfoAsync("Y", 70, null, null);

        var workflow = _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);
        var now = DateTime.UtcNow;

        // Step 1: Schedule
        var apptId = await workflow.ScheduleAppointmentAsync(
            "CL-PC", "Primary Care", now.AddMinutes(-30),
            30, "PROV-1", "Dr. Smith", "Follow-up", "FOLLOW_UP");

        // Step 2: Check in
        await workflow.CheckInAsync(apptId, now.AddMinutes(-25));

        // Step 3: Vitals
        await workflow.RecordVitalsAsync(
            "CL-PC", "Primary Care", "NURSE-1", "Nurse Kim", now.AddMinutes(-20),
            new Dictionary<string, string>
            {
                { "BLOOD PRESSURE", "150/95" },
                { "PULSE", "78" },
                { "WEIGHT", "210" },
                { "HEIGHT", "70" }
            },
            new Dictionary<string, List<string>>
            {
                { "BLOOD PRESSURE", new List<string> { "SITTING" } }
            });

        // Step 4: Problem
        await workflow.AddProblemAsync(
            "Uncontrolled Hypertension", "I10", "CHRONIC", "ACUTE",
            now, "PROV-1", "Dr. Smith", "CL-PC", "Primary Care", true, "BP elevated");

        // Step 5: Order
        var labOrderId = await workflow.PlaceOrderAsync(
            "Lab", "Basic Metabolic Panel", "LAB-BMP",
            "PROV-1", "Dr. Smith", "CL-PC", "Primary Care",
            "ROUTINE", null, "Evaluate renal function");
        await workflow.SignOrderAsync(labOrderId, "SMITH_ESIG");

        // Step 6: Allergy
        await workflow.RecordAllergyAsync(
            "Lisinopril", "Drug", null, "O",
            new List<string> { "Cough" }, "Mild", "PROV-1", "Dr. Smith", "Dry cough noted");

        // Step 7: Check out
        await workflow.CheckOutAsync(apptId, now);

        // Step 8: Cover sheet
        var coverSheet = await workflow.GetCoverSheetAsync();

        // Verify the complete clinical picture
        Assert.That(coverSheet.Demographics.Name, Is.EqualTo("Visit,Complete"));
        Assert.That(coverSheet.Demographics.IsVeteran, Is.True);
        Assert.That(coverSheet.Demographics.ServiceConnectedPercent, Is.EqualTo(70));
        Assert.That(coverSheet.Cwad.HasAllergies, Is.True);
        Assert.That(coverSheet.ActiveProblems, Has.Count.EqualTo(1));
        Assert.That(coverSheet.ActiveProblems[0].IsServiceConnected, Is.True);
        Assert.That(coverSheet.Allergies, Has.Count.EqualTo(1));
        Assert.That(coverSheet.Allergies[0].Allergen, Is.EqualTo("Lisinopril"));
        Assert.That(coverSheet.ActiveOrders, Has.Count.EqualTo(1));
        Assert.That(coverSheet.RecentVitals, Has.Count.EqualTo(4));
        Assert.That(coverSheet.RecentVisits, Has.Count.EqualTo(1));
        Assert.That(coverSheet.RecentVisits[0].Status, Is.EqualTo("Completed"));
    }
}

