// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

// ─── Enums ────────────────────────────────────────────────────────────────────

/// <summary>
/// Data components that can be included in a health summary report.
/// Maps to VistA HEALTH SUMMARY COMPONENT file (#142.1).
/// </summary>
[GenerateSerializer]
public enum HealthSummaryComponentType
{
    /// <summary>Patient demographic information.</summary>
    Demographics = 0,

    /// <summary>Active problem list (File #9000011).</summary>
    ActiveProblems = 1,

    /// <summary>Allergy/adverse reaction list (File #120.8).</summary>
    Allergies = 2,

    /// <summary>Current outpatient medications (File #52).</summary>
    CurrentMedications = 3,

    /// <summary>Inpatient medications/MAR (File #55).</summary>
    InpatientMedications = 4,

    /// <summary>Most recent vital signs (File #120.5).</summary>
    VitalSigns = 5,

    /// <summary>Recent lab results (File #63).</summary>
    LabResults = 6,

    /// <summary>Radiology reports (File #75.1).</summary>
    Radiology = 7,

    /// <summary>Consult requests/results (File #123).</summary>
    Consults = 8,

    /// <summary>TIU clinical notes/documents (File #8925).</summary>
    ClinicalNotes = 9,

    /// <summary>Future and past appointments (File #2.98).</summary>
    Appointments = 10,

    /// <summary>Immunization history (File #9000010.11).</summary>
    Immunizations = 11,

    /// <summary>Clinical reminders due or evaluated (File #811.9).</summary>
    ClinicalReminders = 12,

    /// <summary>Health factors recorded (File #9000010.23).</summary>
    HealthFactors = 13,

    /// <summary>Surgical procedures (File #130).</summary>
    SurgicalProcedures = 14,

    /// <summary>Service-connected conditions (File #396).</summary>
    ServiceConnectedConditions = 15,

    /// <summary>Mental health assessments (File #601.2).</summary>
    MentalHealth = 16,

    /// <summary>Dietetics/nutrition assessments (File #115).</summary>
    Dietetics = 17,
}

/// <summary>Status of a health summary type template.</summary>
[GenerateSerializer]
public enum HealthSummaryTypeStatus
{
    /// <summary>Template is active and available for use.</summary>
    Active = 0,

    /// <summary>Template is inactive and not available for new summaries.</summary>
    Inactive = 1,
}

// ─── Nested Types ─────────────────────────────────────────────────────────────

/// <summary>
/// Configuration for a single component within a health summary type template.
/// Controls which data is pulled and how it is displayed.
/// VistA HEALTH SUMMARY COMPONENT sub-file (#142.01).
/// </summary>
[GenerateSerializer]
public record HealthSummaryComponentConfig
{
    /// <summary>(.01) Type of clinical data to include.</summary>
    [Id(0)] public HealthSummaryComponentType ComponentType { get; init; }

    /// <summary>(.02) Whether this component is enabled in the template.</summary>
    [Id(1)] public bool IsEnabled { get; init; } = true;

    /// <summary>(.03) Display order within the summary (lower = earlier).</summary>
    [Id(2)] public int DisplayOrder { get; init; }

    /// <summary>(.04) Maximum number of entries to include (0 = unlimited).</summary>
    [Id(3)] public int MaxOccurrences { get; init; } = 10;

    /// <summary>(.05) Restrict to entries within this many days (0 = all time).</summary>
    [Id(4)] public int DaysBack { get; init; } = 0;

    /// <summary>(.06) Override section header text (null = use default component name).</summary>
    [Id(5)] public string? SectionHeader { get; init; }
}

/// <summary>
/// Lightweight index entry for the health summary type index grain.
/// </summary>
[GenerateSerializer]
public record HealthSummaryTypeIndexEntry
{
    /// <summary>Unique template identifier.</summary>
    [Id(0)] public string TypeId { get; init; } = string.Empty;

    /// <summary>Display name of the template.</summary>
    [Id(1)] public string Name { get; init; } = string.Empty;

    /// <summary>Optional description.</summary>
    [Id(2)] public string? Description { get; init; }

    /// <summary>Active/inactive status.</summary>
    [Id(3)] public HealthSummaryTypeStatus Status { get; init; }

    /// <summary>Number of components configured in this template.</summary>
    [Id(4)] public int ComponentCount { get; init; }

    /// <summary>Date the template was created.</summary>
    [Id(5)] public DateTime CreatedDate { get; init; }
}

// ─── State ────────────────────────────────────────────────────────────────────

/// <summary>
/// State for a health summary type (report template).
/// VistA HEALTH SUMMARY TYPE file (#142).
/// </summary>
[GenerateSerializer]
public class HealthSummaryTypeState
{
    /// <summary>(.01) Unique identifier for this template.</summary>
    [Id(0)] public string TypeId { get; set; } = string.Empty;

    /// <summary>(.02) Display name shown in CPRS health summary selector.</summary>
    [Id(1)] public string Name { get; set; } = string.Empty;

    /// <summary>(.03) Optional longer description of this template's purpose.</summary>
    [Id(2)] public string? Description { get; set; }

    /// <summary>(.04) Active/inactive status of this template.</summary>
    [Id(3)] public HealthSummaryTypeStatus Status { get; set; } = HealthSummaryTypeStatus.Active;

    /// <summary>(.05) Ordered list of component configurations for this template.</summary>
    [Id(4)] public List<HealthSummaryComponentConfig> Components { get; set; } = new();

    /// <summary>(.06) User ID who created this template.</summary>
    [Id(5)] public string CreatedById { get; set; } = string.Empty;

    /// <summary>(.07) Display name of the creator.</summary>
    [Id(6)] public string CreatedByName { get; set; } = string.Empty;

    /// <summary>(.08) Timestamp when the template was first created.</summary>
    [Id(7)] public DateTime CreatedDate { get; set; }

    /// <summary>(.09) Timestamp of the most recent modification.</summary>
    [Id(8)] public DateTime LastModifiedDate { get; set; }
}

// ─── Index State ──────────────────────────────────────────────────────────────

/// <summary>
/// State for the singleton health summary type index grain.
/// </summary>
[GenerateSerializer]
public class HealthSummaryTypeIndexState
{
    /// <summary>All registered health summary type templates.</summary>
    [Id(0)] public List<HealthSummaryTypeIndexEntry> Entries { get; set; } = new();
}
