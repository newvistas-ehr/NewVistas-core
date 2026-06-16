// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Individual dental treatment / procedure record grain.
/// Maps to VistA File #228.1 DENTAL TREATMENT, managed by DENTX.m / DENPROC.m.
/// Grain key: "DENTAL-TX:{guid}".
/// </summary>
public interface IDentalTreatmentGrain : IGrainWithStringKey
{
    /// <summary>Returns the current treatment state.</summary>
    Task<DentalTreatmentState> GetAsync();

    /// <summary>
    /// Creates a new dental treatment record. Should be called only once per grain.
    /// </summary>
    Task CreateAsync(
        string patientId,
        DateTime treatmentDate,
        string procedureCode,
        string procedureDescription,
        DentalProcedureCategory procedureCategory,
        List<int> toothNumbers,
        List<string> surfaces,
        string providerId,
        string providerName,
        string? locationId,
        string? locationName,
        string? diagnosisCode,
        string? anesthesiaType,
        decimal? chargeAmount,
        string? notes);

    /// <summary>
    /// Marks the treatment as completed and records the completion date.
    /// </summary>
    Task CompleteAsync(DateTime completedDate, string completedByUserId, string? notes);

    /// <summary>
    /// Marks the treatment as cancelled with a reason.
    /// </summary>
    Task CancelAsync(string reason, string cancelledByUserId);

    /// <summary>
    /// Marks the treatment as referred to a specialist.
    /// </summary>
    Task ReferAsync(string referralReason, string referredByUserId);
}
