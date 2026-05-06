// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-ward patient census grain.
/// Provides a fast, consistent view of who is currently admitted to a ward.
/// Grain key pattern: "WARD-CENSUS:{wardId}"
/// </summary>
public interface IWardCensusGrain : IGrainWithStringKey
{
    /// <summary>Adds a new patient or updates an existing patient's census entry (upsert by PatientId).</summary>
    Task AddOrUpdatePatientAsync(WardCensusEntry entry);

    /// <summary>Removes a patient from this ward's census (on discharge or transfer out).</summary>
    Task RemovePatientAsync(string patientId);

    /// <summary>Returns all current patients on this ward.</summary>
    Task<List<WardCensusEntry>> GetCensusAsync();

    /// <summary>Returns the count of patients currently on this ward.</summary>
    Task<int> GetPatientCountAsync();
}
