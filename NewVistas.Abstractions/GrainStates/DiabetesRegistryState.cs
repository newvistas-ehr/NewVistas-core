// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

// ── Status enums ────────────────────────────────────────────────────────────

/// <summary>
/// HbA1c control status. Cutoffs follow the ADA/IHS convention used by the
/// GPRA "Diabetes: Poor HbA1c Control" measure (GPRA-DM-02 in our seed
/// dataset): &lt;7.0 good, 7.0–8.9 at-target/elevated, ≥9.0 poor.
/// </summary>
[GenerateSerializer]
public enum HbA1cControlStatus
{
    NoData = 0,
    Good = 1,           // < 7.0
    AtTarget = 2,       // 7.0–8.9
    Poor = 3,           // ≥ 9.0
}

/// <summary>
/// Time-based exam status. Thresholds are per the IHS standard of care
/// (annual exam, with a 3-month grace before "overdue" kicks in).
/// </summary>
[GenerateSerializer]
public enum DueStatus
{
    NoData = 0,
    UpToDate = 1,       // last performed within 12 months
    Due = 2,            // last performed 12–15 months ago
    Overdue = 3,        // last performed > 15 months ago (or never)
}

/// <summary>
/// Kidney function status (CKD staging, simplified). Round-1 buckets;
/// future expansion can split into G1–G5 if a tribe needs the full granularity.
/// </summary>
[GenerateSerializer]
public enum KidneyFunctionStatus
{
    NoData = 0,
    Normal = 1,         // eGFR ≥ 60
    Reduced = 2,        // eGFR 30–59 (CKD G3)
    Severe = 3,         // eGFR < 30 (CKD G4–G5)
}

// ── Per-patient registry state ──────────────────────────────────────────────

/// <summary>
/// Persistent state for one patient's diabetes registry record. Grain key:
/// <c>"DM-REG:{icn}"</c>.
///
/// Maps to the diabetic-patient subset of RPMS BDM (Diabetes Management).
/// Tracks the data points GPRA cares about (HbA1c, foot exam, eye exam,
/// kidney function) plus a small history for trending. Disease-specific
/// registries beyond diabetes (asthma, CV) can mirror this shape.
/// </summary>
[GenerateSerializer]
public class DiabetesRegistryState
{
    /// <summary>The patient's ICN (also the grain key suffix).</summary>
    [Id(0)] public string Icn { get; set; } = string.Empty;

    /// <summary>True once the patient has been enrolled in the registry (e.g., diagnosed with diabetes).</summary>
    [Id(1)] public bool IsEnrolled { get; set; }

    /// <summary>Date the patient was enrolled.</summary>
    [Id(2)] public DateTime? EnrollmentDate { get; set; }

    /// <summary>Diabetes type at enrollment, e.g., "TYPE_1", "TYPE_2", "GESTATIONAL", "OTHER".</summary>
    [Id(3)] public string? DiabetesType { get; set; }

    // ── HbA1c ────────────────────────────────────────────────────────────
    /// <summary>Last N HbA1c results, oldest first.</summary>
    [Id(4)] public List<HbA1cReading> HbA1cHistory { get; set; } = new();

    // ── Annual exams ─────────────────────────────────────────────────────
    /// <summary>Date of the most recent annual foot exam, if any.</summary>
    [Id(5)] public DateTime? LastFootExamDate { get; set; }

    /// <summary>Provider who performed the most recent foot exam.</summary>
    [Id(6)] public string? LastFootExamProviderName { get; set; }

    /// <summary>Date of the most recent dilated retinal eye exam, if any.</summary>
    [Id(7)] public DateTime? LastEyeExamDate { get; set; }

    /// <summary>Provider who performed the most recent eye exam.</summary>
    [Id(8)] public string? LastEyeExamProviderName { get; set; }

    // ── Kidney function ──────────────────────────────────────────────────
    /// <summary>Most recent eGFR (mL/min/1.73m²), if any.</summary>
    [Id(9)] public decimal? LastEgfr { get; set; }

    /// <summary>Date of the most recent eGFR test.</summary>
    [Id(10)] public DateTime? LastEgfrDate { get; set; }

    /// <summary>Most recent urine albumin/creatinine ratio (mg/g), if any.</summary>
    [Id(11)] public decimal? LastAcrMgPerGram { get; set; }

    /// <summary>Date of the most recent ACR test.</summary>
    [Id(12)] public DateTime? LastAcrDate { get; set; }

