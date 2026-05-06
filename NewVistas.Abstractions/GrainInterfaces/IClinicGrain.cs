// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Clinic Grain Interface based on VistA HOSPITAL LOCATION file (#44).
/// Represents a single schedulable clinic or hospital location.
/// MUMPS references: SDAM2.m, SDCO.m, SDCOU.m
/// </summary>
public interface IClinicGrain : IGrainWithStringKey
{
    /// <summary>
    /// Gets the complete clinic state.
    /// </summary>
    Task<GrainStates.ClinicState> GetClinicAsync();

    /// <summary>
    /// Creates or initializes a clinic record.
    /// VistA File #44 fields: .01 (NAME), 1 (ABBREVIATION), 3 (TYPE), 8 (PHONE)
    /// </summary>
    Task CreateClinicAsync(
        string name,
        string? division,
        string? stopCode,
        string? phoneNumber,
        string? physicalLocation,
        int appointmentLength,
        int maxPatientsPerDay,
        bool allowOverbooking,
        string? clinicType,
        string? primaryProviderId,
        string? primaryProviderName);

    /// <summary>
    /// Updates mutable clinic fields.
    /// </summary>
    Task UpdateClinicAsync(
        string? name,
        string? division,
        string? phoneNumber,
        string? physicalLocation,
        int? appointmentLength,
        int? maxPatientsPerDay,
        bool? allowOverbooking,
        string? status);
}
