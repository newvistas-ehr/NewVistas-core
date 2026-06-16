// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.PT.GrainStates;
using NewVistas.PT.Models;

namespace NewVistas.PT.GrainInterfaces;

/// <summary>
/// Manages the home exercise program for a patient.
/// Key format: "PTHEP:{patientId}"
/// </summary>
public interface IPTHomeExerciseProgramGrain : IGrainWithStringKey
{
    /// <summary>Returns the full HEP state.</summary>
    Task<PTHomeExerciseProgramState> GetProgramAsync();

    /// <summary>Adds a new exercise prescription. Returns the generated prescription ID.</summary>
    Task<string> AddPrescriptionAsync(HepPrescription prescription);

    /// <summary>Updates the status of an existing prescription.</summary>
    Task UpdatePrescriptionStatusAsync(string prescriptionId, HepStatus status);

    /// <summary>Logs a completion of a prescribed exercise. Returns the generated log ID.</summary>
    Task<string> LogCompletionAsync(HepCompletionLog log);

    /// <summary>Returns only active prescriptions.</summary>
    Task<List<HepPrescription>> GetActivePrescriptionsAsync();

    /// <summary>Returns completion logs, optionally filtered by prescription ID and date range.</summary>
    Task<List<HepCompletionLog>> GetCompletionLogsAsync(string? prescriptionId, DateTime? from, DateTime? to);
}
