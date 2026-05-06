// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>Type of GEC/MDS functional assessment. VistA GEC File #25.1 (.03)</summary>
public enum GECAssessmentType
{
    Initial,
    Annual,
    Quarterly,
    SignificantChange,
    Discharge,
    Other
}

/// <summary>Workflow status of a GEC assessment.</summary>
public enum GECAssessmentStatus
{
    Draft,
    Completed,
    Submitted
}

/// <summary>Level of care provided in the Community Living Center. VistA GEC File #25.1 (.05)</summary>
public enum GECLevelOfCare
{
    SkilledNursing,
    SubAcuteRehabilitation,
    Hospice,
    DementiaCare,
    LongTermCare,
    Respite,
    Palliative
}

/// <summary>Current CLC admission status. VistA GEC File #25.1 (.06)</summary>
public enum CLCAdmissionStatus
{
    Active,
    OnLeave,
    Discharged,
    Deceased
}

/// <summary>Source from which the patient was admitted to the CLC.</summary>
public enum CLCAdmitSource
{
    AcuteHospital,
    Community,
    AnotherLTCF,
    Rehabilitation,
    HomeCare,
    Other
}

/// <summary>Destination on discharge from CLC.</summary>
public enum CLCDischargeDestination
{
    Home,
    AcuteCare,
    AnotherLTCF,
    AssistedLiving,
    Hospice,
    Deceased,
    Other
}

/// <summary>
/// Resource Utilization Group (RUG-IV) classification category
/// used for Medicare billing in long-term care.
/// </summary>
public enum GECRUGCategory
{
    NotAssigned,
    Rehabilitation,
    ExtensiveServices,
    SpecialCare,
    ClinicallyCComplex,
    BehavioralSymptoms,
    CognitiveImpairment,
    Reduced
}

/// <summary>
/// Functional assessment for a GEC/CLC patient (modeled on MDS 3.0).
/// VistA GEC File #25.1 (GERIATRIC EVAL). MDS.m, GMTS.m
/// </summary>
[GenerateSerializer]
public class GECAssessmentState
{
    /// <summary>Unique assessment identifier.</summary>
    [Id(0)] public string AssessmentId { get; set; } = string.Empty;

