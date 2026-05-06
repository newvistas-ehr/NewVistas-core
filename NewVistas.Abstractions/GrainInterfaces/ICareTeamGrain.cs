// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Care Team Grain — manages the list of providers assigned to a patient's care team.
///
/// Derived from VistA PCMM (Primary Care Management Module), File #404.43 (Patient-Team Assignment).
/// Supports automatic team building from appointments and manual provider assignment.
///
/// Key pattern: "CARE-TEAM:{patientId}"
/// </summary>
public interface ICareTeamGrain : IGrainWithStringKey
{
    /// <summary>
    /// Add a provider to the care team. Idempotent — if the provider already exists and is active,
    /// updates role/specialty/expiration. If inactive, reactivates with new dates.
    /// </summary>
    Task AddMemberAsync(string providerId, string providerName, string role,
        string? specialty, string assignmentSource, DateTime? expirationDate);

    /// <summary>
    /// Remove a provider from the care team. Sets IsActive=false with DeactivationDate
    /// rather than deleting — maintains audit trail.
    /// </summary>
    Task RemoveMemberAsync(string providerId);

    /// <summary>
    /// Get all care team members (active and inactive).
    /// </summary>
    Task<List<CareTeamMember>> GetMembersAsync();

    /// <summary>
    /// Get only active care team members (IsActive=true and not expired).
    /// </summary>
    Task<List<CareTeamMember>> GetActiveMembersAsync();

    /// <summary>
    /// Set the Primary Care Provider. Deactivates the previous PCP if different.
    /// Only one PCP can be active at a time.
    /// </summary>
    Task SetPcpAsync(string providerId, string providerName, string? specialty);

    /// <summary>
    /// Get the current PCP, or null if none assigned.
    /// </summary>
    Task<CareTeamMember?> GetPcpAsync();

    /// <summary>
    /// Check whether any member (active or inactive) exists for this provider.
    /// </summary>
    Task<bool> HasMemberAsync(string providerId);

    /// <summary>
    /// Check whether an active (non-expired) member exists for this provider.
    /// Used by access control to determine record access rights.
    /// </summary>
    Task<bool> HasActiveMemberAsync(string providerId);

    /// <summary>
    /// Get active care team members filtered by role (e.g. "SPECIALIST", "NURSE").
    /// </summary>
    Task<List<CareTeamMember>> GetMembersByRoleAsync(string role);

    /// <summary>
    /// Update the LastSeenDate for a provider (called on appointment checkout).
    /// </summary>
    Task UpdateMemberLastSeenAsync(string providerId, DateTime lastSeenDate);
}
