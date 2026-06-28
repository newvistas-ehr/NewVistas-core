// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Pharmacy Grain Interface based on VistA PRESCRIPTION file (#52)
/// </summary>
public interface IPharmacyGrain : IGrainWithStringKey
{
    Task<GrainStates.PharmacyState> GetPrescriptionAsync();

    Task CreatePrescriptionAsync(
        string patientId,
        string drugName,
        string? drugId,
        string? dosage,
        string? route,
        string? schedule,
        string? sig,
        int? daysSupply,
        int? quantity,
        int? refills,
        string? providerId,
        string? providerName,
        string? pharmacyId,
        string? pharmacyName,
        string? orderId,
        string? comments);

    /// <summary>
    /// Transmit this prescription to its destination pharmacy as an NCPDP SCRIPT NewRx (over
    /// Surescripts) and record the outcome on the Rx. The offline default does not send.
    /// </summary>
    Task TransmitNewRxAsync(string? pharmacyNcpdpId);

    Task FillPrescriptionAsync(DateTime fillDate);
    Task DiscontinueAsync(string reason);
    Task ExpireAsync();
    Task PlaceOnHoldAsync(string reason);
    Task ResumeAsync();
    Task RefillAsync(DateTime fillDate);
    Task VerifyAsync(string pharmacistId);
    Task SetCounselingFlagAsync(bool required);
    Task PrintLabelAsync(string? rxNumber);
    Task UpdatePriorityAsync(string priority);
    Task<List<GrainStates.RefillRecord>> GetRefillHistoryAsync();
    Task AddRefillRecordAsync(GrainStates.RefillRecord record);

    /// <summary>
    /// Checks whether this prescription is eligible for refill without performing it.
    /// Returns a structured result with eligibility status, reasons, dates, and constraints.
    /// VistA reference: PSO refill date calculation, CalcMaxRefills, DEA schedule rules.
    /// </summary>
    Task<GrainStates.RefillEligibilityResult> CheckRefillEligibilityAsync(DateTime proposedFillDate);

    /// <summary>
    /// Records the actual product dispensed (NDC and lot number) for audit and tracking.
    /// Called after fill/refill to document what was physically dispensed.
    /// VistA reference: PSO dispense recording.
    /// </summary>
    Task RecordDispenseAsync(string? ndcDispensed, string? lotNumber, string? pharmacistId);

    /// <summary>
    /// Records completion of patient counseling session.
    /// Guard: CounselingRequired must be true.
    /// VistA reference: PSOCP.m, PSOCP1.m patient counseling.
    /// </summary>
    Task RecordCounselingAsync(string pharmacistId, string? notes);

    /// <summary>
    /// Generates structured label content from the current prescription state.
    /// Guard: must be verified.
    /// VistA reference: PSJLBL.m label generation.
    /// </summary>
    Task<GrainStates.PrescriptionLabelContent> GenerateLabelContentAsync();

    // ── GAP 1: Medication Status Group ───────────────────────────────────────

    /// <summary>
    /// Recalculates and sets the MedStatusGroup based on current Status.
    /// Group 0 = ACTIVE, 1 = PENDING, 2 = NON-ACTIVE.
    /// Mirrors VistA rMeds.pas MedStatusGroup() function.
    /// </summary>
    Task UpdateMedStatusGroupAsync();

    // ── GAP 3: DEA / Controlled Substance ────────────────────────────────────

    /// <summary>
    /// Records the result of a DEA controlled substance check.
    /// Mirrors VistA rODMeds.pas DEACheckFailed / DEACheckFailedAtSignature.
    /// </summary>
    Task SetDeaCheckResultAsync(
        bool isControlledSubstance,
        string? deaSchedule,
        bool passed,
        string? failedReason);

    // ── GAP 4: Qty / Days / Refills Constraints ───────────────────────────────

    /// <summary>
    /// Sets the calculated maximum refills, days supply, and quantity constraints.
    /// Mirrors VistA rODMeds.pas CalcMaxRefills() / QtyToDays() / DaysToQty().
    /// </summary>
    Task SetDispenseConstraintsAsync(
        int? maxRefills,
        int? maxDaysSupply,
        int? maxQuantity,
        bool isTitration,
        bool isDischarge,
        bool scheduleRequired);
}