    /// <summary>Patient file number. VistA GEC File #25.1 (.01)</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name. VistA GEC File #25.1 (.02)</summary>
    [Id(2)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Type of assessment (Initial, Annual, etc.). VistA GEC File #25.1 (.03)</summary>
    [Id(3)] public GECAssessmentType AssessmentType { get; set; }

    /// <summary>Current workflow status.</summary>
    [Id(4)] public GECAssessmentStatus Status { get; set; }

    /// <summary>Date the assessment was performed. VistA GEC File #25.1 (.04)</summary>
    [Id(5)] public DateTime AssessmentDate { get; set; }

    /// <summary>Assessment reference period start date.</summary>
    [Id(6)] public DateTime PeriodStart { get; set; }

    /// <summary>Assessment reference period end date.</summary>
    [Id(7)] public DateTime PeriodEnd { get; set; }

    /// <summary>Clinician who completed the assessment.</summary>
    [Id(8)] public string CompletedBy { get; set; } = string.Empty;

    /// <summary>Title/discipline of the completing clinician.</summary>
    [Id(9)] public string CompletedByTitle { get; set; } = string.Empty;

    // ── ADL Self-Care Scores (MDS 3.0 Section G, 0=Independent 4=Total Dependence) ──

    /// <summary>Bed mobility ADL score (0–4).</summary>
    [Id(10)] public int ADLBedMobility { get; set; }

    /// <summary>Transfer ADL score (0–4).</summary>
    [Id(11)] public int ADLTransfer { get; set; }

    /// <summary>Walking/locomotion ADL score (0–4).</summary>
    [Id(12)] public int ADLWalking { get; set; }

    /// <summary>Dressing ADL score (0–4).</summary>
    [Id(13)] public int ADLDressing { get; set; }

    /// <summary>Eating ADL score (0–4).</summary>
    [Id(14)] public int ADLEating { get; set; }

    /// <summary>Toilet use ADL score (0–4).</summary>
    [Id(15)] public int ADLToiletUse { get; set; }

    /// <summary>Personal hygiene ADL score (0–4).</summary>
    [Id(16)] public int ADLPersonalHygiene { get; set; }

    /// <summary>Total ADL Self-Care score (sum of 7 ADL items, 0–28).</summary>
    [Id(17)] public int ADLTotalScore { get; set; }

    // ── Cognitive / Mood ──────────────────────────────────────────────────────

    /// <summary>BIMS (Brief Interview for Mental Status) score (0–15). MDS 3.0 Section C.</summary>
    [Id(18)] public int? BIMSScore { get; set; }

    /// <summary>PHQ-9 mood interview total score (0–27). MDS 3.0 Section D.</summary>
    [Id(19)] public int? PHQ9Score { get; set; }

    // ── Clinical Indicators ───────────────────────────────────────────────────

    /// <summary>Whether the patient has pain (MDS 3.0 Section J).</summary>
    [Id(20)] public bool PainPresent { get; set; }

    /// <summary>Pain frequency description if pain present.</summary>
    [Id(21)] public string PainFrequency { get; set; } = string.Empty;

    /// <summary>Number of pressure ulcers/injuries present (MDS 3.0 Section M).</summary>
    [Id(22)] public int PressureUlcerCount { get; set; }

    /// <summary>Number of falls in the last 30 days (MDS 3.0 Section J).</summary>
    [Id(23)] public int FallsLast30Days { get; set; }

    /// <summary>Whether a nutrition/hydration concern is present (MDS 3.0 Section K).</summary>
    [Id(24)] public bool NutritionConcern { get; set; }

    /// <summary>Whether behavior symptoms are present (MDS 3.0 Section E).</summary>
    [Id(25)] public bool BehaviorSymptoms { get; set; }

    // ── Classification ────────────────────────────────────────────────────────

    /// <summary>RUG-IV category assigned based on assessment. VistA GEC billing.</summary>
    [Id(26)] public GECRUGCategory RUGCategory { get; set; }

    /// <summary>Level of care at time of assessment.</summary>
    [Id(27)] public GECLevelOfCare LevelOfCare { get; set; }

    /// <summary>Clinical notes and additional observations.</summary>
    [Id(28)] public string Notes { get; set; } = string.Empty;

    /// <summary>Date and time assessment was submitted/finalized.</summary>
    [Id(29)] public DateTime? SubmittedDate { get; set; }

    /// <summary>Last modified timestamp.</summary>
    [Id(30)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>Summary entry for GEC assessment index queries.</summary>
[GenerateSerializer]
public class GECAssessmentIndexEntry
{
    [Id(0)] public string AssessmentId { get; set; } = string.Empty;
    [Id(1)] public string PatientId { get; set; } = string.Empty;
    [Id(2)] public string PatientName { get; set; } = string.Empty;
    [Id(3)] public GECAssessmentType AssessmentType { get; set; }
    [Id(4)] public DateTime AssessmentDate { get; set; }
    [Id(5)] public GECAssessmentStatus Status { get; set; }
    [Id(6)] public GECRUGCategory RUGCategory { get; set; }
    [Id(7)] public int ADLTotalScore { get; set; }
    [Id(8)] public GECLevelOfCare LevelOfCare { get; set; }
}

/// <summary>
/// Community Living Center admission record.
/// VistA GEC File #25.1 (COMMUNITY LIVING CENTER). GECCLC.m
/// </summary>
[GenerateSerializer]
public class CLCAdmissionState
{
    /// <summary>Unique admission identifier.</summary>
    [Id(0)] public string AdmissionId { get; set; } = string.Empty;

    /// <summary>Patient file number. VistA GEC File #25.1 (.01)</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name. VistA GEC File #25.1 (.02)</summary>
    [Id(2)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Patient date of birth.</summary>
    [Id(3)] public DateTime? PatientDOB { get; set; }

    /// <summary>Date of CLC admission. VistA GEC File #25.1 (.03)</summary>
    [Id(4)] public DateTime AdmitDate { get; set; }

    /// <summary>Source of admission (hospital, community, etc.). VistA GEC File #25.1 (.04)</summary>
    [Id(5)] public CLCAdmitSource AdmitSource { get; set; }

    /// <summary>Level of care provided. VistA GEC File #25.1 (.05)</summary>
    [Id(6)] public GECLevelOfCare LevelOfCare { get; set; }

    /// <summary>Current admission status. VistA GEC File #25.1 (.06)</summary>
    [Id(7)] public CLCAdmissionStatus Status { get; set; }

    /// <summary>Ward or unit name (e.g., "CLC Ward 3B").</summary>
    [Id(8)] public string Ward { get; set; } = string.Empty;

    /// <summary>Bed number or room assignment.</summary>
    [Id(9)] public string BedRoom { get; set; } = string.Empty;

    /// <summary>Attending physician name.</summary>
    [Id(10)] public string AttendingPhysician { get; set; } = string.Empty;

    /// <summary>Primary diagnosis at admission.</summary>
    [Id(11)] public string PrimaryDiagnosis { get; set; } = string.Empty;

    /// <summary>Referring facility if transferred from another institution.</summary>
    [Id(12)] public string ReferringFacility { get; set; } = string.Empty;

    /// <summary>Anticipated discharge date (planning target).</summary>
    [Id(13)] public DateTime? AnticipatedDischargeDate { get; set; }

    /// <summary>Actual discharge date.</summary>
    [Id(14)] public DateTime? ActualDischargeDate { get; set; }

    /// <summary>Discharge destination.</summary>
    [Id(15)] public CLCDischargeDestination? DischargeDestination { get; set; }

    /// <summary>Discharge summary notes.</summary>
    [Id(16)] public string DischargeNotes { get; set; } = string.Empty;

    /// <summary>Admission notes.</summary>
    [Id(17)] public string Notes { get; set; } = string.Empty;

    /// <summary>Last modified timestamp.</summary>
    [Id(18)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>Summary entry for CLC admission index queries.</summary>
[GenerateSerializer]
public class CLCAdmissionIndexEntry
{
    [Id(0)] public string AdmissionId { get; set; } = string.Empty;
    [Id(1)] public string PatientId { get; set; } = string.Empty;
    [Id(2)] public string PatientName { get; set; } = string.Empty;
    [Id(3)] public DateTime AdmitDate { get; set; }
    [Id(4)] public GECLevelOfCare LevelOfCare { get; set; }
    [Id(5)] public CLCAdmissionStatus Status { get; set; }
    [Id(6)] public string Ward { get; set; } = string.Empty;
    [Id(7)] public string BedRoom { get; set; } = string.Empty;
    [Id(8)] public string AttendingPhysician { get; set; } = string.Empty;
    [Id(9)] public DateTime? AnticipatedDischargeDate { get; set; }
}
