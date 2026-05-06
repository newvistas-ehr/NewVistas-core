// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for a DRG (Diagnosis Related Group) assignment.
/// Maps to VistA ICD DRG Grouper — assigns DRGs for inpatient billing
/// based on principal diagnosis, secondary diagnoses, procedures, age, sex, and discharge status.
/// Key: "DRG:{admissionId}"
/// </summary>
[GenerateSerializer]
public class DrgAssignmentState
{
    /// <summary>Admission/encounter ID.</summary>
    [Id(0)] public string AdmissionId { get; set; } = string.Empty;

    /// <summary>Patient ID.</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name.</summary>
    [Id(2)] public string PatientName { get; set; } = string.Empty;

    /// <summary>DRG code (e.g., "470", "871").</summary>
    [Id(3)] public string DrgCode { get; set; } = string.Empty;

    /// <summary>DRG description.</summary>
    [Id(4)] public string DrgDescription { get; set; } = string.Empty;

    /// <summary>MDC (Major Diagnostic Category) code.</summary>
    [Id(5)] public string MdcCode { get; set; } = string.Empty;

    /// <summary>MDC description.</summary>
    [Id(6)] public string MdcDescription { get; set; } = string.Empty;

    /// <summary>DRG type: MEDICAL, SURGICAL.</summary>
    [Id(7)] public string DrgType { get; set; } = string.Empty;

    /// <summary>Relative weight (for reimbursement calculation).</summary>
    [Id(8)] public decimal RelativeWeight { get; set; }

    /// <summary>Geometric mean length of stay (days).</summary>
    [Id(9)] public decimal GeometricMeanLOS { get; set; }

    /// <summary>Arithmetic mean length of stay (days).</summary>
    [Id(10)] public decimal ArithmeticMeanLOS { get; set; }

    /// <summary>Principal diagnosis ICD-10 code.</summary>
    [Id(11)] public string PrincipalDiagnosis { get; set; } = string.Empty;

    /// <summary>Principal diagnosis description.</summary>
    [Id(12)] public string PrincipalDiagnosisDescription { get; set; } = string.Empty;

    /// <summary>Secondary diagnoses (comma-separated ICD-10 codes).</summary>
    [Id(13)] public List<string> SecondaryDiagnoses { get; set; } = new();

    /// <summary>Procedures performed (ICD-10-PCS codes).</summary>
    [Id(14)] public List<string> Procedures { get; set; } = new();

    /// <summary>Patient age at admission.</summary>
    [Id(15)] public int PatientAge { get; set; }

    /// <summary>Patient sex: M, F.</summary>
    [Id(16)] public string PatientSex { get; set; } = string.Empty;

    /// <summary>Discharge status code.</summary>
    [Id(17)] public string DischargeStatus { get; set; } = string.Empty;

    /// <summary>Actual length of stay (days).</summary>
    [Id(18)] public int ActualLOS { get; set; }

    /// <summary>Whether this is an outlier case (LOS significantly exceeds geometric mean).</summary>
    [Id(19)] public bool IsOutlier { get; set; }

    /// <summary>Complication/Comorbidity level: NONE, CC, MCC.</summary>
    [Id(20)] public string CcMccLevel { get; set; } = "NONE";

    /// <summary>Status: PENDING, ASSIGNED, REVIEWED, FINAL.</summary>
    [Id(21)] public string Status { get; set; } = "PENDING";

    /// <summary>Assigned by (coder ID).</summary>
    [Id(22)] public string? AssignedBy { get; set; }

    /// <summary>Reviewed by (reviewer ID).</summary>
    [Id(23)] public string? ReviewedBy { get; set; }

    /// <summary>Created date.</summary>
    [Id(24)] public DateTime CreatedDate { get; set; }

    /// <summary>Last modified.</summary>
    [Id(25)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>
/// Lightweight DRG assignment entry for index.
/// </summary>
[GenerateSerializer]
public class DrgAssignmentEntry
{
    /// <summary>Admission ID.</summary>
    [Id(0)] public string AdmissionId { get; set; } = string.Empty;

    /// <summary>Patient name.</summary>
    [Id(1)] public string PatientName { get; set; } = string.Empty;

    /// <summary>DRG code.</summary>
    [Id(2)] public string DrgCode { get; set; } = string.Empty;

    /// <summary>DRG description.</summary>
    [Id(3)] public string DrgDescription { get; set; } = string.Empty;

    /// <summary>Relative weight.</summary>
    [Id(4)] public decimal RelativeWeight { get; set; }

    /// <summary>Actual LOS.</summary>
    [Id(5)] public int ActualLOS { get; set; }

    /// <summary>Status.</summary>
    [Id(6)] public string Status { get; set; } = string.Empty;

    /// <summary>CC/MCC level.</summary>
    [Id(7)] public string CcMccLevel { get; set; } = string.Empty;
}

/// <summary>
/// State for DRG assignment index per facility (for billing/coding worklist).
/// Key: "DRG-INDEX:{facilityId}"
/// </summary>
[GenerateSerializer]
public class DrgIndexState
{
    /// <summary>All DRG assignments.</summary>
    [Id(0)] public List<DrgAssignmentEntry> Assignments { get; set; } = new();
}
