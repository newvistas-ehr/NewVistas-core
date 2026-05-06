// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

// ─── Relationship type enum ───────────────────────────────────────────────────

/// <summary>Relationship of a contact person to the patient (VistA File #408.12).</summary>
[GenerateSerializer]
public enum RelationshipType
{
    /// <summary>Legal spouse or domestic partner.</summary>
    Spouse = 0,

    /// <summary>Child (biological, adopted, or step-child).</summary>
    Child = 1,

    /// <summary>Parent or step-parent.</summary>
    Parent = 2,

    /// <summary>Sibling.</summary>
    Sibling = 3,

    /// <summary>Legal guardian.</summary>
    Guardian = 4,

    /// <summary>Emergency contact (not otherwise classified).</summary>
    EmergencyContact = 5,

    /// <summary>Holder of medical or durable power of attorney.</summary>
    PowerOfAttorney = 6,

    /// <summary>Other relationship type.</summary>
    Other = 7,
}

// ─── Patient relation record ──────────────────────────────────────────────────

/// <summary>
/// A single patient relation / emergency contact record (VistA File #408.12 PATIENT RELATION).
/// </summary>
[GenerateSerializer]
public record PatientRelation
{
    /// <summary>Unique identifier for this relation record.</summary>
    [Id(0)] public string RelationId { get; init; } = string.Empty;

    /// <summary>Type of relationship to the patient.</summary>
    [Id(1)] public RelationshipType RelationshipType { get; init; }

    /// <summary>Full name of the related person.</summary>
    [Id(2)] public string Name { get; init; } = string.Empty;

    /// <summary>Primary telephone number.</summary>
    [Id(3)] public string? Phone { get; init; }

    /// <summary>Alternate / cell telephone number.</summary>
    [Id(4)] public string? AlternatePhone { get; init; }

    /// <summary>Mailing address of the related person.</summary>
    [Id(5)] public string? Address { get; init; }

    /// <summary>Whether this person is designated as primary next-of-kin.</summary>
    [Id(6)] public bool IsPrimaryNextOfKin { get; init; }

    /// <summary>Whether this person is designated as an emergency contact.</summary>
    [Id(7)] public bool IsEmergencyContact { get; init; }

    /// <summary>Free-text notes about this relation.</summary>
    [Id(8)] public string? Notes { get; init; }
}

// ─── Patient relation aggregate — VistA File #408.12 ────────────────────────

/// <summary>
/// Aggregate of all relation/emergency contact records for a single patient
/// (VistA File #408.12 PATIENT RELATION).
/// </summary>
[GenerateSerializer]
public class PatientRelationState
{
    /// <summary>Patient identifier.</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>All patient relation records.</summary>
    [Id(1)] public List<PatientRelation> Relations { get; set; } = new();

    /// <summary>UTC timestamp when this record was first created.</summary>
    [Id(2)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of the most recent modification.</summary>
    [Id(3)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
