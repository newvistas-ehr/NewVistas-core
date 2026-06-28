// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.EventSourcing;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents the state of a Patient grain based on VistA PATIENT file (#2).
///
/// Inherits the clinical event-sourcing outbox from <see cref="EventSourcedStateBase"/>
/// so that causal events for embedded clinical data (problems, allergies, immunizations,
/// etc.) are persisted atomically with the projection in a single <c>WriteStateAsync</c>.
/// </summary>
[GenerateSerializer]
public class PatientState : EventSourcedStateBase
{
    /// <summary>
    /// Patient Internal Entry Number (IEN)
    /// </summary>
    [Id(0)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Patient Name (.01)
    /// </summary>
    [Id(1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Patient Sex (.02) - M or F
    /// </summary>
    [Id(2)]
    public string Sex { get; set; } = string.Empty;

    /// <summary>
    /// Date of Birth (.03)
    /// </summary>
    [Id(3)]
    public DateTime? DateOfBirth { get; set; }

    /// <summary>
    /// Social Security Number (.09)
    /// </summary>
    [Id(4)]
    public string SocialSecurityNumber { get; set; } = string.Empty;

    /// <summary>
    /// Current Admission (.105)
    /// </summary>
    [Id(5)]
    public string? CurrentAdmission { get; set; }

    /// <summary>
    /// Room-Bed (.108)
    /// </summary>
    [Id(6)]
    public string? RoomBed { get; set; }

    /// <summary>
    /// Current Movement (.1)
    /// </summary>
    [Id(7)]
    public string? CurrentMovement { get; set; }

    /// <summary>
    /// Veteran (Y/N) (.361)
    /// </summary>
    [Id(8)]
    public string Veteran { get; set; } = string.Empty;

    /// <summary>
    /// Service Connected (%) (.302)
    /// </summary>
    [Id(9)]
    public int? ServiceConnectedPercentage { get; set; }

    /// <summary>
    /// Current Means Test Status (.14)
    /// </summary>
    [Id(10)]
    public string? CurrentMeansTestStatus { get; set; }

    /// <summary>
    /// Date of Death (.351)
    /// </summary>
    [Id(11)]
    public DateTime? DateOfDeath { get; set; }

    /// <summary>
    /// Place of Birth - City (.112)
    /// </summary>
    [Id(12)]
    public string? BirthCity { get; set; }

    /// <summary>
    /// Place of Birth - State (.113)
    /// </summary>
    [Id(13)]
    public string? BirthState { get; set; }

    /// <summary>
    /// Marital Status (.05)
    /// </summary>
    [Id(14)]
    public string? MaritalStatus { get; set; }

    /// <summary>
    /// Religious Preference (.06)
    /// </summary>
    [Id(15)]
    public string? ReligiousPreference { get; set; }

    /// <summary>
    /// Race (.02) - Multiple
    /// </summary>
    [Id(16)]
    public List<string> Race { get; set; } = new();

    /// <summary>
    /// Ethnicity Information (.06) - Multiple
    /// </summary>
    [Id(17)]
    public List<string> Ethnicity { get; set; } = new();

    /// <summary>
    /// Street Address Line 1 (.111)
    /// </summary>
    [Id(18)]
    public string? StreetAddress1 { get; set; }

    /// <summary>
    /// Street Address Line 2 (.112)
    /// </summary>
    [Id(19)]
    public string? StreetAddress2 { get; set; }

    /// <summary>
    /// Street Address Line 3 (.113)
    /// </summary>
    [Id(20)]
    public string? StreetAddress3 { get; set; }

    /// <summary>
    /// City (.114)
    /// </summary>
    [Id(21)]
    public string? City { get; set; }

    /// <summary>
    /// State (.115)
    /// </summary>
    [Id(22)]
    public string? State { get; set; }

    /// <summary>
    /// Zip Code (.116)
    /// </summary>
    [Id(23)]
    public string? ZipCode { get; set; }

    /// <summary>
    /// Phone Number (Residence) (.131)
    /// </summary>
    [Id(24)]
    public string? PhoneNumberResidence { get; set; }

    /// <summary>
    /// Phone Number (Work) (.132)
    /// </summary>
    [Id(25)]
    public string? PhoneNumberWork { get; set; }

    /// <summary>
    /// Email Address (.133)
    /// </summary>
    [Id(26)]
    public string? Email { get; set; }

    /// <summary>
    /// Emergency Contact Name (.331)
    /// </summary>
    [Id(27)]
    public string? EmergencyContactName { get; set; }

    /// <summary>
    /// Emergency Contact Relationship (.332)
    /// </summary>
    [Id(28)]
    public string? EmergencyContactRelationship { get; set; }

    /// <summary>
    /// Emergency Contact Phone (.333)
    /// </summary>
    [Id(29)]
    public string? EmergencyContactPhone { get; set; }

    /// <summary>
    /// Appointment Date/Time
    /// </summary>
    [Id(30)]
    public List<DateTime> Appointments { get; set; } = new();

    /// <summary>
    /// Eligibility Code (.361)
    /// </summary>
    [Id(31)]
    public string? EligibilityCode { get; set; }

    /// <summary>
    /// Primary Eligibility Code (.361)
    /// </summary>
    [Id(32)]
    public string? PrimaryEligibilityCode { get; set; }

    /// <summary>
    /// Service Entry Date (.321)
    /// </summary>
    [Id(33)]
    public DateTime? ServiceEntryDate { get; set; }

    /// <summary>
    /// Service Separation Date (.322)
    /// </summary>
    [Id(34)]
    public DateTime? ServiceSeparationDate { get; set; }

    /// <summary>
    /// Service Branch (.323)
    /// </summary>
    [Id(35)]
    public string? ServiceBranch { get; set; }

    /// <summary>
    /// Service Discharge Type (.324)
    /// </summary>
    [Id(36)]
    public string? ServiceDischargeType { get; set; }

    /// <summary>
    /// Prisoner of War (Y/N) (.325)
    /// </summary>
    [Id(37)]
    public string? PrisonerOfWar { get; set; }

    /// <summary>
    /// Date Record Created
    /// </summary>
    [Id(38)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date Record Last Modified
    /// </summary>
    [Id(39)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Indicates if patient is active
    /// </summary>
    [Id(40)]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Collection of Appointment IDs for this patient
    /// Reference to SDEC APPOINTMENT file (#409.84)
    /// </summary>
    [Id(41)]
    public List<string> AppointmentIds { get; set; } = new();

    /// <summary>
    /// Collection of Lab Test IDs for this patient
    /// Reference to LAB DATA file (#63)
    /// </summary>
    [Id(42)]
    public List<string> LabTestIds { get; set; } = new();

    /// <summary>
    /// Collection of Order IDs for this patient
    /// Reference to ORDER file (#100)
    /// </summary>
    [Id(43)]
    public List<string> OrderIds { get; set; } = new();

    /// <summary>
    /// Legacy allergy ID list — no longer used. Kept for serialization compatibility.
    /// </summary>
    [Id(44)]
    public List<string> AllergyIds { get; set; } = new();

    /// <summary>
    /// Embedded allergy entries for this patient.
    /// VistA PATIENT ALLERGIES file (#120.8) — stored directly on the patient grain.
    /// </summary>
    [Id(67)]
    public List<AllergyEntry> Allergies { get; set; } = new();

    /// <summary>
    /// Embedded problem entries for this patient.
    /// VistA PROBLEM LIST file (#9000011) — stored directly on the patient grain.
    /// </summary>
    [Id(68)]
    public List<ProblemEntry> Problems { get; set; } = new();

    /// <summary>
    /// Embedded immunization entries for this patient.
    /// VistA V IMMUNIZATION file (#9000010.11) — stored directly on the patient grain.
    /// </summary>
    [Id(69)]
    public List<ImmunizationEntry> Immunizations { get; set; } = new();

    /// <summary>
    /// Embedded service connected condition entries for this patient.
    /// VistA SERVICE CONNECTED CONDITIONS file (#2.04) — stored directly on the patient grain.
    /// </summary>
    [Id(70)]
    public List<ScConditionEntry> ScConditions { get; set; } = new();

    /// <summary>
    /// Embedded diet order entries for this patient.
    /// VistA FH PATIENT file (#115.2) — stored directly on the patient grain.
    /// </summary>
    [Id(71)]
    public List<DieteticsEntry> DietOrders { get; set; } = new();

    /// <summary>
    /// Embedded prosthetics item entries for this patient.
    /// VistA PROSTHETICS file (#669.1) — stored directly on the patient grain.
    /// </summary>
    [Id(72)]
    public List<ProstheticsEntry> ProstheticsItems { get; set; } = new();

    /// <summary>
    /// Embedded means test entries for this patient.
    /// VistA ANNUAL MEANS TEST file (#408.31) — stored directly on the patient grain.
    /// </summary>
    [Id(73)]
    public List<MeansTestEntry> MeansTests { get; set; } = new();

    /// <summary>
    /// Recent vitals cache — the last N vital summaries for the cover sheet.
    /// Maintained by the workflow grain on record; trimmed to site parameter VitalsDisplayCount.
    /// Eliminates fan-out reads for the common "show latest vitals" query.
    /// </summary>
    [Id(74)]
    public List<VitalSummary> RecentVitals { get; set; } = new();

    /// <summary>
    /// Recent orders cache — the last N order summaries for the cover sheet.
    /// Maintained by the workflow grain on placement; trimmed to site parameter OrdersDisplayCount.
    /// Eliminates fan-out reads for the common "show current orders" query.
    /// </summary>
    [Id(75)]
    public List<OrderSummary> RecentOrders { get; set; } = new();

    /// <summary>
    /// Recent notes cache — last N TIU document summaries for cover sheet display.
    /// Maintained by the workflow grain on note creation/signing; trimmed to site parameter NotesDisplayCount.
    /// Eliminates fan-out reads for the common "show recent notes" query.
    /// </summary>
    [Id(76)]
    public List<TiuNoteSummary> RecentNotes { get; set; } = new();

    /// <summary>
    /// If this patient was merged into another, stores the surviving patient's ID.
    /// Null if never merged. Maps to VistA DG MERGE file (#15.1).
    /// </summary>
    [Id(77)]
    public string? MergedIntoPatientId { get; set; }

    /// <summary>
    /// Date/time this patient was merged into the surviving patient.
    /// </summary>
    [Id(78)]
    public DateTime? MergeDate { get; set; }

    /// <summary>
    /// User who authorized the merge.
    /// </summary>
    [Id(79)]
    public string? MergedByUserId { get; set; }

    /// <summary>
    /// Domains (PatientHistoryDomains constants) whose full ID history has
    /// been flushed to the per-domain IPatientHistoryIndexGrain. A domain's
    /// ID list in this state is NEVER trimmed until its domain appears here —
    /// the crash-safety invariant of the lazy capping migration.
    /// Allergies are never capped and never appear here.
    /// </summary>
    [Id(80)]
    public HashSet<string> HistoryMigratedDomains { get; set; } = new();

    /// <summary>
    /// Legacy problem ID list — no longer used. Kept for serialization compatibility.
    /// </summary>
    [Id(45)]
    public List<string> ProblemIds { get; set; } = new();

    /// <summary>
    /// Collection of Prescription IDs � PRESCRIPTION file (#52)
    /// </summary>
    [Id(46)]
    public List<string> PharmacyIds { get; set; } = new();

    /// <summary>
    /// Collection of BCMA Administration IDs � BCMA MEDICATION LOG (#53.79)
    /// </summary>
    [Id(47)]
    public List<string> BcmaIds { get; set; } = new();

    /// <summary>
    /// Collection of Radiology IDs � RAD/NUC MED ORDERS file (#75.1)
    /// </summary>
    [Id(48)]
    public List<string> RadiologyIds { get; set; } = new();

    /// <summary>
    /// Legacy vital ID list — no longer used for reads. Kept for serialization compatibility.
    /// Vitals now use RecentVitals (embedded cache) + PatientVitalIndexGrain (full history).
    /// </summary>
    [Id(49)]
    public List<string> VitalIds { get; set; } = new();

    /// <summary>
    /// Collection of TIU Document IDs � TIU DOCUMENT file (#8925)
    /// </summary>
    [Id(50)]
    public List<string> TiuDocumentIds { get; set; } = new();

    /// <summary>
    /// Collection of Consult IDs � REQUEST/CONSULTATION file (#123)
    /// </summary>
    [Id(51)]
    public List<string> ConsultIds { get; set; } = new();

    /// <summary>
    /// Collection of Surgery IDs � SURGERY file (#130)
    /// </summary>
    [Id(52)]
    public List<string> SurgeryIds { get; set; } = new();

    /// <summary>
    /// Collection of Clinical Reminder IDs � CLINICAL REMINDER file (#811.9)
    /// </summary>
    [Id(53)]
    public List<string> ClinicalReminderIds { get; set; } = new();

    /// <summary>
    /// Collection of Immunization IDs � V IMMUNIZATION file (#9000010.11)
    /// </summary>
    /// Legacy immunization ID list — no longer used. Kept for serialization compatibility.
    [Id(54)]
    public List<string> ImmunizationIds { get; set; } = new();

    /// <summary>
    /// Collection of Health Factor IDs � V HEALTH FACTORS file (#9000010.23)
    /// </summary>
    [Id(55)]
    public List<string> HealthFactorIds { get; set; } = new();

    /// <summary>
    /// Collection of Mental Health Instrument IDs � YS MH INSTRUMENT (#601.71)
    /// </summary>
    [Id(56)]
    public List<string> MentalHealthIds { get; set; } = new();

    /// <summary>
    /// Collection of Dietetics IDs � FH PATIENT file (#115.2)
    /// </summary>
    /// Legacy dietetics ID list — no longer used. Kept for serialization compatibility.
    [Id(57)]
    public List<string> DieteticsIds { get; set; } = new();

    /// <summary>
    /// Collection of Prosthetics IDs � PROSTHETICS file (#669.1)
    /// </summary>
    /// Legacy prosthetics ID list — no longer used. Kept for serialization compatibility.
    [Id(58)]
    public List<string> ProstheticsIds { get; set; } = new();

    /// <summary>
    /// Collection of Imaging IDs � IMAGE file (#2005)
    /// </summary>
    [Id(59)]
    public List<string> ImagingIds { get; set; } = new();

    /// <summary>
    /// Collection of ADT Movement IDs � PATIENT MOVEMENT file (#405)
    /// </summary>
    [Id(60)]
    public List<string> AdtIds { get; set; } = new();

    /// <summary>
    /// Collection of Means Test IDs � ANNUAL MEANS TEST file (#408.31)
    /// </summary>
    /// Legacy means test ID list — no longer used. Kept for serialization compatibility.
    [Id(61)]
    public List<string> MeansTestIds { get; set; } = new();

    /// <summary>
    /// Collection of Service Connected Condition IDs (#2.04)
    /// </summary>
    /// Legacy SC condition ID list — no longer used. Kept for serialization compatibility.
    [Id(62)]
    public List<string> ServiceConnectedConditionIds { get; set; } = new();

    /// <summary>
    /// VistA Data File Number — the local facility IEN in PATIENT file (#2).
    /// Assigned at registration. Used as the primary human-readable patient identifier
    /// within a facility (e.g., "100022"). The Orleans grain key may or may not match this;
    /// in production deployments the grain key should equal the DFN.
    /// </summary>
    [Id(63)]
    public string? Dfn { get; set; }

    /// <summary>
    /// Integration Control Number — the national cross-facility identifier assigned
    /// by the VA Master Patient Index (MPI). Format: {10-digit}V{6-digit}
    /// (e.g., "1234567890V123456"). Null until MPI correlation completes (MPIF001.m GETICN).
    /// </summary>
    [Id(64)]
    public string? Icn { get; set; }

    /// <summary>
    /// Quick flag indicating whether this patient record is marked as sensitive.
    /// Mirrors PAC grain for cover sheet display. (DG SENSITIVITY .01)
    /// </summary>
    [Id(65)]
    public bool IsSensitiveRecord { get; set; }

    /// <summary>
    /// Sensitivity level — mirrors PAC grain for fast lookup.
    /// "STANDARD", "ELEVATED", "HIGH"
    /// </summary>
    [Id(66)]
    public string? SensitivityLevel { get; set; }

    /// <summary>
    /// The patient's preferred (default) OUTPATIENT pharmacy — a PharmacyId in the pharmacy
    /// directory. New outpatient prescriptions default to it; the provider can override per-Rx.
    /// Part of the EXTERNAL_PHARMACY enhancement (see <see cref="SiteFeatures"/>); inpatient
    /// orders always go to the hospital pharmacy and ignore this.
    /// </summary>
    [Id(81)]
    public string? PreferredPharmacyId { get; set; }

    /// <summary>Cached display name of the preferred pharmacy, denormalized for quick display.</summary>
    [Id(82)]
    public string? PreferredPharmacyName { get; set; }
}
