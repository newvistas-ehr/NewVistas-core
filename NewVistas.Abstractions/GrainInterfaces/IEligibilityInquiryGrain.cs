// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Grain interface for an individual insurance eligibility verification transaction (EDI 270/271).
/// Maps to VistA IB ELIGIBILITY VERIFICATION and X12 270/271 transaction sets.
/// MUMPS: IBCNEDE*.m, IBCNEUT.m, IBCNEDE1.m
/// Grain key: "ELIG-270:{guid}"
/// </summary>
public interface IEligibilityInquiryGrain : IGrainWithStringKey
{
    /// <summary>Returns the current inquiry state.</summary>
    Task<EligibilityInquiryState> GetAsync();

    /// <summary>
    /// Creates a new eligibility inquiry (270 transaction). Sets Status = Draft.
    /// </summary>
    Task<string> CreateAsync(
        string patientId,
        string? insurancePlanId,
        string? personalPolicyId,
        string payerId,
        string payerName,
        string subscriberId,
        string? subscriberName,
        string? relationshipToSubscriber,
        DateTime? patientDateOfBirth,
        List<string> serviceTypeCodes,
        DateTime serviceDate,
        string? initiatedByUserId,
        string? initiatedByUserName,
        string? notes);

    /// <summary>
    /// Marks the inquiry as submitted to the payer. Sets Status = Submitted.
    /// </summary>
    Task SubmitAsync(string? traceNumber);

    /// <summary>
    /// Records the payer's 271 response indicating the patient IS eligible.
    /// Sets Status = Eligible with coverage details and benefit information.
    /// </summary>
    Task RecordEligibleResponseAsync(
        CoverageLevel coverageLevel,
        string? responsePlanName,
        DateTime? coverageEffectiveDate,
        DateTime? coverageTerminationDate,
        string? responseGroupNumber,
        List<EligibilityBenefitDetail>? benefitDetails,
        string? payerMessage);

    /// <summary>
    /// Records the payer's 271 response indicating the patient is NOT eligible.
    /// Sets Status = Ineligible with reason information.
    /// </summary>
    Task RecordIneligibleResponseAsync(
        CoverageLevel coverageLevel,
        List<string>? rejectReasonCodes,
        string? payerMessage);

    /// <summary>
    /// Records an error response from the payer (AAA reject segments).
    /// Sets Status = Error.
    /// </summary>
    Task RecordErrorResponseAsync(
        List<string> rejectReasonCodes,
        string? payerMessage);
}
