// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient cross-registry membership grain — key: "CCR-PAT:{patientId}"
/// Tracks which registries (HIV, HepC, Diabetes) a patient is enrolled in.
/// </summary>
public interface IPatientRegistryListGrain : IGrainWithStringKey
{
    /// <summary>Returns all registry enrollments for this patient.</summary>
    Task<List<PatientRegistryEnrollmentEntry>> GetAllEnrollmentsAsync();

    /// <summary>Returns only Active registry enrollments.</summary>
    Task<List<PatientRegistryEnrollmentEntry>> GetActiveEnrollmentsAsync();

    Task UpsertEnrollmentAsync(PatientRegistryEnrollmentEntry entry);

    /// <summary>Removes the enrollment for the specified registry type.</summary>
    Task RemoveEnrollmentAsync(RegistryType registryType);
}
