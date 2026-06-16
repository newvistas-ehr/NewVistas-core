// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

// ─── Enumerations ──────────────────────────────────────────────────────────────

/// <summary>
/// SCI/D registry enrollment status.
/// VistA SCI PATIENT file (#154), field STATUS.
/// </summary>
public enum SCIRegistryStatus
{
    Active = 0,
    Inactive = 1,
    Deceased = 2,
    Transferred = 3,
    Excluded = 4
}

/// <summary>
/// Type of spinal cord injury/dysfunction — traumatic vs. non-traumatic.
/// VistA SCI PATIENT file (#154), field TYPE OF INJURY.
/// </summary>
public enum SCIInjuryType
{
    Traumatic = 0,
    NonTraumatic = 1
}

/// <summary>
/// Etiology of the spinal cord injury or dysfunction.
/// VistA SCI PATIENT file (#154), field ETIOLOGY.
/// </summary>
public enum SCIEtiology
{
    // Traumatic causes
    MotorVehicleAccident = 0,
    Fall = 1,
    Violence = 2,
    Sports = 3,
    OtherTraumatic = 4,

    // Non-traumatic causes
    Disease = 10,
    Tumor = 11,
    Vascular = 12,
    Degenerative = 13,
    Iatrogenic = 14,
    CongenitalDevelopmental = 15,
    OtherNonTraumatic = 16,

    Unknown = 99
}

/// <summary>
/// ASIA Impairment Scale grade — American Spinal Injury Association classification.
/// VistA SCI PATIENT file (#154), field ASIA IMPAIRMENT SCALE.
/// </summary>
public enum SCIAisGrade
{
    /// <summary>A — Complete: No motor/sensory function preserved in sacral segments S4-S5.</summary>
    A = 0,
    /// <summary>B — Sensory Incomplete: Sensory but not motor function preserved below NLI including S4-S5.</summary>
    B = 1,
    /// <summary>C — Motor Incomplete: Motor function preserved below NLI with more than half key muscles grade &lt; 3.</summary>
    C = 2,
    /// <summary>D — Motor Incomplete: Motor function preserved below NLI with at least half key muscles grade ≥ 3.</summary>
    D = 3,
    /// <summary>E — Normal: Sensory and motor function normal.</summary>
    E = 4,
    /// <summary>Not Determined / Not Yet Tested.</summary>
    NotDetermined = 5
}

/// <summary>
/// Bladder management method.
/// VistA SCI PATIENT file (#154), field BLADDER MGMT.
/// </summary>
public enum SCIBladderManagement
{
    IntermittentCatheterization = 0,
    IndwellingUrethralCatheter = 1,
    IndwellingSuprapubicCatheter = 2,
    ExternalCondomCatheter = 3,
    ReflexVoiding = 4,
    Crede = 5,
    Valsalva = 6,
    SpontaneousVoiding = 7,
    Colostomy = 8,
    Other = 9
}

/// <summary>
/// Bowel management program method.
/// VistA SCI PATIENT file (#154), field BOWEL PROGRAM.
/// </summary>
public enum SCIBowelProgram
{
    DigitalStimulation = 0,
    Suppository = 1,
    ManualEvacuation = 2,
    NaturalSpontaneous = 3,
    Colostomy = 4,
    MiniEnema = 5,
    Other = 6
}

/// <summary>
/// Locomotion method — primary means of mobility.
/// VistA SCI PATIENT file (#154), field LOCOMOTION.
/// </summary>
public enum SCILocomotionMethod
{
    ManualWheelchair = 0,
    PowerWheelchair = 1,
    AmbulatoryWithDevice = 2,
    AmbulatoryNoDevice = 3,
    Bedridden = 4,
    Other = 5
}

/// <summary>
/// Living situation at time of encounter.
/// VistA SCI PATIENT file (#154), field LIVING SITUATION.
/// </summary>
public enum SCILivingSituation
{
    PrivateHome = 0,
    NursingHome = 1,
    BoardCare = 2,
    FosterHome = 3,
    AssistedLiving = 4,
    HospitalLongTermCare = 5,
    Homeless = 6,
    Other = 7
}

/// <summary>
/// SCI annual encounter type.
/// VistA SCI PATIENT file (#154), sub-file TYPE OF ENCOUNTER.
/// </summary>
public enum SCIEncounterType
{
    Annual = 0,
    ProblemFocused = 1,
    Telephone = 2,
    Telemedicine = 3,
    Transitional = 4
}

// ─── Nested Record Types ───────────────────────────────────────────────────────

/// <summary>
/// Represents a single SCI/D annual review or follow-up encounter.
/// VistA SCI PATIENT file (#154), sub-file ANNUAL DATA (#154.03).
/// </summary>
[GenerateSerializer]
public class SCIAnnualEncounterRecord
{
    /// <summary>Unique identifier for this encounter record.</summary>
    [Id(0)]
    public string EncounterId { get; set; } = string.Empty;

