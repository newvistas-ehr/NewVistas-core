// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Care Team state — tracks which providers are assigned to a patient's care team.
///
/// Derived from VistA PCMM (Primary Care Management Module), File #404.43 (Patient-Team Assignment).
/// Key pattern: "CARE-TEAM:{patientId}"
/// </summary>
[GenerateSerializer]
public class CareTeamState
{
    /// <summary>
    /// Patient ID this care team belongs to.
    /// </summary>
    [Id(0)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Members of this patient's care team — providers with various roles.
    /// </summary>
    [Id(1)]
    public List<CareTeamMember> Members { get; set; } = new();

    /// <summary>
    /// Date record last modified.
    /// </summary>
    [Id(2)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date record created.
    /// </summary>
    [Id(3)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A single member of a patient's care team.
/// Tracks provider identity, role, assignment source, and expiration.
///
/// VistA PCMM File #404.43 fields:
///   (.01) Patient — PatientId (on parent state)
///   (.02) Team Position — Role
///   (.03) Provider — ProviderId
///   (.04) Effective Date — AssignmentDate
///   (.05) Inactivate Date — ExpirationDate / DeactivationDate
/// </summary>
[GenerateSerializer]
public class CareTeamMember
{
    /// <summary>
    /// Provider's user ID (matches INewPersonGrain key suffix).
    /// </summary>
    [Id(0)]
    public string ProviderId { get; set; } = string.Empty;

    /// <summary>
    /// Provider's display name (denormalized for fast listing).
    /// </summary>
    [Id(1)]
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Team role — PCP, SPECIALIST, NURSE, ATTENDING, CONSULTING, SOCIAL_WORKER, PHARMACIST.
    /// Maps to PCMM team position types.
    /// </summary>
    [Id(2)]
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Provider's clinical specialty (e.g. "CARDIOLOGY", "INTERNAL MEDICINE").
    /// </summary>
    [Id(3)]
    public string? Specialty { get; set; }

    /// <summary>
    /// Date the provider was assigned to this care team.
    /// </summary>
    [Id(4)]
    public DateTime AssignmentDate { get; set; }

    /// <summary>
    /// Optional expiration date for the assignment.
    /// Appointment-sourced memberships default to 90 days from appointment date.
    /// Manual assignments have no expiration (null).
    /// </summary>
    [Id(5)]
    public DateTime? ExpirationDate { get; set; }

    /// <summary>
    /// Whether this member is currently active on the care team.
    /// RemoveMember sets this to false rather than deleting — maintains audit trail.
    /// </summary>
    [Id(6)]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// How this provider was added — MANUAL, APPOINTMENT, CONSULT, ADMISSION.
    /// </summary>
    [Id(7)]
    public string AssignmentSource { get; set; } = string.Empty;

    /// <summary>
    /// Date the provider last saw this patient (updated on appointment checkout).
    /// </summary>
    [Id(8)]
    public DateTime? LastSeenDate { get; set; }

    /// <summary>
    /// Date this member was deactivated (removed from team). Null if still active.
    /// </summary>
    [Id(9)]
    public DateTime? DeactivationDate { get; set; }
}
