// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

// ── Enums ────────────────────────────────────────────────────────────────────

/// <summary>National VA clinical case registry type.</summary>
[GenerateSerializer]
public enum RegistryType
{
    HIV = 0,
    HepatitisCVirus = 1,
    DiabetesMellitus = 2,
    Asthma = 3,
}

/// <summary>Patient enrollment status within a clinical case registry.</summary>
[GenerateSerializer]
public enum CCREnrollmentStatus
{
    Active = 0,
    Inactive = 1,
    LostToFollowUp = 2,
    TransferredOut = 3,
    Deceased = 4
}

/// <summary>CDC HIV disease stage classification (1–3).</summary>
[GenerateSerializer]
public enum HIVStage
{
    Stage1 = 0,
    Stage2 = 1,
    Stage3AIDS = 2
}

/// <summary>HCV genotype for treatment selection.</summary>
[GenerateSerializer]
public enum HepCGenotype
{
    Unknown = 0,
    Genotype1a = 1,
    Genotype1b = 2,
    Genotype2 = 3,
    Genotype3 = 4,
    Genotype4 = 5,
    Genotype5 = 6,
    Genotype6 = 7
}

/// <summary>Hepatitis C antiviral treatment status.</summary>
[GenerateSerializer]
public enum HepCTreatmentStatus
{
    NeverTreated = 0,
    CurrentlyOnTreatment = 1,
    TreatmentCompleted = 2,
    SVRAchieved = 3,
    TreatmentFailed = 4,
    TreatmentDeferred = 5
}

/// <summary>Diabetes mellitus classification type.</summary>
[GenerateSerializer]
public enum DiabetesType
{
    Type1 = 0,
    Type2 = 1,
    Gestational = 2,
    LADA = 3,
    MODY = 4,
    Other = 5
}

/// <summary>Asthma severity classification — RPMS Asthma Tracking (File #90181.01).</summary>
[GenerateSerializer]
public enum AsthmaSeverity
{
    Intermittent = 0,
    MildPersistent = 1,
    ModeratePersistent = 2,
    SeverePersistent = 3,
}

/// <summary>Asthma control level per NAEPP EPR-3 guidelines.</summary>
[GenerateSerializer]
public enum AsthmaControlLevel
{
    WellControlled = 0,
    NotWellControlled = 1,
    VeryPoorlyControlled = 2,
}

/// <summary>Diabetes medication category — RPMS CDM taxonomy (BCDMFLDS.m).</summary>
[GenerateSerializer]
public class DiabetesMedicationStatus
{
    [Id(0)] public bool Insulin { get; set; }
    [Id(1)] public bool Metformin { get; set; }
    [Id(2)] public bool Sulfonylurea { get; set; }
    [Id(3)] public bool Glitazone { get; set; }
    [Id(4)] public bool DPP4Inhibitor { get; set; }
    [Id(5)] public bool GLP1Agonist { get; set; }
    [Id(6)] public bool SGLT2Inhibitor { get; set; }
    [Id(7)] public bool Acarbose { get; set; }
    [Id(8)] public bool Statin { get; set; }
    [Id(9)] public bool ACEInhibitorOrARB { get; set; }
    [Id(10)] public bool Aspirin { get; set; }
    [Id(11)] public bool DietControlOnly { get; set; }
}

/// <summary>Diabetes clinical exam tracking — RPMS DMS audit items.</summary>
[GenerateSerializer]
public class DiabetesExamRecord
{
    [Id(0)] public bool FootExamDone { get; set; }
    [Id(1)] public DateTime? FootExamDate { get; set; }
    [Id(2)] public bool EyeExamDone { get; set; }
    [Id(3)] public DateTime? EyeExamDate { get; set; }
    [Id(4)] public bool DentalExamDone { get; set; }
    [Id(5)] public DateTime? DentalExamDate { get; set; }
}

