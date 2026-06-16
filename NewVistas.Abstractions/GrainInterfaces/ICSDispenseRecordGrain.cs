// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Grain representing a single DEA-required controlled substance patient dispense record.
/// VistA File #58.80 — PSNLOG.m, PSNCS.m
/// Grain key: "CS-DISPENSE:{guid}"
/// </summary>
public interface ICSDispenseRecordGrain : IGrainWithStringKey
{
    /// <summary>Returns the full dispense record state.</summary>
    Task<CSDispenseRecordState> GetRecordAsync();

    /// <summary>Creates a new CS dispense record.</summary>
    Task CreateRecordAsync(
        string locationId,
        string locationName,
        string patientId,
        string patientName,
        DateTime? patientDateOfBirth,
        string drugId,
        string drugName,
        DEADrugSchedule deaSchedule,
        string? ndcNumber,
        decimal quantityDispensed,
        string unitOfMeasure,
        decimal runningBalance,
        CSDispenseType dispenseType,
        string prescriberId,
        string prescriberName,
        string? prescriberDEANumber,
        string dispensedById,
        string dispensedByName,
        string? witnessId,
        string? witnessName,
        DateTime dispenseDateTime,
        string? prescriptionNumber,
        string? orderId,
        string? notes);
}
