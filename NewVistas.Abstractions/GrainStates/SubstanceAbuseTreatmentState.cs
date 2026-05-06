// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

// ── Enums ────────────────────────────────────────────────────────────────────

/// <summary>
/// Status of a substance abuse treatment episode.
/// Maps to RPMS CDMIS visit types (File #9002170).
/// </summary>
[GenerateSerializer]
public enum SATreatmentStatus
{
    Intake = 0,
    Active = 1,
    Transferred = 2,
    Discharged = 3,
    Closed = 4,
    Reopened = 5,
    Cancelled = 6,
}

/// <summary>
/// Treatment modality — RPMS CDMIS component codes (File #9002170.1).
/// </summary>
[GenerateSerializer]
public enum SATreatmentModality
{
    Outpatient = 0,
    IntensiveOutpatient = 1,
    ResidentialInpatient = 2,
    PartialHospitalization = 3,
    Detoxification = 4,
    MedicationAssisted = 5,
    AftercareMaintenance = 6,
    PreventionEducation = 7,
    Other = 8,
}

/// <summary>
/// Substance type — RPMS CDMIS drug table (File #9002170.5).
/// </summary>
[GenerateSerializer]
public enum SubstanceType
{
    Alcohol = 0,
    Cannabis = 1,
    Opioids = 2,
    Stimulants = 3,
    Benzodiazepines = 4,
    Hallucinogens = 5,
    Inhalants = 6,
    Tobacco = 7,
    Polysubstance = 8,
    Other = 9,
}

/// <summary>
/// Discharge disposition — RPMS CDMIS (File #9002171).
/// </summary>
[GenerateSerializer]
public enum SADischargeDisposition
{
    CompletedTreatment = 0,
    TransferredToHigherCare = 1,
    TransferredToLowerCare = 2,
    PatientDroppedOut = 3,
    AdministrativeDischarge = 4,
    Incarceration = 5,
    Deceased = 6,
    Other = 7,
}

/// <summary>
/// MAT (Medication-Assisted Treatment) medication type.
/// </summary>
[GenerateSerializer]
public enum MATMedication
{
    None = 0,
    Buprenorphine = 1,
    BuprenorphineNaloxone = 2,
    Methadone = 3,
    Naltrexone = 4,
    NaltrexoneExtendedRelease = 5,
    Disulfiram = 6,
    Acamprosate = 7,
    Other = 8,
}

/// <summary>
/// Type of SA treatment visit.
/// </summary>
[GenerateSerializer]
public enum SAVisitType
{
    Individual = 0,
    Group = 1,
    Family = 2,
    MedicationCheck = 3,
    UrineDrugScreen = 4,
    CrisisIntervention = 5,
    CaseManagement = 6,
    PeerSupport = 7,
    Assessment = 8,
    Other = 9,
}

// ── Nested Entry Types ───────────────────────────────────────────────────────

/// <summary>
/// MAT prescription entry on a treatment episode.
/// Maps to RPMS CDMIS pharmacotherapy (File #9002170.11).
/// </summary>
[GenerateSerializer]
public class MATEntry
{
    [Id(0)] public string EntryId { get; set; } = string.Empty;
    [Id(1)] public MATMedication Medication { get; set; }
    [Id(2)] public string? Dosage { get; set; }
    [Id(3)] public DateTime StartDate { get; set; }
    [Id(4)] public DateTime? EndDate { get; set; }
    [Id(5)] public string? PrescriberId { get; set; }
    [Id(6)] public string? PrescriberName { get; set; }
    [Id(7)] public bool IsActive { get; set; } = true;
    [Id(8)] public string? Notes { get; set; }
}

// ── Main State Classes ───────────────────────────────────────────────────────

/// <summary>
/// Persistent state for a Substance Abuse Treatment Episode grain (SA-EPISODE:{id}).
/// Maps to RPMS CDMIS (Chemical Dependency MIS) — File #9002170-9002174.
/// Tracks enrollment, treatment modality, MAT, substances, and discharge.
/// </summary>
[GenerateSerializer]
public class SATreatmentEpisodeState
{
    /// <summary>Unique grain key (SA-EPISODE:{guid}).</summary>
    [Id(0)]
    public string EpisodeId { get; set; } = string.Empty;

    /// <summary>Patient identifier.</summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Episode status.</summary>
    [Id(2)]
    public SATreatmentStatus Status { get; set; }

    /// <summary>Treatment modality.</summary>
    [Id(3)]
    public SATreatmentModality Modality { get; set; }

    /// <summary>Primary substance of abuse.</summary>
    [Id(4)]
    public SubstanceType PrimarySubstance { get; set; }

    /// <summary>Secondary substances of abuse.</summary>
    [Id(5)]
    public List<SubstanceType> SecondarySubstances { get; set; } = new();

