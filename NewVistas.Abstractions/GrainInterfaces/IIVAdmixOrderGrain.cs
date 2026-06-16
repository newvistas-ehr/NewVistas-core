// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Represents a single IV admixture compounding order.
/// Maps to VistA Files #53.4 (IV ORDERS) and #50.8 (IV ADDITIVE).
/// MUMPS routines: PSJIV.m, PSJVXU.m, PSJLBL.m
/// Grain key pattern: "IVAD-ORDER:{guid}"
/// </summary>
public interface IIVAdmixOrderGrain : IGrainWithStringKey
{
    /// <summary>Returns the complete state of this IV admixture order.</summary>
    Task<IVAdmixOrderState> GetOrderAsync();

    /// <summary>
    /// Creates a new IV admixture order.
    /// PSJIV.m ORDER.
    /// </summary>
    Task CreateOrderAsync(
        string patientId,
        string baseSolution,
        int baseSolutionVolumeMl,
        IVAdmixRoute route,
        IVAdmixFrequency frequency,
        IVContainerType containerType,
        int containerCount,
        IVAdmixPriority priority,
        string? linkedInpatientOrderId,
        string? infusionRateStr,
        decimal? infusionRateMlHr,
        decimal? infusionDurationHours,
        string? routeDescription,
        string? frequencyDescription,
        DateTime? startDateTime,
        DateTime? stopDateTime,
        string? providerId,
        string? providerName,
        string? notes);

    /// <summary>Adds a drug additive or base solution component to the order.</summary>
    Task AddAdditiveAsync(IVAdmixAdditive additive);

    /// <summary>Removes a drug additive by drug name.</summary>
    Task RemoveAdditiveAsync(string drugName);

    /// <summary>
    /// Pharmacist verification of the IV order.
    /// PSJVXU.m VERIFY. Status → Verified.
    /// </summary>
    Task VerifyOrderAsync(
        string pharmacistId,
        string pharmacistName,
        DateTime verifiedDate);

    /// <summary>
    /// Marks the order as in compounding (technician has started mixing).
    /// Status → Compounding.
    /// </summary>
    Task StartCompoundingAsync(
        string compoundedById,
        string compoundedByName,
        DateTime startDate);

    /// <summary>
    /// Completes compounding and assigns lot/expiration.
    /// PSJLBL.m COMPLETE. Status → Ready.
    /// </summary>
    Task CompleteCompoundingAsync(
        DateTime completedDate,
        string? lotNumber,
        DateTime? expirationDate);

    /// <summary>
    /// Records that the IV label was printed.
    /// PSJLBL.m PRINT.
    /// </summary>
    Task PrintLabelAsync(string printedBy, DateTime printedDate);

    /// <summary>
    /// Records dispensing of the admixture to the ward.
    /// Status → Dispensed.
    /// </summary>
    Task DispenseOrderAsync(DateTime dispensingDateTime);

    /// <summary>
    /// Records administration of the admixture to the patient.
    /// Status → Administered.
    /// </summary>
    Task RecordAdministrationAsync(DateTime administrationDateTime);

    /// <summary>
    /// Discontinues the order with a reason.
    /// Status → Discontinued.
    /// </summary>
    Task DiscontinueOrderAsync(string reason);

    /// <summary>
    /// Cancels the order with a reason.
    /// Status → Cancelled.
    /// </summary>
    Task CancelOrderAsync(string reason);

    /// <summary>Updates the scheduled start and stop date/time for the infusion.</summary>
    Task UpdateScheduleAsync(DateTime? startDateTime, DateTime? stopDateTime);

    /// <summary>Sets the total volume (recalculated after additives are added).</summary>
    Task SetTotalVolumeAsync(int totalVolumeMl);
}
