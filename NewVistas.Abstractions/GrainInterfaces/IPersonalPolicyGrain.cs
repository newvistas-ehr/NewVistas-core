// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Personal Insurance Policy grain (VistA File #355.7 PERSONAL POLICY).
/// Represents a specific patient's enrollment in a group insurance plan,
/// tracking subscriber details, coverage dates, COBRA status, and deductible tracking.
///
/// MUMPS routines: IBINSP*.m (personal policy management).
/// Grain key: "IB-POLICY:{policyId}"
/// </summary>
public interface IPersonalPolicyGrain : IGrainWithStringKey
{
    /// <summary>Returns the full personal policy record.</summary>
    Task<GrainStates.PersonalPolicyState> GetAsync();

    /// <summary>
    /// Creates the personal policy record.
    /// Returns the policy ID (same as the grain key suffix).
    /// </summary>
    Task<string> CreateAsync(
        string patientId,
        string? groupPlanId,
        string groupPlanName,
        string subscriberId,
        string? subscriberName,
        string? relationshipToSubscriber,
        DateTime? effectiveDate,
        DateTime? expirationDate,
        string? coverageType,
        bool isPrimary,
        decimal? copayAmount,
        string? pharmacyMemberId,
        string? notes);

    /// <summary>Records that the annual deductible has been met for the current benefit year.</summary>
    Task MarkDeductibleMetAsync(DateTime metDate);

    /// <summary>Marks this policy as inactive (coverage ended).</summary>
    Task DeactivateAsync();

    /// <summary>Activates COBRA continuation coverage for this policy.</summary>
    Task SetCobraAsync(DateTime startDate, DateTime? endDate);
}
