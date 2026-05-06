// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for the iCare provider dashboard grain.
/// Stores the provider's patient panel and cached dashboard data.
/// Maps to IHS RPMS iCare / BQI dashboard.
/// </summary>
[GenerateSerializer]
public class iCareDashboardState
{
    /// <summary>Provider ID this dashboard belongs to (grain key).</summary>
    [Id(0)]
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>Patients on this provider's panel.</summary>
    [Id(1)]
    public List<PanelPatient> Panel { get; set; } = new();

    /// <summary>Most recently generated patient summaries.</summary>
    [Id(2)]
    public List<iCarePatientSummary> PatientSummaries { get; set; } = new();

    /// <summary>When the dashboard was last generated.</summary>
    [Id(3)]
    public DateTime? LastGeneratedDate { get; set; }

    [Id(4)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(5)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A patient on a provider's panel.
/// </summary>
[GenerateSerializer]
public class PanelPatient
{
    [Id(0)]
    public string PatientId { get; set; } = string.Empty;

    [Id(1)]
    public string PatientName { get; set; } = string.Empty;

    [Id(2)]
    public DateTime AddedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Aggregated clinical summary for a single patient on the iCare dashboard.
/// Combines reminders, quality gaps, and registry enrollment.
/// </summary>
[GenerateSerializer]
public class iCarePatientSummary
{
    /// <summary>Patient identifier.</summary>
    [Id(0)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name.</summary>
    [Id(1)]
    public string PatientName { get; set; } = string.Empty;

    /// <summary>Date of birth.</summary>
    [Id(2)]
    public DateTime? DateOfBirth { get; set; }

    /// <summary>Sex.</summary>
    [Id(3)]
    public string Sex { get; set; } = string.Empty;

    // ── Clinical Reminders ──────────────────────────────────────────

    /// <summary>Reminders that are DUE for this patient.</summary>
    [Id(4)]
    public List<iCareReminderItem> DueReminders { get; set; } = new();

    /// <summary>Total number of due reminders.</summary>
    [Id(5)]
    public int DueReminderCount { get; set; }

    // ── Quality Measure Gaps ────────────────────────────────────────

    /// <summary>Quality measures where this patient has a gap (in denominator but not numerator).</summary>
    [Id(6)]
    public List<iCareQualityGap> QualityGaps { get; set; } = new();

    /// <summary>Total number of quality gaps.</summary>
    [Id(7)]
    public int QualityGapCount { get; set; }

    // ── Registry Enrollment ─────────────────────────────────────────

    /// <summary>Disease registries this patient is enrolled in.</summary>
    [Id(8)]
    public List<iCareRegistryEntry> Registries { get; set; } = new();

    // ── Summary Indicators ──────────────────────────────────────────

    /// <summary>Overall status: GREEN (no gaps), YELLOW (some gaps), RED (critical gaps).</summary>
    [Id(9)]
    public string OverallStatus { get; set; } = "GREEN";

    /// <summary>Last encounter/visit date.</summary>
    [Id(10)]
    public DateTime? LastVisitDate { get; set; }
}

[GenerateSerializer]
public class iCareReminderItem
{
    [Id(0)]
    public string ReminderName { get; set; } = string.Empty;

    [Id(1)]
    public string Category { get; set; } = string.Empty;

    [Id(2)]
    public string Priority { get; set; } = string.Empty;

    [Id(3)]
    public DateTime? DueDate { get; set; }
}

[GenerateSerializer]
public class iCareQualityGap
{
    [Id(0)]
    public string MeasureId { get; set; } = string.Empty;

    [Id(1)]
    public string MeasureTitle { get; set; } = string.Empty;

    [Id(2)]
    public string ClinicalDomain { get; set; } = string.Empty;

    [Id(3)]
    public string GapDescription { get; set; } = string.Empty;
}

[GenerateSerializer]
public class iCareRegistryEntry
{
    [Id(0)]
    public string RegistryType { get; set; } = string.Empty;

    [Id(1)]
    public string EnrollmentStatus { get; set; } = string.Empty;

    [Id(2)]
    public string? KeyIndicator { get; set; }

    [Id(3)]
    public DateTime? LastIndicatorDate { get; set; }
}

/// <summary>
/// Result from generating the iCare dashboard.
/// </summary>
[GenerateSerializer]
public class iCareDashboardResult
{
    [Id(0)]
    public bool Success { get; set; }

    [Id(1)]
    public string? ErrorMessage { get; set; }

    [Id(2)]
    public List<iCarePatientSummary> PatientSummaries { get; set; } = new();

    [Id(3)]
    public DateTime GeneratedDate { get; set; }

    [Id(4)]
    public int TotalPatients { get; set; }

    [Id(5)]
    public int PatientsWithGaps { get; set; }

    [Id(6)]
    public int TotalDueReminders { get; set; }

    [Id(7)]
    public int TotalQualityGaps { get; set; }
}