    /// <summary>VA Fiscal Year (e.g., 2025) of this review.</summary>
    [Id(1)]
    public int FiscalYear { get; set; }

    /// <summary>Date the encounter/review was performed.</summary>
    [Id(2)]
    public DateTime EncounterDate { get; set; }

    /// <summary>Type of SCI encounter (Annual, ProblemFocused, Telephone, etc.).</summary>
    [Id(3)]
    public SCIEncounterType EncounterType { get; set; } = SCIEncounterType.Annual;

    /// <summary>ASIA Impairment Scale grade at this encounter.</summary>
    [Id(4)]
    public SCIAisGrade AisGrade { get; set; } = SCIAisGrade.NotDetermined;

    /// <summary>
    /// Neurological level of injury at this encounter (e.g., "C4", "T10", "L2").
    /// May change over time with recovery or deterioration.
    /// </summary>
    [Id(5)]
    public string NeurologicalLevel { get; set; } = string.Empty;

    /// <summary>Number of hospital admissions in the prior year.</summary>
    [Id(6)]
    public int HospitalAdmissions { get; set; }

    /// <summary>Number of urinary tract infections in the prior year.</summary>
    [Id(7)]
    public int UrinaryTractInfections { get; set; }

    /// <summary>Number of pressure injuries (any stage) present or treated in prior year.</summary>
    [Id(8)]
    public int PressureInjuryCount { get; set; }

    /// <summary>Highest pressure injury stage encountered (0 = none, 1-4 = NPUAP staging).</summary>
    [Id(9)]
    public int HighestPressureInjuryStage { get; set; }

    /// <summary>Equipment and assistive technology needs identified at this encounter.</summary>
    [Id(10)]
    public List<string> EquipmentNeeds { get; set; } = new();

    /// <summary>Bladder management method at this encounter.</summary>
    [Id(11)]
    public SCIBladderManagement? BladderManagement { get; set; }

    /// <summary>Bowel management program at this encounter.</summary>
    [Id(12)]
    public SCIBowelProgram? BowelProgram { get; set; }

    /// <summary>Living situation at this encounter.</summary>
    [Id(13)]
    public SCILivingSituation? LivingSituation { get; set; }

    /// <summary>Provider identifier for the encounter.</summary>
    [Id(14)]
    public string? ProviderId { get; set; }

    /// <summary>Provider name for display.</summary>
    [Id(15)]
    public string? ProviderName { get; set; }

    /// <summary>Free-text clinical notes for this encounter.</summary>
    [Id(16)]
    public string? Notes { get; set; }

