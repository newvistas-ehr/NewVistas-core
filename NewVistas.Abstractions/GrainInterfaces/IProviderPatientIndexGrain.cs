// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Provider Patient Index Grain — the "My Patients" list for a provider.
/// Tracks all patients assigned to a provider with denormalized demographics for fast search.
///
/// Key pattern: "PROV-PAT-IDX:{providerId}"
/// </summary>
public interface IProviderPatientIndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Add or update a patient entry. Upserts by PatientId.
    /// </summary>
    Task AddOrUpdatePatientAsync(ProviderPatientEntry entry);

    /// <summary>
    /// Remove a patient entry entirely.
    /// </summary>
    Task RemovePatientAsync(string patientId);

    /// <summary>
    /// Deactivate a patient (set IsActive=false) without removing from the list.
    /// </summary>
    Task DeactivatePatientAsync(string patientId);

    /// <summary>
    /// Get all patients (active and inactive).
    /// </summary>
    Task<List<ProviderPatientEntry>> GetAllPatientsAsync();

    /// <summary>
    /// Get only active patients, ordered by name.
    /// </summary>
    Task<List<ProviderPatientEntry>> GetActivePatientsAsync();

    /// <summary>
    /// Search active patients by name prefix (case-insensitive).
    /// </summary>
    Task<List<ProviderPatientEntry>> SearchPatientsAsync(string nameFilter);

    /// <summary>
    /// Get active patients filtered by relationship type (e.g. "PCP", "SPECIALIST").
    /// </summary>
    Task<List<ProviderPatientEntry>> GetPatientsByRoleAsync(string role);

    /// <summary>
    /// Update the LastSeenDate for a patient (called on appointment checkout).
    /// </summary>
    Task UpdateLastSeenAsync(string patientId, DateTime lastSeenDate);

    /// <summary>
    /// Update the NextAppointmentDate for a patient.
    /// </summary>
    Task UpdateNextAppointmentAsync(string patientId, DateTime? nextAppointmentDate);
}
