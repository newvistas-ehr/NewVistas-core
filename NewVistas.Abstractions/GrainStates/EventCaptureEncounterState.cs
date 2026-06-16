// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Encounter type for Event Capture workload classification.
/// Maps to VistA TYPE OF VISIT field in File #721.3.
/// </summary>
[GenerateSerializer]
public enum EcEncounterType
{
    /// <summary>Outpatient ambulatory visit.</summary>
    Outpatient = 0,
    /// <summary>Inpatient ward-based workload.</summary>
    Inpatient = 1,
    /// <summary>Day hospital / day case procedure.</summary>
    Daycase = 2,
    /// <summary>Telephone encounter (non-face-to-face).</summary>
    Telephone = 3,
    /// <summary>Nursing home care unit.</summary>
    NursingHome = 4,
    /// <summary>Domiciliary care.</summary>
    Domiciliary = 5,
    /// <summary>Home-based care.</summary>
    HomeBased = 6,
    /// <summary>Compensation and pension (C&amp;P) examination.</summary>
    CompensationAndPension = 7,
}

/// <summary>
/// Patient eligibility/category for workload reporting.
/// Maps to VistA PATIENT CATEGORY field in File #721.3.
/// </summary>
[GenerateSerializer]
public enum EcPatientCategory
{
    /// <summary>Service-connected veteran.</summary>
    ServiceConnected = 0,
    /// <summary>Non-service-connected veteran.</summary>
    NonServiceConnected = 1,
    /// <summary>Medicare beneficiary.</summary>
    Medicare = 2,
    /// <summary>Medicaid beneficiary.</summary>
    Medicaid = 3,
    /// <summary>Active duty military.</summary>
    ActiveDuty = 4,
    /// <summary>Humanitarian care.</summary>
    Humanitarian = 5,
    /// <summary>Research participant.</summary>
    Research = 6,
    /// <summary>Other / unclassified.</summary>
    Other = 7,
}

/// <summary>
/// Status of an Event Capture encounter record.
/// </summary>
[GenerateSerializer]
public enum EcEncounterStatus
{
    /// <summary>Encounter is open/in progress.</summary>
    Open = 0,
    /// <summary>Encounter is complete (checked out).</summary>
    Complete = 1,
    /// <summary>Encounter has been soft-deleted.</summary>
    Deleted = 2,
}

/// <summary>
/// A procedure captured within an encounter record.
/// Corresponds to VistA File #721.3 PROCEDURE sub-file.
/// </summary>
[GenerateSerializer]
public class EcProcedureEntry
{
    /// <summary>
    /// CPT code (.01) — Current Procedural Terminology code for the service rendered.
    /// </summary>
    [Id(0)]
    public string CptCode { get; set; } = string.Empty;

    /// <summary>
    /// Procedure description — human-readable name for the CPT code.
    /// </summary>
    [Id(1)]
    public string ProcedureDescription { get; set; } = string.Empty;

    /// <summary>
    /// Quantity (.03) — number of times the procedure was performed.
    /// </summary>
    [Id(2)]
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Provider ID (.04) — provider who performed the procedure.
    /// </summary>
    [Id(3)]
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>
    /// Provider name — display name of the performing provider.
    /// </summary>
    [Id(4)]
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Modifier code (.05) — CPT modifier (e.g., -25, -59) if applicable.
    /// </summary>
    [Id(5)]
    public string? ModifierCode { get; set; }

    /// <summary>Date/time this procedure entry was added.</summary>
    [Id(6)]
    public DateTime AddedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A diagnosis associated with an encounter.
/// Corresponds to ICD-10 code entries on the encounter record.
/// </summary>
[GenerateSerializer]
public class EcDiagnosisEntry
{
    /// <summary>
    /// ICD-10 code (.01) — diagnosis code for the encounter.
    /// </summary>
    [Id(0)]
    public string Icd10Code { get; set; } = string.Empty;

    /// <summary>
    /// Diagnosis description — human-readable description.
    /// </summary>
    [Id(1)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Is primary (.02) — true if this is the primary/principal diagnosis.
    /// </summary>
    [Id(2)]
    public bool IsPrimary { get; set; }
}

/// <summary>
/// Event Capture Encounter State — represents a workload/encounter capture record.
/// Corresponds to VistA File #721 (EC PATIENT) and File #721.3 (EC ENCOUNTER).
/// MUMPS routines: ECPEEN.m (encounter entry), ECPEEU.m (encounter update).
/// </summary>
[GenerateSerializer]
public class EventCaptureEncounterState
{
    /// <summary>
    /// Encounter ID — grain key, format "EC-ENCOUNTER:{guid}".
    /// </summary>
    [Id(0)]
    public string EncounterId { get; set; } = string.Empty;

    /// <summary>
    /// Patient ID (.01) — pointer to PATIENT file (#2).
    /// </summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Encounter date/time (.02) — date and time of the workload event. File #721.3 field (.01).
    /// </summary>
    [Id(2)]
    public DateTime EncounterDateTime { get; set; }

    /// <summary>
    /// DSS unit ID (.03) — pointer to DSS UNIT file (#724). Primary workload attribution unit.
    /// </summary>
    [Id(3)]
    public string DssUnitId { get; set; } = string.Empty;