    /// <summary>Timestamp when this encounter record was created.</summary>
    [Id(17)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}

// ─── Index Entry (used by SCIIndexState) ──────────────────────────────────────

/// <summary>
/// Summary entry for the SCI/D registry index.
/// Stored in the singleton <see cref="SCIIndexState"/>.
/// </summary>
[GenerateSerializer]
public class SCIIndexEntry
{
    /// <summary>Patient identifier.</summary>
    [Id(0)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Date the patient was enrolled in the SCI registry.</summary>
    [Id(1)]
    public DateTime EnrollmentDate { get; set; }

    /// <summary>Current registry enrollment status.</summary>
    [Id(2)]
    public SCIRegistryStatus Status { get; set; } = SCIRegistryStatus.Active;

    /// <summary>Neurological level of injury (most recently recorded).</summary>
    [Id(3)]
    public string NeurologicalLevel { get; set; } = string.Empty;

    /// <summary>ASIA Impairment Scale grade (most recently recorded).</summary>
    [Id(4)]
    public SCIAisGrade AisGrade { get; set; } = SCIAisGrade.NotDetermined;

    /// <summary>SCI center/facility where the patient is followed.</summary>
    [Id(5)]
    public string? SCICenter { get; set; }

    /// <summary>Enrolling provider name.</summary>
    [Id(6)]
    public string? EnrollingProviderName { get; set; }

    /// <summary>Date of spinal cord injury or onset of dysfunction.</summary>
    [Id(7)]
    public DateTime? DateOfInjuryOnset { get; set; }

    /// <summary>Injury type (Traumatic or NonTraumatic).</summary>
    [Id(8)]
    public SCIInjuryType InjuryType { get; set; } = SCIInjuryType.Traumatic;
}

// ─── Main Grain States ─────────────────────────────────────────────────────────

/// <summary>
/// State for the SCI Patient grain — the core SCI/D registry record.
/// VistA SCI PATIENT file (#154).
/// MUMPS routines: SCIRPAU.m (registry patient add/update), SCIRPIV.m (patient inquiry).
/// </summary>
[GenerateSerializer]
public class SCIPatientState
{
    /// <summary>Patient identifier — the grain key. (#154 .01 NAME → pointer to DPT)</summary>
    [Id(0)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Date the patient was enrolled in the SCI/D registry. (#154 .02 DATE OF ENROLLMENT)</summary>
    [Id(1)]
    public DateTime EnrollmentDate { get; set; }

    /// <summary>Current SCI/D registry enrollment status. (#154 STATUS)</summary>
    [Id(2)]
    public SCIRegistryStatus Status { get; set; } = SCIRegistryStatus.Active;

    /// <summary>SCI center/VA facility where patient is primarily followed. (#154 SCI CENTER)</summary>
    [Id(3)]
    public string? SCICenter { get; set; }

    /// <summary>Date of traumatic injury or onset of non-traumatic SCI/D. (#154 DATE OF INJURY/ONSET)</summary>
    [Id(4)]
    public DateTime? DateOfInjuryOnset { get; set; }

    /// <summary>Type of injury: Traumatic or NonTraumatic. (#154 TYPE OF INJURY)</summary>
    [Id(5)]
    public SCIInjuryType InjuryType { get; set; } = SCIInjuryType.Traumatic;

    /// <summary>Specific etiology of the SCI/D. (#154 ETIOLOGY)</summary>
    [Id(6)]
    public SCIEtiology Etiology { get; set; } = SCIEtiology.Unknown;

    /// <summary>
    /// Neurological level of injury at most recent assessment (e.g., "C4", "T10", "L2").
    /// VistA SCI PATIENT file (#154), NEUROLOGICAL LEVEL.
    /// </summary>
    [Id(7)]
    public string NeurologicalLevelOfInjury { get; set; } = string.Empty;

    /// <summary>ASIA Impairment Scale grade at most recent assessment. (#154 ASIA IMPAIRMENT SCALE)</summary>
    [Id(8)]
    public SCIAisGrade AisGrade { get; set; } = SCIAisGrade.NotDetermined;

    /// <summary>Primary ICD-10 diagnosis code for the SCI/D. (#154 PRIMARY DIAGNOSIS)</summary>
    [Id(9)]
    public string? PrimaryDiagnosisCode { get; set; }

    /// <summary>Description of the primary diagnosis. (#154 PRIMARY DIAGNOSIS)</summary>
    [Id(10)]
    public string? PrimaryDiagnosisDescription { get; set; }

    /// <summary>Associated conditions and secondary diagnoses (e.g., "Neurogenic bladder", "Spasticity").</summary>
    [Id(11)]
    public List<string> AssociatedConditions { get; set; } = new();

    /// <summary>Bladder management method. (#154 BLADDER MGMT)</summary>
    [Id(12)]
    public SCIBladderManagement? BladderManagement { get; set; }

    /// <summary>Bowel management program. (#154 BOWEL PROGRAM)</summary>
    [Id(13)]
    public SCIBowelProgram? BowelProgram { get; set; }

    /// <summary>Primary locomotion method. (#154 LOCOMOTION)</summary>
    [Id(14)]
    public SCILocomotionMethod? LocomotionMethod { get; set; }

    /// <summary>Living situation at enrollment or most recent update. (#154 LIVING SITUATION)</summary>
    [Id(15)]
    public SCILivingSituation? LivingSituation { get; set; }

    /// <summary>Provider who enrolled the patient in the registry. (#154 ENROLLING PROVIDER)</summary>
    [Id(16)]
    public string? EnrollingProviderId { get; set; }

    /// <summary>Enrolling provider display name.</summary>
    [Id(17)]
    public string? EnrollingProviderName { get; set; }

    /// <summary>Primary SCI/D provider responsible for ongoing care. (#154 PRIMARY PROVIDER)</summary>
    [Id(18)]
    public string? PrimaryProviderId { get; set; }

    /// <summary>Primary provider display name.</summary>
    [Id(19)]
    public string? PrimaryProviderName { get; set; }

    /// <summary>Annual and follow-up encounter records. (#154 sub-file ANNUAL DATA #154.03)</summary>
    [Id(20)]
    public List<SCIAnnualEncounterRecord> AnnualEncounters { get; set; } = new();

    /// <summary>Free-text clinical notes for the registry record.</summary>
    [Id(21)]
    public string? Notes { get; set; }

    /// <summary>Date this registry record was created in the system.</summary>
    [Id(22)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date this registry record was last modified.</summary>
    [Id(23)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// State for the SCI registry index grain — a singleton listing all enrolled patients.
/// </summary>
[GenerateSerializer]
public class SCIIndexState
{
    /// <summary>All patients enrolled in the SCI/D registry.</summary>
    [Id(0)]
    public List<SCIIndexEntry> Entries { get; set; } = new();
}
