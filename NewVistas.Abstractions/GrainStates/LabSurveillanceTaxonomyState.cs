// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Lab surveillance taxonomy entry — a code within a taxonomy group.
/// Maps to RPMS Activity Taxonomy (File #9999999.05) entries.
/// </summary>
[GenerateSerializer]
public class LabSurveillanceTaxonomyCode
{
    /// <summary>Clinical code (LOINC, ICD-10, CPT, SNOMED).</summary>
    [Id(0)]
    public string Code { get; set; } = string.Empty;

    /// <summary>Code system: "LOINC", "ICD-10", "CPT", "SNOMED".</summary>
    [Id(1)]
    public string CodeSystem { get; set; } = string.Empty;

    /// <summary>Human-readable description.</summary>
    [Id(2)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Specimen type (for LOINC lab codes).</summary>
    [Id(3)]
    public string? SpecimenType { get; set; }

    /// <summary>Value operator for threshold matching (e.g., "positive", "greater-than").</summary>
    [Id(4)]
    public string? ValueOperator { get; set; }

    /// <summary>Threshold value for quantitative matching.</summary>
    [Id(5)]
    public string? ThresholdValue { get; set; }

    /// <summary>Result interpretation for qualitative matching.</summary>
    [Id(6)]
    public string? ResultInterpretation { get; set; }
}

/// <summary>
/// Persistent state for a Lab Surveillance Taxonomy grain (LAB-SURV-TAX:{taxonomyId}).
/// Groups trigger codes by reportable condition, matching RPMS Activity Taxonomy (File #9999999.05).
/// Used by screening logic to evaluate multiple codes for the same condition efficiently.
/// </summary>
[GenerateSerializer]
public class LabSurveillanceTaxonomyState
{
    /// <summary>Unique taxonomy identifier.</summary>
    [Id(0)]
    public string TaxonomyId { get; set; } = string.Empty;

    /// <summary>Taxonomy name (e.g., "Chlamydia Tests", "Rapid Flu LOINC", "TB Culture").</summary>
    [Id(1)]
    public string TaxonomyName { get; set; } = string.Empty;

    /// <summary>Reportable condition this taxonomy maps to.</summary>
    [Id(2)]
    public string ConditionName { get; set; } = string.Empty;

    /// <summary>SNOMED condition code.</summary>
    [Id(3)]
    public string? ConditionCode { get; set; }

    /// <summary>Category: "communicable", "environmental", "occupational", "chronic".</summary>
    [Id(4)]
    public string Category { get; set; } = "communicable";

    /// <summary>Jurisdiction(s) where reportable.</summary>
    [Id(5)]
    public List<string> Jurisdictions { get; set; } = new();

    /// <summary>Reporting timeframe requirement.</summary>
    [Id(6)]
    public string ReportingTimeframe { get; set; } = "24 hours";

    /// <summary>Codes in this taxonomy group.</summary>
    [Id(7)]
    public List<LabSurveillanceTaxonomyCode> Codes { get; set; } = new();

    /// <summary>Whether this taxonomy is active for surveillance.</summary>
    [Id(8)]
    public bool IsActive { get; set; } = true;

    [Id(9)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

// ── Index ────────────────────────────────────────────────────────────────────

[GenerateSerializer]
public class LabSurveillanceTaxonomyIndexEntry
{
    [Id(0)] public string TaxonomyId { get; set; } = string.Empty;
    [Id(1)] public string TaxonomyName { get; set; } = string.Empty;
    [Id(2)] public string ConditionName { get; set; } = string.Empty;
    [Id(3)] public string Category { get; set; } = string.Empty;
    [Id(4)] public int CodeCount { get; set; }
    [Id(5)] public bool IsActive { get; set; }
}

[GenerateSerializer]
public class LabSurveillanceTaxonomyIndexState
{
    [Id(0)] public List<LabSurveillanceTaxonomyIndexEntry> Entries { get; set; } = new();
}
