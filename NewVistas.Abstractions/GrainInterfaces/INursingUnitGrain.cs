// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Nursing unit census grain — VistA File #210 (NURSING UNIT).
/// Tracks bed assignments, charge nurse, and occupancy for a single inpatient unit.
/// Grain key: "NURS-UNIT:{unitId}"
/// </summary>
public interface INursingUnitGrain : IGrainWithStringKey
{
    /// <summary>Returns the full unit state including all bed assignments.</summary>
    Task<NursingUnitState> GetAsync();

    /// <summary>Initialises the unit record. No-op if already set up (idempotent).</summary>
    Task InitializeAsync(string unitName, string? unitType, int totalBeds);

    /// <summary>Sets the charge nurse for the current shift.</summary>
    Task SetChargeNurseAsync(string nurseId, string nurseName);

    /// <summary>
    /// Assigns a patient to a bed, creating the assignment if the bed does not exist.
    /// </summary>
    Task AssignPatientAsync(
        string bed,
        string patientId,
        string patientName,
        DateTime admissionDateTime,
        string? attendingNurseId,
        string? attendingNurseName);

    /// <summary>Clears the occupancy of a bed (discharge or transfer).</summary>
    Task DischargeFromBedAsync(string bed);

    /// <summary>Updates the acuity level shown for a bed occupant.</summary>
    Task UpdateBedAcuityAsync(string bed, AcuityLevel level);

    /// <summary>Returns the summary entry used by the unit index grain.</summary>
    Task<NursingUnitEntry> GetUnitEntryAsync();
}
