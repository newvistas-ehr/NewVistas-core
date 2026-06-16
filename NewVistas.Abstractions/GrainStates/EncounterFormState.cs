// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for an encounter form template definition.
/// Maps to IHS RPMS PCC encounter form templates and VistA Reminder Dialogs.
/// Defines the fields, layout, and validation for a configurable data capture form.
/// </summary>
[GenerateSerializer]
public class EncounterFormTemplateState
{
    /// <summary>Unique template ID (grain key, e.g., "EF-TPL:{guid}").</summary>
    [Id(0)]
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>Template display name.</summary>
    [Id(1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Description of what this form captures.</summary>
    [Id(2)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Form type: ENCOUNTER, SCREENING, ASSESSMENT, PROCEDURE_NOTE, INTAKE, DISCHARGE, CUSTOM.</summary>
    [Id(3)]
    public string FormType { get; set; } = string.Empty;

    /// <summary>Optional clinic restriction (null = available to all clinics).</summary>
    [Id(4)]
    public string? ClinicId { get; set; }

    /// <summary>Status: DRAFT, PUBLISHED, RETIRED.</summary>
    [Id(5)]
    public string Status { get; set; } = "DRAFT";

    /// <summary>Ordered list of field definitions.</summary>
    [Id(6)]
    public List<EncounterFormFieldDefinition> Fields { get; set; } = new();

    /// <summary>Version number, incremented on each publish.</summary>
    [Id(7)]
    public int Version { get; set; } = 1;

    [Id(8)]
    public string CreatedByName { get; set; } = string.Empty;

    [Id(9)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(10)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Definition of a single field on an encounter form template.
/// </summary>
[GenerateSerializer]
public class EncounterFormFieldDefinition
{
    /// <summary>Unique field identifier within the template.</summary>
    [Id(0)]
    public string FieldId { get; set; } = string.Empty;

    /// <summary>Display label.</summary>
    [Id(1)]
    public string Label { get; set; } = string.Empty;

    /// <summary>Field type: TEXT, TEXTAREA, NUMBER, DATE, BOOLEAN, SELECT, MULTISELECT, VITALS, ICD10.</summary>
    [Id(2)]
    public string FieldType { get; set; } = "TEXT";

    /// <summary>Whether this field must be filled before submission.</summary>
    [Id(3)]
    public bool IsRequired { get; set; }

    /// <summary>Display order (lower = earlier).</summary>
    [Id(4)]
    public int DisplayOrder { get; set; }

    /// <summary>Dropdown/select options (pipe-delimited for SELECT/MULTISELECT).</summary>
    [Id(5)]
    public string? Options { get; set; }

    /// <summary>Default value for the field.</summary>
    [Id(6)]
    public string? DefaultValue { get; set; }

    /// <summary>Help text shown to the user.</summary>
    [Id(7)]
    public string? HelpText { get; set; }

    /// <summary>Section/group name for visual grouping.</summary>
    [Id(8)]
    public string? SectionName { get; set; }
}

/// <summary>
/// State for a completed or in-progress encounter form instance.
/// </summary>
[GenerateSerializer]
public class EncounterFormInstanceState
{
    /// <summary>Unique instance ID (grain key, e.g., "EF-INST:{guid}").</summary>
    [Id(0)]
    public string InstanceId { get; set; } = string.Empty;

    /// <summary>Template this instance is based on.</summary>
    [Id(1)]
    public string TemplateId { get; set; } = string.Empty;

    /// <summary>Template name for display.</summary>
    [Id(2)]
    public string TemplateName { get; set; } = string.Empty;

    /// <summary>Patient this form is for.</summary>
    [Id(3)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient name for display.</summary>
    [Id(4)]
    public string PatientName { get; set; } = string.Empty;

    /// <summary>Associated encounter/visit ID (optional).</summary>
    [Id(5)]
    public string? EncounterId { get; set; }

    /// <summary>Status: DRAFT, SUBMITTED, AMENDED, VOIDED.</summary>
    [Id(6)]
    public string Status { get; set; } = "DRAFT";

    /// <summary>Field values keyed by FieldId.</summary>
    [Id(7)]
    public Dictionary<string, string?> FieldValues { get; set; } = new();

    /// <summary>Provider who created this instance.</summary>
    [Id(8)]
    public string CreatedByProviderId { get; set; } = string.Empty;

    /// <summary>Provider name who created this instance.</summary>
    [Id(9)]
    public string CreatedByProviderName { get; set; } = string.Empty;

    /// <summary>When the form was submitted.</summary>
    [Id(10)]
    public DateTime? SubmittedDate { get; set; }

    /// <summary>Who submitted the form.</summary>
    [Id(11)]
    public string? SubmittedByName { get; set; }

    /// <summary>Amendment/void reason.</summary>
    [Id(12)]
    public string? AmendReason { get; set; }

    [Id(13)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(14)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Index entry for encounter form templates.
/// </summary>
[GenerateSerializer]
public class EncounterFormTemplateIndexEntry
{
    [Id(0)]
    public string TemplateId { get; set; } = string.Empty;

    [Id(1)]
    public string Name { get; set; } = string.Empty;

    [Id(2)]
    public string FormType { get; set; } = string.Empty;

    [Id(3)]
    public string Status { get; set; } = string.Empty;

    [Id(4)]
    public string? ClinicId { get; set; }

    [Id(5)]
    public int Version { get; set; }

    [Id(6)]
    public int FieldCount { get; set; }

    [Id(7)]
    public DateTime CreatedDate { get; set; }
}

/// <summary>
/// Index entry for encounter form instances.
/// </summary>
[GenerateSerializer]
public class EncounterFormInstanceIndexEntry
{
    [Id(0)]
    public string InstanceId { get; set; } = string.Empty;

    [Id(1)]
    public string TemplateId { get; set; } = string.Empty;

    [Id(2)]
    public string TemplateName { get; set; } = string.Empty;

    [Id(3)]
    public string PatientId { get; set; } = string.Empty;

    [Id(4)]
    public string PatientName { get; set; } = string.Empty;

    [Id(5)]
    public string Status { get; set; } = string.Empty;

    [Id(6)]
    public string CreatedByProviderName { get; set; } = string.Empty;

    [Id(7)]
    public DateTime CreatedDate { get; set; }

    [Id(8)]
    public DateTime? SubmittedDate { get; set; }
}

/// <summary>
/// Persistent state for the template index singleton.
/// </summary>
[GenerateSerializer]
public class EncounterFormTemplateIndexState
{
    [Id(0)]
    public Dictionary<string, EncounterFormTemplateIndexEntry> Entries { get; set; } = new();
}

/// <summary>
/// Persistent state for the instance index singleton.
/// </summary>
[GenerateSerializer]
public class EncounterFormInstanceIndexState
{
    [Id(0)]
    public Dictionary<string, EncounterFormInstanceIndexEntry> Entries { get; set; } = new();
}