    // ── Dates ────────────────────────────────────────────────────────────

    /// <summary>Date of intake/enrollment — CDMIS field (.01).</summary>
    [Id(6)]
    public DateTime IntakeDate { get; set; }

    /// <summary>Date of discharge or closure.</summary>
    [Id(7)]
    public DateTime? DischargeDate { get; set; }

    /// <summary>Date of last use (self-reported).</summary>
    [Id(8)]
    public DateTime? LastUseDate { get; set; }

    /// <summary>Sobriety date (patient-reported).</summary>
    [Id(9)]
    public DateTime? SobrietyDate { get; set; }

    // ── Clinical ─────────────────────────────────────────────────────────

    /// <summary>Discharge disposition when episode ends.</summary>
    [Id(10)]
    public SADischargeDisposition? DischargeDisposition { get; set; }

    /// <summary>Medication-Assisted Treatment entries.</summary>
    [Id(11)]
    public List<MATEntry> MATEntries { get; set; } = new();

    /// <summary>RPMS CDMIS program reference (File #9002173).</summary>
    [Id(12)]
    public string? ProgramName { get; set; }

    /// <summary>Treatment goals (free-text list).</summary>
    [Id(13)]
    public List<string> TreatmentGoals { get; set; } = new();

    // ── Provider / Location ──────────────────────────────────────────────

    [Id(14)] public string? ProviderId { get; set; }
    [Id(15)] public string? ProviderName { get; set; }
    [Id(16)] public string? LocationId { get; set; }
    [Id(17)] public string? LocationName { get; set; }

    // ── Notes & Audit ────────────────────────────────────────────────────

    [Id(18)] public string? Notes { get; set; }
    [Id(19)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [Id(20)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Persistent state for a Substance Abuse Treatment Visit grain (SA-VISIT:{id}).
/// Maps to RPMS CDMIS visit/client service records (File #9002172).
/// </summary>
[GenerateSerializer]
public class SAVisitState
{
    [Id(0)] public string VisitId { get; set; } = string.Empty;
    [Id(1)] public string EpisodeId { get; set; } = string.Empty;
    [Id(2)] public string PatientId { get; set; } = string.Empty;
    [Id(3)] public DateTime VisitDate { get; set; }
    [Id(4)] public SAVisitType VisitType { get; set; }

    /// <summary>Duration of visit in minutes.</summary>
    [Id(5)] public int? DurationMinutes { get; set; }

    /// <summary>Urine drug screen result (if visit type is UDS).</summary>
    [Id(6)] public string? UdsResult { get; set; }

    /// <summary>Substances detected in UDS (if applicable).</summary>
    [Id(7)] public List<string> UdsSubstancesDetected { get; set; } = new();

    /// <summary>Self-reported days since last use at this visit.</summary>
    [Id(8)] public int? DaysSinceLastUse { get; set; }

    /// <summary>Self-reported craving level (0-10 scale).</summary>
    [Id(9)] public int? CravingLevel { get; set; }

    [Id(10)] public string? ProviderId { get; set; }
    [Id(11)] public string? ProviderName { get; set; }
    [Id(12)] public string? Notes { get; set; }
    [Id(13)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [Id(14)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

// ── Index Entries ────────────────────────────────────────────────────────────

[GenerateSerializer]
public class SATreatmentEpisodeIndexEntry
{
    [Id(0)] public string EpisodeId { get; set; } = string.Empty;
    [Id(1)] public string PatientId { get; set; } = string.Empty;
    [Id(2)] public SATreatmentStatus Status { get; set; }
    [Id(3)] public SATreatmentModality Modality { get; set; }
    [Id(4)] public SubstanceType PrimarySubstance { get; set; }
    [Id(5)] public DateTime IntakeDate { get; set; }
    [Id(6)] public DateTime? DischargeDate { get; set; }
    [Id(7)] public string? ProviderName { get; set; }
    [Id(8)] public string? ProgramName { get; set; }
}

[GenerateSerializer]
public class SATreatmentEpisodeIndexState
{
    [Id(0)] public List<SATreatmentEpisodeIndexEntry> Entries { get; set; } = new();
}

[GenerateSerializer]
public class SAVisitIndexEntry
{
    [Id(0)] public string VisitId { get; set; } = string.Empty;
    [Id(1)] public string EpisodeId { get; set; } = string.Empty;
    [Id(2)] public DateTime VisitDate { get; set; }
    [Id(3)] public SAVisitType VisitType { get; set; }
    [Id(4)] public int? DurationMinutes { get; set; }
    [Id(5)] public string? ProviderName { get; set; }
    [Id(6)] public string? UdsResult { get; set; }
}

[GenerateSerializer]
public class SAVisitIndexState
{
    [Id(0)] public List<SAVisitIndexEntry> Entries { get; set; } = new();
}
