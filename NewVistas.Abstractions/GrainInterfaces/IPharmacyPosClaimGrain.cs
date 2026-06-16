// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Pharmacy POS Claim Grain — RPMS ABSP CLAIMS (File #9002313.02).
/// Key: "POS-CLAIM:{guid}"
///
/// Models a single NCPDP transaction: B1 billing, B2 reversal, E1 eligibility.
/// Additive feature grain per Site Flavor Architecture (Option 4).
/// </summary>
public interface IPharmacyPosClaimGrain : IGrainWithStringKey
{
    Task<GrainStates.PharmacyPosClaimState> GetAsync();

    Task CreateAsync(
        string patientId,
        string? prescriptionId,
        GrainStates.NcpdpTransactionType transactionType,
        string bin, string pcn, string ncpdpVersion,
        string? groupNumber, string? cardholderId, string? relationshipCode,
        string? insurerId, string? insurerName,
        string? ndc, string? drugName, decimal? quantityDispensed, int? daysSupply,
        DateTime? dateOfService,
        decimal? ingredientCostSubmitted, decimal? dispensingFeeSubmitted,
        decimal? usualAndCustomary,
        string? pharmacyNcpdpId, string? pharmacistName,
        string? prescriberNpi, string? prescriberName,
        string? originalClaimId);

    /// <summary>Records the adjudication response from the payer.</summary>
    Task AdjudicateAsync(
        GrainStates.PosClaimStatus status,
        decimal? insurancePaidAmount,
        decimal? patientResponsibility,
        decimal? copayAmount,
        decimal? coinsuranceAmount,
        decimal? deductibleAmount,
        string? authorizationNumber,
        List<GrainStates.PosRejection>? rejections,
        List<GrainStates.DurMessage>? durMessages);

    /// <summary>Marks the claim as reversed (B2 processed).</summary>
    Task ReverseAsync();

    /// <summary>Cancels a pending claim.</summary>
    Task CancelAsync();
}
