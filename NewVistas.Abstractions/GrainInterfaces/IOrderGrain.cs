// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Order Grain Interface based on VistA ORDER file (#100)
/// Represents a single order in the system
/// </summary>
public interface IOrderGrain : IGrainWithStringKey
{
    /// <summary>
    /// Gets the complete order state
    /// </summary>
    Task<GrainStates.OrderState> GetOrderAsync();

    /// <summary>
    /// Creates a new order
    /// </summary>
    Task CreateOrderAsync(
        string patientId,
        string orderType,
        string orderableItem,
        string? orderableItemId,
        string providerId,
        string providerName,
        DateTime startDateTime,
        string? locationId,
        string? locationName,
        string urgency,
        string? instructions,
        string? reason,
        string? nature,
        string? createdBy);

    /// <summary>
    /// Updates order instructions
    /// </summary>
    Task UpdateInstructionsAsync(string instructions, string? modifiedBy);

    /// <summary>
    /// Updates order status
    /// </summary>
    Task UpdateStatusAsync(string status);

    /// <summary>
    /// Signs the order electronically
    /// </summary>
    Task SignOrderAsync(string signature, DateTime signatureDateTime);

    /// <summary>
    /// Releases the order to make it active
    /// </summary>
    Task ReleaseOrderAsync(DateTime releaseDateTime);

    /// <summary>
    /// Discontinues/cancels the order
    /// </summary>
    Task DiscontinueOrderAsync(
        DateTime discontinuedDateTime,
        string reason,
        string? discontinuedByProviderId);

    /// <summary>
    /// Marks order as completed
    /// </summary>
    Task CompleteOrderAsync(DateTime completedDateTime);

    /// <summary>
    /// Sets order stop date/time
    /// </summary>
    Task SetStopDateTimeAsync(DateTime stopDateTime);

    /// <summary>
    /// Adds comments to the order
    /// </summary>
    Task AddCommentsAsync(string comments);

    /// <summary>
    /// Flags the order
    /// </summary>
    Task FlagOrderAsync();

    /// <summary>
    /// Unflags the order
    /// </summary>
    Task UnflagOrderAsync();

    /// <summary>
    /// Places the order on hold. Mirrors VistA ORDER STATUS #3 (HOLD).
    /// Per VistA: "Not all packages may allow their orders to be placed on hold;
    /// Pharmacy orders may be placed on hold, but Lab orders cannot."
    /// </summary>
    Task HoldOrderAsync();

    /// <summary>
    /// Gets the patient ID for this order
    /// </summary>
    Task<string> GetPatientIdAsync();

    /// <summary>
    /// Gets the order type
    /// </summary>
    Task<string> GetOrderTypeAsync();

    /// <summary>
    /// Checks if order is active
    /// </summary>
    Task<bool> IsActiveAsync();

    /// <summary>
    /// Checks if order is completed
    /// </summary>
    Task<bool> IsCompletedAsync();

    // ── GAP 2: Missing Order Actions ──────────────────────────────────────────

    /// <summary>
    /// Renews the order (OA_RENEW / RN) — creates a continuation of an existing order.
    /// Sets Nature to "RENEWAL" and resets stop date.
    /// </summary>
    Task RenewOrderAsync(string renewedByProviderId, DateTime? newStopDateTime);

    /// <summary>
    /// Nurse verification of the order (OA_VERIFY / VR).
    /// Sets NurseVerifiedBy and updates SignatureStatus.
    /// </summary>
    Task VerifyOrderAsync(string nurseId, DateTime verifiedDateTime);

    /// <summary>
    /// Copies this order to a new order ID (OA_COPY).
    /// Records the source order ID in CopiedFromOrderId.
    /// </summary>
    Task SetCopiedFromAsync(string sourceOrderId);

    /// <summary>
    /// Releases an order that was previously placed on hold (OA_UNHOLD).
    /// </summary>
    Task UnholdOrderAsync();

    /// <summary>
    /// Transfers the order to a different location/service.
    /// </summary>
    Task TransferOrderAsync(string toLocationId, string toLocationName);

    /// <summary>
    /// Parks the order (OA_PARK / PK) — saves without releasing to the service.
    /// Parked orders require a subsequent unpark/release to become active.
    /// </summary>
    Task ParkOrderAsync(DateTime parkedDateTime);

    /// <summary>
    /// Unparks the order (OA_UNPARK / UP) — releases a parked order.
    /// </summary>
    Task UnparkOrderAsync(DateTime releaseDateTime);

    /// <summary>
    /// Sets the chart review record for the order.
    /// </summary>
    Task RecordChartReviewAsync(string reviewedByUserId, DateTime reviewedDateTime);

    // ── GAP 11: Event-Delayed Orders ──────────────────────────────────────────

    /// <summary>
    /// Marks this as an event-delayed order. Mirrors VistA TOrderDelayEvent.
    /// EventType: A = Admission, T = Transfer, D = Discharge, C = Cancel Event.
    /// </summary>
    Task SetEventDelayAsync(
        string eventType,
        string eventIen,
        string eventName,
        string? patientEventIen,
        DateTime? eventEffectiveDateTime);

    /// <summary>
    /// Releases a previously event-delayed order (the triggering event has occurred).
    /// </summary>
    Task ReleaseDelayedOrderAsync(DateTime releasedDateTime);

    // ── GAP 7: Billing Treatment Factors ─────────────────────────────────────

    /// <summary>
    /// Sets CIDC billing treatment factors for the order.
    /// Mirrors VistA UBACore.pas treatment factor flags (SC/AO/IR/SWAC/MST/HNC/CV/SHAD/CL).
    /// </summary>
    Task SetTreatmentFactorsAsync(
        bool serviceConnected,
        bool agentOrange,
        bool ionizingRadiation,
        bool southwestAsia,
        bool mst,
        bool headNeckCancer,
        bool combatVeteran,
        bool shad,
        bool campLejeune);

    /// <summary>
    /// Associates ICD-10 diagnosis codes with this order for CIDC billing.
    /// </summary>
    Task SetAssociatedDiagnosisCodesAsync(List<string> icd10Codes);

    // ── GAP 3: Controlled Substance ───────────────────────────────────────────

    /// <summary>
    /// Sets the controlled substance flags for this order.
    /// Mirrors VistA rOrders.pas TOrder.IsControlledSubstance / IsDetox.
    /// </summary>
    Task SetControlledSubstanceAsync(bool isControlledSubstance, bool isDetox);
}
