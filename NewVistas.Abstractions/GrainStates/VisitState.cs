// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// A single diagnosis recorded during an encounter (V POV — Purpose of Visit, File #9000010.07).
/// </summary>
[GenerateSerializer]
public class PceEncounterDiagnosis
{
    /// <summary>ICD-10 code (.01)</summary>
    [Id(0)]
    public string Icd10Code { get; set; } = string.Empty;

    /// <summary>Diagnosis description</summary>
    [Id(1)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Primary diagnosis flag (.04)</summary>
    [Id(2)]
    public bool IsPrimary { get; set; }

    /// <summary>Provider who documented this diagnosis (.06)</summary>
    [Id(3)]
    public string? ProviderId { get; set; }

    [Id(4)]
    public string? ProviderName { get; set; }

    /// <summary>Date/time recorded</summary>
    [Id(5)]
    public DateTime RecordedDateTime { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A CPT/HCPCS procedure recorded during an encounter (V CPT, File #9000010.18).
/// </summary>
[GenerateSerializer]
public class PceProcedure
{
    /// <summary>CPT code (.01)</summary>
    [Id(0)]
    public string CptCode { get; set; } = string.Empty;

    /// <summary>Procedure description</summary>
    [Id(1)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Quantity / units (.07)</summary>
    [Id(2)]
    public int Quantity { get; set; } = 1;

    /// <summary>Modifiers (.08) — comma-separated if multiple</summary>
    [Id(3)]
    public string? Modifiers { get; set; }

    /// <summary>Provider who performed procedure (.06)</summary>
    [Id(4)]
    public string? ProviderId { get; set; }

    [Id(5)]
    public string? ProviderName { get; set; }

    /// <summary>Date/time recorded</summary>
    [Id(6)]
    public DateTime RecordedDateTime { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// An encounter provider (PCE PROVIDER, sub-file of Visit).
/// </summary>
[GenerateSerializer]
public class PceEncounterProvider
{
    /// <summary>Provider DUZ (.01)</summary>
    [Id(0)]
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>Provider name</summary>
    [Id(1)]
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>Role: PRIMARY, SECONDARY, ATTENDING, RESIDENT, etc.</summary>
    [Id(2)]
    public string Role { get; set; } = string.Empty;

    /// <summary>Primary provider flag (.04)</summary>
    [Id(3)]
    public bool IsPrimary { get; set; }
}

/// <summary>
/// Represents the state of a Visit grain — VistA PCE VISIT file (#9000010).
/// </summary>
[GenerateSerializer]
public class VisitState
{
    /// <summary>
    /// Visit IEN (grain key)
    /// </summary>
    [Id(0)]
    public string VisitId { get; set; } = string.Empty;

    /// <summary>
    /// Patient (.05) — pointer to PATIENT file (#2)
    /// </summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Visit/Admit Date &amp; Time (.01)
    /// </summary>
    [Id(2)]
    public DateTime VisitDateTime { get; set; }

    /// <summary>
    /// Service Category (.07):
    /// A = Ambulatory, C = Chart Review, E = Event (Historical),
    /// H = Inpatient, I = Intercare, N = Not Found,
    /// O = Occasion of Service, R = Nursing Home, T = Telecommunications
    /// </summary>
    [Id(3)]
    public string ServiceCategory { get; set; } = "A";

    /// <summary>
    /// Hospital Location (.22) — pointer to HOSPITAL LOCATION file (#44)
    /// </summary>
    [Id(4)]
    public string? LocationId { get; set; }

    [Id(5)]
    public string? LocationName { get; set; }

    /// <summary>
    /// Visit Type — NEW or ESTABLISHED
    /// </summary>
    [Id(6)]
    public string? VisitType { get; set; }

    /// <summary>
    /// Primary Stop Code (.25) — DSS Stop Code
    /// </summary>
    [Id(7)]
    public string? StopCode { get; set; }

    /// <summary>
    /// Secondary Stop Code (.26)
    /// </summary>
    [Id(8)]
    public string? SecondaryStopCode { get; set; }

    /// <summary>
    /// Check Out Date &amp; Time (.18)
    /// </summary>
    [Id(9)]
    public DateTime? CheckOutDateTime { get; set; }

    /// <summary>
    /// Primary provider — pointer to NEW PERSON file (#200)
    /// </summary>
    [Id(10)]
    public string? PrimaryProviderId { get; set; }

    [Id(11)]
    public string? PrimaryProviderName { get; set; }

    /// <summary>
    /// Linked Appointment ID — SDEC APPOINTMENT file (#409.84)
    /// </summary>
    [Id(12)]
    public string? LinkedAppointmentId { get; set; }

    /// <summary>
    /// Linked TIU note IDs — TIU DOCUMENT file (#8925)
    /// </summary>
    [Id(13)]
    public List<string> LinkedNoteIds { get; set; } = new();

    /// <summary>
    /// Encounter diagnoses — V POV, sub-file (#9000010.07)
    /// </summary>
    [Id(14)]
    public List<PceEncounterDiagnosis> Diagnoses { get; set; } = new();

    /// <summary>
    /// Encounter procedures — V CPT, sub-file (#9000010.18)
    /// </summary>
    [Id(15)]
    public List<PceProcedure> Procedures { get; set; } = new();

    /// <summary>
    /// Encounter providers — PCE PROVIDER, sub-file
    /// </summary>
    [Id(16)]
    public List<PceEncounterProvider> Providers { get; set; } = new();

    /// <summary>
    /// Visit status: OPEN, CHECKED OUT, CANCELLED
    /// </summary>
    [Id(17)]
    public string Status { get; set; } = "OPEN";

    /// <summary>
    /// Cancellation reason (if Status = CANCELLED)
    /// </summary>
    [Id(18)]
    public string? CancellationReason { get; set; }

    /// <summary>
    /// Free-text comments
    /// </summary>
    [Id(19)]
    public string? Comments { get; set; }

    [Id(20)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(21)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    // ── GAP 7: Billing Treatment Factors (CIDC) ───────────────────────────────

    /// <summary>
    /// Treatment factor: Service Connected (SC) — from VistA UBACore.pas / uPCE.pas
    /// </summary>
    [Id(22)]
    public bool TfServiceConnected { get; set; }

    /// <summary>
    /// Treatment factor: Agent Orange (AO)
    /// </summary>
    [Id(23)]
    public bool TfAgentOrange { get; set; }

    /// <summary>
    /// Treatment factor: Ionizing Radiation (IR)
    /// </summary>
    [Id(24)]
    public bool TfIonizingRadiation { get; set; }

    /// <summary>
    /// Treatment factor: Southwest Asia Conditions (SWAC)
    /// </summary>
    [Id(25)]
    public bool TfSouthwestAsia { get; set; }

    /// <summary>
    /// Treatment factor: Military Sexual Trauma (MST)
    /// </summary>
    [Id(26)]
    public bool TfMst { get; set; }

    /// <summary>
    /// Treatment factor: Head and/or Neck Cancer (HNC)
    /// </summary>
    [Id(27)]
    public bool TfHeadNeckCancer { get; set; }

    /// <summary>
    /// Treatment factor: Combat Veteran (CV)
    /// </summary>
    [Id(28)]
    public bool TfCombatVeteran { get; set; }

    /// <summary>
    /// Treatment factor: Shipboard Hazard and Defense (SHAD)
    /// </summary>
    [Id(29)]
    public bool TfShad { get; set; }

    /// <summary>
    /// Treatment factor: Camp Lejeune (CL)
    /// </summary>
    [Id(30)]
    public bool TfCampLejeune { get; set; }

    // ── GAP 9: Additional PCE Items ───────────────────────────────────────────

    /// <summary>
    /// Health factors recorded at this encounter — V HEALTH FACTORS, sub-file (#9000010.23)
    /// </summary>
    [Id(31)]
    public List<PceHealthFactor> HealthFactors { get; set; } = new();

    /// <summary>
    /// Immunizations administered at this encounter — V IMMUNIZATION, sub-file (#9000010.11)
    /// </summary>
    [Id(32)]
    public List<PceImmunization> Immunizations { get; set; } = new();

    /// <summary>
    /// Exams performed at this encounter — V EXAM, sub-file (#9000010.13)
    /// </summary>
    [Id(33)]
    public List<PceExam> Exams { get; set; } = new();

    /// <summary>
    /// Patient education given at this encounter — V PATIENT ED, sub-file (#9000010.16)
    /// </summary>
    [Id(34)]
    public List<PcePatientEducation> PatientEducation { get; set; } = new();
}

/// <summary>
/// A health factor recorded at an encounter — V HEALTH FACTORS, File #9000010.23
/// </summary>
[GenerateSerializer]
public class PceHealthFactor
{
    /// <summary>Health Factor name (.01) — pointer to HEALTH FACTOR file (#9999999.64)</summary>
    [Id(0)]
    public string HealthFactorName { get; set; } = string.Empty;

    /// <summary>Health Factor IEN</summary>
    [Id(1)]
    public string? HealthFactorId { get; set; }

    /// <summary>Level/Severity (.04): M = MINIMAL, MO = MODERATE, HV = HEAVY/SEVERE</summary>
    [Id(2)]
    public string? Level { get; set; }

    /// <summary>Provider who recorded (.06)</summary>
    [Id(3)]
    public string? ProviderId { get; set; }

    [Id(4)]
    public string? ProviderName { get; set; }

    [Id(5)]
    public DateTime RecordedDateTime { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// An immunization recorded at an encounter — V IMMUNIZATION, File #9000010.11
/// </summary>
[GenerateSerializer]
public class PceImmunization
{
    /// <summary>Immunization (.01) — pointer to IMMUNIZATION file (#9999999.14)</summary>
    [Id(0)]
    public string ImmunizationName { get; set; } = string.Empty;

    [Id(1)]
    public string? ImmunizationId { get; set; }

    /// <summary>Series (.04): P = PARTIAL, C = COMPLETE, B = BOOSTER, 1-8 = SERIES NUMBER</summary>
    [Id(2)]
    public string? Series { get; set; }

    /// <summary>Lot number (.09)</summary>
    [Id(3)]
    public string? LotNumber { get; set; }

    /// <summary>Route (.07): ID = INTRADERMAL, IM = INTRAMUSCULAR, IV = INTRAVENOUS, PO = ORAL, SC = SUBCUTANEOUS</summary>
    [Id(4)]
    public string? Route { get; set; }

    /// <summary>Site (.08): LA = LEFT ARM, RA = RIGHT ARM, etc.</summary>
    [Id(5)]
    public string? Site { get; set; }

    /// <summary>Contraindicated (.06): true if administered despite contraindication</summary>
    [Id(6)]
    public bool IsContraindicated { get; set; }

    [Id(7)]
    public string? ProviderId { get; set; }

    [Id(8)]
    public string? ProviderName { get; set; }

    [Id(9)]
    public DateTime RecordedDateTime { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// An exam performed at an encounter — V EXAM, File #9000010.13
/// </summary>
[GenerateSerializer]
public class PceExam
{
    /// <summary>Exam (.01) — pointer to EXAM file (#9999999.15)</summary>
    [Id(0)]
    public string ExamName { get; set; } = string.Empty;

    [Id(1)]
    public string? ExamId { get; set; }

    /// <summary>Result (.04): A = ABNORMAL, N = NORMAL</summary>
    [Id(2)]
    public string? Result { get; set; }

    [Id(3)]
    public string? ProviderId { get; set; }

    [Id(4)]
    public string? ProviderName { get; set; }

    [Id(5)]
    public DateTime RecordedDateTime { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Patient education given at an encounter — V PATIENT ED, File #9000010.16
/// </summary>
[GenerateSerializer]
public class PcePatientEducation
{
    /// <summary>Education Topic (.01) — pointer to EDUCATION TOPICS file (#9999999.09)</summary>
    [Id(0)]
    public string TopicName { get; set; } = string.Empty;

    [Id(1)]
    public string? TopicId { get; set; }

    /// <summary>Level of Understanding (.04): P = POOR, M = MARGINAL, A = ADEQUATE, G = GOOD, E = EXCELLENT, N/A</summary>
    [Id(2)]
    public string? LevelOfUnderstanding { get; set; }

    [Id(3)]
    public string? ProviderId { get; set; }

    [Id(4)]
    public string? ProviderName { get; set; }

    [Id(5)]
    public DateTime RecordedDateTime { get; set; } = DateTime.UtcNow;
}
