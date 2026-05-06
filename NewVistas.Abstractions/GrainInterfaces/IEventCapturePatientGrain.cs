// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Event Capture Patient Grain — tracks the list of all encounter IDs for a patient.
/// Based on VistA File #721 (EC PATIENT).
/// Acts as the per-patient index for Event Capture encounters.
///
/// Grain key format: "EC-PATIENT:{patientId}"
/// </summary>
public interface IEventCapturePatientGrain : IGrainWithStringKey
{
    /// <summary>Returns the full patient event capture state.</summary>
    Task<GrainStates.EventCapturePatientState> GetAsync();

    /// <summary>Adds an encounter ID to this patient's list.</summary>
    Task AddEncounterAsync(string encounterId, DateTime encounterDateTime);

    /// <summary>
    /// Returns the most recent encounter IDs for this patient,
    /// in reverse-chronological order.
    /// </summary>
    Task<List<GrainStates.EcPatientEncounterEntry>> GetEncounterEntriesAsync(int maxResults);
}
