// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Event Capture Encounter Grain — represents a single workload/encounter capture record.
/// Based on VistA Event Capture (EC) package, File #721 (EC PATIENT) and #721.3 (EC ENCOUNTER).
/// MUMPS routines: ECPEC.m, ECPEEN.m, ECPEEU.m, ECPEWL.m.
///
/// Grain key format: "EC-ENCOUNTER:{guid}"
/// </summary>
public interface IEventCaptureEncounterGrain : IGrainWithStringKey
{
    /// <summary>Returns the full encounter state.</summary>
    Task<GrainStates.EventCaptureEncounterState> GetEncounterAsync();

    /// <summary>
    /// Creates a new workload encounter record.
    /// Equivalent to ECPEEN CREATE in VistA.
    /// </summary>
    Task CreateAsync(
        string patientId,
        DateTime encounterDateTime,
        string dssUnitId,
        string dssUnitName,
        string? dssUnitCode,
        string? clinicId,
        string? clinicName,
        string? locationId,
        string? locationName,
        string primaryProviderId,
        string primaryProviderName,
        string? attendingProviderId,
        string? attendingProviderName,
        GrainStates.EcEncounterType encounterType,
        GrainStates.EcPatientCategory patientCategory,
        string? primaryStopCode,
        string? creditStopCode,
        string? comments);

    /// <summary>
    /// Adds a procedure to the encounter.
    /// Equivalent to ECPEEN PROC in VistA (File #721.3 sub-file).
    /// </summary>
    Task AddProcedureAsync(
        string cptCode,
        string procedureDescription,
        int quantity,
        string providerId,
        string providerName,
        string? modifierCode);

    /// <summary>
    /// Removes a procedure from the encounter by CPT code and provider.
    /// </summary>
    Task RemoveProcedureAsync(string cptCode, string providerId);

    /// <summary>
    /// Adds a diagnosis code to the encounter.
    /// </summary>
    Task AddDiagnosisAsync(string icd10Code, string description, bool isPrimary);

    /// <summary>
    /// Records check-out time and sets status to Complete.
    /// Equivalent to ECPEEN COMPLETE in VistA.
    /// </summary>
    Task CompleteAsync(DateTime checkOutDateTime, int? visitLengthMinutes);

    /// <summary>
    /// Marks the encounter as deleted (soft delete).
    /// Equivalent to ECPEEN DELETE in VistA.
    /// </summary>
    Task DeleteAsync(string deletedByProviderId, string deletedByProviderName, string? reason);

    /// <summary>
    /// Updates the primary DSS unit assignment.
    /// Equivalent to ECPEEN EDIT in VistA.
    /// </summary>
    Task UpdateDssUnitAsync(string dssUnitId, string dssUnitName, string? dssUnitCode);
}