    /// <summary>
    /// DSS unit name — display name of the DSS unit.
    /// </summary>
    [Id(4)]
    public string DssUnitName { get; set; } = string.Empty;

    /// <summary>
    /// DSS unit code — short 3–6 character code for the unit.
    /// </summary>
    [Id(5)]
    public string? DssUnitCode { get; set; }

    /// <summary>
    /// Clinic ID — pointer to HOSPITAL LOCATION file (#44).
    /// </summary>
    [Id(6)]
    public string? ClinicId { get; set; }

    /// <summary>
    /// Clinic name — display name of the clinic.
    /// </summary>
    [Id(7)]
    public string? ClinicName { get; set; }

    /// <summary>
    /// Location ID — physical location where the encounter occurred.
    /// </summary>
    [Id(8)]
    public string? LocationId { get; set; }

    /// <summary>
    /// Location name — display name of the location.
    /// </summary>
    [Id(9)]
    public string? LocationName { get; set; }

    /// <summary>
    /// Primary provider ID (.04) — provider generating the workload credit.
    /// </summary>
    [Id(10)]
    public string PrimaryProviderId { get; set; } = string.Empty;

    /// <summary>
    /// Primary provider name — display name of the primary provider.
    /// </summary>
    [Id(11)]
    public string PrimaryProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Attending provider ID — supervising/attending physician if different from primary.
    /// </summary>
    [Id(12)]
    public string? AttendingProviderId { get; set; }

    /// <summary>
    /// Attending provider name — display name of the attending provider.
    /// </summary>
    [Id(13)]
    public string? AttendingProviderName { get; set; }

    /// <summary>
    /// Encounter type (.05) — classification of the visit type (Outpatient, Inpatient, etc.).
    /// </summary>
    [Id(14)]
    public EcEncounterType EncounterType { get; set; } = EcEncounterType.Outpatient;

    /// <summary>
    /// Patient category (.06) — eligibility/priority category for workload reporting.
    /// </summary>
    [Id(15)]
    public EcPatientCategory PatientCategory { get; set; } = EcPatientCategory.NonServiceConnected;

    /// <summary>
    /// Primary stop code (.07) — VistA clinic stop code for primary workload credit.
    /// </summary>
    [Id(16)]
    public string? PrimaryStopCode { get; set; }

    /// <summary>
    /// Credit stop code (.08) — secondary stop code for workload credit splitting.
    /// </summary>
    [Id(17)]
    public string? CreditStopCode { get; set; }

    /// <summary>
    /// Status — current state of the encounter record.
    /// </summary>
    [Id(18)]
    public EcEncounterStatus Status { get; set; } = EcEncounterStatus.Open;

    /// <summary>
    /// Check-out date/time — time the patient departed; set on completion.
    /// </summary>
    [Id(19)]
    public DateTime? CheckOutDateTime { get; set; }

    /// <summary>
    /// Visit length (minutes) — calculated or entered visit duration.
    /// </summary>
    [Id(20)]
    public int? VisitLengthMinutes { get; set; }

    /// <summary>
    /// Procedures — list of CPT procedure entries captured during the encounter.
    /// Corresponds to File #721.3 PROCEDURE sub-file.
    /// </summary>
    [Id(21)]
    public List<EcProcedureEntry> Procedures { get; set; } = new();

    /// <summary>
    /// Diagnoses — ICD-10 diagnoses associated with the encounter.
    /// </summary>
    [Id(22)]
    public List<EcDiagnosisEntry> Diagnoses { get; set; } = new();

    /// <summary>
    /// Comments — free-text remarks about the encounter.
    /// </summary>
    [Id(23)]
    public string? Comments { get; set; }

    /// <summary>
    /// Deleted by provider ID — set when the encounter is soft-deleted.
    /// </summary>
    [Id(24)]
    public string? DeletedByProviderId { get; set; }

    /// <summary>
    /// Deleted by provider name — display name of the provider who deleted the record.
    /// </summary>
    [Id(25)]
    public string? DeletedByProviderName { get; set; }

    /// <summary>
    /// Delete reason — reason given for deleting the encounter.
    /// </summary>
    [Id(26)]
    public string? DeleteReason { get; set; }

    /// <summary>
    /// Date deleted — timestamp when the encounter was soft-deleted.
    /// </summary>
    [Id(27)]
    public DateTime? DeletedDate { get; set; }

    /// <summary>
    /// Created date — when this encounter record was created.
    /// </summary>
    [Id(28)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last modified date — updated on every state change.
    /// </summary>
    [Id(29)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Summary projection of an Event Capture encounter for list/search results.
/// </summary>
[GenerateSerializer]
public class EventCaptureEncounterSummary
{
    [Id(0)]
    public string EncounterId { get; set; } = string.Empty;

    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    [Id(2)]
    public DateTime EncounterDateTime { get; set; }

    [Id(3)]
    public string DssUnitName { get; set; } = string.Empty;

    [Id(4)]
    public string? DssUnitCode { get; set; }

    [Id(5)]
    public string? ClinicName { get; set; }

    [Id(6)]
    public string PrimaryProviderName { get; set; } = string.Empty;

    [Id(7)]
    public EcEncounterType EncounterType { get; set; }

    [Id(8)]
    public EcEncounterStatus Status { get; set; }

    [Id(9)]
    public int ProcedureCount { get; set; }
}
