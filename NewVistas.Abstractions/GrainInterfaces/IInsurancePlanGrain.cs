// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Group Insurance Plan grain (VistA File #355.3 GROUP INSURANCE PLAN).
/// Represents a shared plan definition (Medicare, CHAMPVA, TRICARE, commercial group plan)
/// that individual patient personal policies (File #355.7) reference.
///
/// MUMPS routines: IBINSU*.m (insurance management), IBINSP*.m (plan management).
/// Grain key: "IB-PLAN:{planId}"
/// </summary>
public interface IInsurancePlanGrain : IGrainWithStringKey
{
    /// <summary>Returns the full group insurance plan record.</summary>
    Task<GrainStates.InsurancePlanState> GetAsync();

    /// <summary>
    /// Creates the group insurance plan record.
    /// Returns the plan ID (same as the grain key suffix).
    /// </summary>
    Task<string> CreateAsync(
        string groupPlanName,
        string insuranceCompanyName,
        string? planType,
        string? groupNumber,
        string? coverageType,
        DateTime? effectiveDate,
        DateTime? expirationDate,
        decimal? coinsurancePercent,
        decimal? deductibleAmount,
        decimal? annualMaxBenefit,
        string? claimsAddress,
        string? claimsPhone,
        string? pharmacyBinNumber,
        string? pharmacyPcnNumber,
        int? filingTimeFrameDays,
        bool isPreCertRequired,
        bool allowsElectronicVerification,
        string? notes);

    /// <summary>
    /// Records an insurance eligibility verification event.
    /// Updates VerificationDateTime and VerificationSource.
    /// </summary>
    Task VerifyAsync(string verificationSource, DateTime verificationDateTime);

    /// <summary>Marks this plan as inactive (no longer accepting new enrollments).</summary>
    Task DeactivateAsync();
}