/// <summary>Diabetes education tracking — RPMS DMS audit items.</summary>
[GenerateSerializer]
public class DiabetesEducationRecord
{
    [Id(0)] public bool DietInstruction { get; set; }
    [Id(1)] public bool ExerciseInstruction { get; set; }
    [Id(2)] public bool OtherDMEducation { get; set; }
    [Id(3)] public bool TobaccoCessationCounseling { get; set; }
    [Id(4)] public bool SelfMonitoringEducation { get; set; }
    [Id(5)] public DateTime? LastEducationDate { get; set; }
}

// ── Supporting Types ─────────────────────────────────────────────────────────

/// <summary>Summary entry for registry population lists and index grains.</summary>
[GenerateSerializer]
public class CCREntrySummary
{
    [Id(0)] public string PatientId { get; set; } = string.Empty;
    [Id(1)] public string PatientName { get; set; } = string.Empty;
    [Id(2)] public RegistryType RegistryType { get; set; }
    [Id(3)] public CCREnrollmentStatus Status { get; set; }
    [Id(4)] public DateTime EnrollmentDate { get; set; }
    [Id(5)] public string SiteId { get; set; } = string.Empty;
    [Id(6)] public string PrimaryProviderName { get; set; } = string.Empty;
    [Id(7)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>Per-patient entry tracking which registries a patient is enrolled in.</summary>
[GenerateSerializer]
public class PatientRegistryEnrollmentEntry
{
    [Id(0)] public RegistryType RegistryType { get; set; }
    [Id(1)] public CCREnrollmentStatus Status { get; set; }
    [Id(2)] public DateTime EnrollmentDate { get; set; }
    [Id(3)] public DateTime LastModifiedDate { get; set; }
    [Id(4)] public string PrimaryProviderName { get; set; } = string.Empty;
}

// ── State Classes ─────────────────────────────────────────────────────────────

/// <summary>
/// Persisted state for one patient's enrollment in a specific clinical case registry.
/// A single grain instance covers all condition-specific fields for the enrolled registry type.
/// VistA CCR references: HIV Registry, HEPVU (HCV), DPCR (Diabetes Primary Care Registry).
/// </summary>
[GenerateSerializer]
public class ClinicalRegistryEntryState
{
    // ── Common fields (0–15) ─────────────────────────────────────────────────

    /// <summary>Grain key (e.g., "CCR:HIV:PAT-001").</summary>
    [Id(0)] public string EntryId { get; set; } = string.Empty;

    /// <summary>Patient identifier.</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient display name.</summary>
    [Id(2)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Patient date of birth.</summary>
    [Id(3)] public DateTime? DateOfBirth { get; set; }

    /// <summary>Registry type this entry belongs to.</summary>
    [Id(4)] public RegistryType RegistryType { get; set; }

    /// <summary>Current enrollment status.</summary>
    [Id(5)] public CCREnrollmentStatus EnrollmentStatus { get; set; }

    /// <summary>Date patient was enrolled in this registry.</summary>
    [Id(6)] public DateTime EnrollmentDate { get; set; }

    /// <summary>Date enrollment was deactivated (if applicable).</summary>
    [Id(7)] public DateTime? DeactivationDate { get; set; }

    /// <summary>Reason for deactivation or status change.</summary>
    [Id(8)] public string? DeactivationReason { get; set; }

    /// <summary>Staff or provider who enrolled the patient.</summary>
    [Id(9)] public string EnrolledById { get; set; } = string.Empty;

    /// <summary>Enrolling staff display name.</summary>
    [Id(10)] public string EnrolledByName { get; set; } = string.Empty;

    /// <summary>Primary registry care provider ID.</summary>
    [Id(11)] public string PrimaryProviderId { get; set; } = string.Empty;

    /// <summary>Primary provider display name.</summary>
    [Id(12)] public string PrimaryProviderName { get; set; } = string.Empty;

    /// <summary>VA medical center or site ID.</summary>
    [Id(13)] public string SiteId { get; set; } = string.Empty;

    /// <summary>Site display name.</summary>
    [Id(14)] public string SiteName { get; set; } = string.Empty;

    /// <summary>Clinical notes on this registry enrollment.</summary>
    [Id(15)] public string? Notes { get; set; }

    // ── HIV-specific fields (16–24) ───────────────────────────────────────────

    /// <summary>Date of confirmed HIV diagnosis.</summary>
    [Id(16)] public DateTime? HIVDiagnosisDate { get; set; }

    /// <summary>CDC HIV disease stage classification.</summary>
    [Id(17)] public HIVStage? HIVStage { get; set; }

    /// <summary>Most recent CD4 T-cell count (cells/mm³).</summary>
    [Id(18)] public decimal? CD4CountCellsPerMm3 { get; set; }

    /// <summary>Date of most recent CD4 measurement.</summary>
    [Id(19)] public DateTime? CD4Date { get; set; }

    /// <summary>Most recent HIV RNA viral load (copies/mL).</summary>
    [Id(20)] public decimal? ViralLoadCopiesPerMl { get; set; }

    /// <summary>Date of most recent viral load measurement.</summary>
    [Id(21)] public DateTime? ViralLoadDate { get; set; }

    /// <summary>Whether viral load is currently undetectable/suppressed.</summary>
    [Id(22)] public bool IsVirallySuppressed { get; set; }

    /// <summary>Date antiretroviral therapy (ART) was initiated.</summary>
    [Id(23)] public DateTime? ARTStartDate { get; set; }

    /// <summary>Current ART regimen (drug names or regimen code).</summary>
    [Id(24)] public string? CurrentARTRegimen { get; set; }

    // ── Hepatitis C-specific fields (25–32) ──────────────────────────────────

    /// <summary>Date of confirmed HCV diagnosis.</summary>
    [Id(25)] public DateTime? HepCDiagnosisDate { get; set; }

    /// <summary>HCV genotype for treatment selection.</summary>
    [Id(26)] public HepCGenotype? HepCGenotype { get; set; }

    /// <summary>Fibrosis score (kPa from FibroScan or FIB-4 equivalent).</summary>
    [Id(27)] public decimal? FibrosisScoreKpa { get; set; }

    /// <summary>Current HCV antiviral treatment status.</summary>
    [Id(28)] public HepCTreatmentStatus? HepCTreatmentStatus { get; set; }

    /// <summary>Date HCV treatment was started.</summary>
    [Id(29)] public DateTime? HepCTreatmentStartDate { get; set; }

    /// <summary>Date HCV treatment was completed.</summary>
    [Id(30)] public DateTime? HepCTreatmentEndDate { get; set; }

    /// <summary>Whether sustained virologic response (SVR12) was achieved.</summary>
    [Id(31)] public bool SVRAchieved { get; set; }

    /// <summary>Date SVR was confirmed.</summary>
    [Id(32)] public DateTime? SVRDate { get; set; }

    // ── Diabetes-specific fields (33–38) ─────────────────────────────────────

    /// <summary>Diabetes mellitus classification type.</summary>
    [Id(33)] public DiabetesType? DiabetesType { get; set; }

    /// <summary>Date of diabetes diagnosis.</summary>
    [Id(34)] public DateTime? DiabetesDiagnosisDate { get; set; }

    /// <summary>Most recent hemoglobin A1c percentage.</summary>
    [Id(35)] public decimal? HbA1cPct { get; set; }

    /// <summary>Date of most recent HbA1c measurement.</summary>
    [Id(36)] public DateTime? HbA1cDate { get; set; }

    /// <summary>Whether the patient is currently insulin-dependent.</summary>
    [Id(37)] public bool IsInsulinDependent { get; set; }

    /// <summary>Documented diabetes complications (e.g., Retinopathy, Nephropathy).</summary>
    [Id(38)] public List<string> DiabetesComplications { get; set; } = new();

    // ── Diabetes enrichment fields (41–50) — RPMS DMS/CDM ────────────────────

    /// <summary>Most recent LDL cholesterol (mg/dL).</summary>
    [Id(41)] public decimal? LdlMgDl { get; set; }

    /// <summary>Date of most recent LDL measurement.</summary>
    [Id(42)] public DateTime? LdlDate { get; set; }

    /// <summary>Most recent urine microalbumin result (mg/L).</summary>
    [Id(43)] public decimal? MicroalbuminMgL { get; set; }

    /// <summary>Date of most recent microalbumin measurement.</summary>
    [Id(44)] public DateTime? MicroalbuminDate { get; set; }

    /// <summary>Most recent systolic/diastolic blood pressure.</summary>
    [Id(45)] public int? BloodPressureSystolic { get; set; }

    /// <summary>Most recent diastolic blood pressure.</summary>
    [Id(46)] public int? BloodPressureDiastolic { get; set; }

    /// <summary>Date of most recent blood pressure reading.</summary>
    [Id(47)] public DateTime? BloodPressureDate { get; set; }

    /// <summary>Diabetes medication categories — RPMS CDM taxonomy.</summary>
    [Id(48)] public DiabetesMedicationStatus? DiabetesMedications { get; set; }

    /// <summary>Diabetes exam tracking (foot, eye, dental).</summary>
    [Id(49)] public DiabetesExamRecord? DiabetesExams { get; set; }

    /// <summary>Diabetes education tracking.</summary>
    [Id(50)] public DiabetesEducationRecord? DiabetesEducation { get; set; }

    // ── Asthma-specific fields (51–63) — RPMS Asthma Tracking (File #90181) ──

    /// <summary>Date of asthma diagnosis.</summary>
    [Id(51)] public DateTime? AsthmaDiagnosisDate { get; set; }

    /// <summary>Asthma severity classification.</summary>
    [Id(52)] public AsthmaSeverity? AsthmaSeverity { get; set; }

    /// <summary>Current asthma control level (per NAEPP EPR-3).</summary>
    [Id(53)] public AsthmaControlLevel? AsthmaControlLevel { get; set; }

    /// <summary>Date of most recent spirometry/PFT.</summary>
    [Id(54)] public DateTime? SpirometryDate { get; set; }

    /// <summary>FEV1 predicted percentage from most recent spirometry.</summary>
    [Id(55)] public decimal? Fev1PredictedPct { get; set; }

    /// <summary>FEV1/FVC ratio from most recent spirometry.</summary>
    [Id(56)] public decimal? Fev1FvcRatio { get; set; }

    /// <summary>Most recent peak flow reading (L/min).</summary>
    [Id(57)] public int? PeakFlowLPerMin { get; set; }

    /// <summary>Personal best peak flow (L/min) for action plan zones.</summary>
    [Id(58)] public int? PeakFlowPersonalBest { get; set; }

    /// <summary>Name of current controller medication (e.g., ICS, ICS/LABA).</summary>
    [Id(59)] public string? ControllerMedication { get; set; }

    /// <summary>Name of current rescue medication (e.g., SABA — albuterol).</summary>
    [Id(60)] public string? RescueMedication { get; set; }

    /// <summary>Whether an asthma action plan is documented.</summary>
    [Id(61)] public bool HasAsthmaActionPlan { get; set; }

    /// <summary>Known asthma triggers (e.g., dust, exercise, cold air, pets).</summary>
    [Id(62)] public List<string> AsthmaTriggers { get; set; } = new();

    /// <summary>Number of ED visits for asthma in past 12 months.</summary>
    [Id(63)] public int? AsthmaEdVisitsLast12Months { get; set; }

    // ── Audit fields (39–40) ─────────────────────────────────────────────────

    /// <summary>Date/time the record was created.</summary>
    [Id(39)] public DateTime CreatedDate { get; set; }

    /// <summary>Date/time the record was last modified.</summary>
    [Id(40)] public DateTime LastModifiedDate { get; set; }
}