    [Id(13)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [Id(14)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>One HbA1c lab result.</summary>
[GenerateSerializer]
public class HbA1cReading
{
    [Id(0)] public decimal Value { get; set; }
    [Id(1)] public DateTime DateOfTest { get; set; }
}

// ── Computed snapshot ───────────────────────────────────────────────────────

/// <summary>
/// Computed view of a diabetes registry record at a point in time. Used by
/// clinician UIs (the patient cover sheet's "Diabetes" panel) and as input
/// to GPRA aggregation.
/// </summary>
[GenerateSerializer]
public class DiabetesRegistrySnapshot
{
    [Id(0)] public string Icn { get; set; } = string.Empty;
    [Id(1)] public bool IsEnrolled { get; set; }
    [Id(2)] public string? DiabetesType { get; set; }

    /// <summary>Most recent HbA1c value, if any.</summary>
    [Id(3)] public decimal? LastHbA1cValue { get; set; }

    /// <summary>Date of most recent HbA1c, if any.</summary>
    [Id(4)] public DateTime? LastHbA1cDate { get; set; }

    /// <summary>Computed HbA1c control status.</summary>
    [Id(5)] public HbA1cControlStatus HbA1cControl { get; set; }

    /// <summary>Computed status of the annual foot exam.</summary>
    [Id(6)] public DueStatus FootExamStatus { get; set; }

    /// <summary>Computed status of the annual dilated eye exam.</summary>
    [Id(7)] public DueStatus EyeExamStatus { get; set; }

    /// <summary>Computed status of the annual ACR (urine albumin/creatinine) test.</summary>
    [Id(8)] public DueStatus AcrStatus { get; set; }

    /// <summary>Computed kidney function status from the most recent eGFR.</summary>
    [Id(9)] public KidneyFunctionStatus KidneyFunction { get; set; }

    /// <summary>Most recent eGFR, if any.</summary>
    [Id(10)] public decimal? LastEgfrValue { get; set; }

    /// <summary>Date of most recent eGFR.</summary>
    [Id(11)] public DateTime? LastEgfrDate { get; set; }

    /// <summary>Most recent ACR, if any.</summary>
    [Id(12)] public decimal? LastAcrValue { get; set; }

    /// <summary>Date of most recent ACR.</summary>
    [Id(13)] public DateTime? LastAcrDate { get; set; }

    /// <summary>Date the most recent foot exam was performed.</summary>
    [Id(14)] public DateTime? LastFootExamDate { get; set; }

    /// <summary>Date the most recent eye exam was performed.</summary>
    [Id(15)] public DateTime? LastEyeExamDate { get; set; }
}

// ── Pre-visit plan ──────────────────────────────────────────────────────────

/// <summary>
/// Pre-visit plan generated for a diabetic patient before a clinic encounter.
/// Lists the items that are due or overdue at the supplied visit date so
/// the clinician can address them during the visit. Equivalent to the
/// pre-visit planning summary in RPMS Comprehensive Diabetes Management.
/// </summary>
[GenerateSerializer]
public class DiabetesPreVisitPlan
{
    [Id(0)] public string Icn { get; set; } = string.Empty;
    [Id(1)] public DateTime VisitDate { get; set; }

    /// <summary>Human-readable items to address at the visit (e.g., "HbA1c due (last test 8 months ago)").</summary>
    [Id(2)] public List<string> ItemsDue { get; set; } = new();

    /// <summary>Items that are overdue (more urgent than Due).</summary>
    [Id(3)] public List<string> ItemsOverdue { get; set; } = new();

    /// <summary>Items completed within standard intervals — informational, not actionable.</summary>
    [Id(4)] public List<string> ItemsUpToDate { get; set; } = new();

    /// <summary>Snapshot at the time the plan was generated.</summary>
    [Id(5)] public DiabetesRegistrySnapshot Snapshot { get; set; } = new();
}

// ── Registry index ──────────────────────────────────────────────────────────

/// <summary>
/// Singleton index of all diabetes registry enrollees. Grain key:
/// <c>"DM-REGISTRY-IDX"</c>.
/// </summary>
[GenerateSerializer]
public class DiabetesRegistryIndexState
{
    /// <summary>ICN → enrollment-date for quick cohort enumeration.</summary>
    [Id(0)] public Dictionary<string, DateTime> EnrolledIcns { get; set; } = new();
}
