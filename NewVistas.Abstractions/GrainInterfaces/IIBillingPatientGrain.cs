// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Patient billing / copay account grain (VistA File #354 BILLING PATIENT, #354.7 IB PATIENT COPAY ACCOUNT).
/// Tracks each patient's copay balance, exemption status, copay cap, and hardship waivers.
///
/// MUMPS routines: IBCPACT.m (copay account), IBCPALR.m (copay alerts), IBCR*.m (exemptions).
/// Grain key: "IB-PATIENT:{patientId}"
/// </summary>
public interface IIBillingPatientGrain : IGrainWithStringKey
{
    /// <summary>Returns the full billing patient / copay account record.</summary>
    Task<GrainStates.IBillingPatientState> GetAsync();

    /// <summary>
    /// Initializes the copay account for a patient who doesn't yet have one.
    /// Safe to call repeatedly — idempotent if already initialized.
    /// </summary>
    Task EnsureInitializedAsync(string patientId);

    /// <summary>
    /// Sets the patient's copay exemption status.
    /// Passing isExempt=false clears the exemption fields.
    /// </summary>
    Task SetCopayExemptionAsync(
        bool isExempt,
        string? reasonCode,
        DateTime? effectiveDate,
        DateTime? expirationDate);

    /// <summary>Records that the patient's annual copay cap has been reached.</summary>
    Task MarkCopayCapReachedAsync(DateTime capReachedDate);

    /// <summary>Grants a financial hardship waiver, optionally with an expiration date.</summary>
    Task GrantHardshipAsync(DateTime grantedDate, DateTime? expirationDate);

    /// <summary>
    /// Posts a copay transaction to the patient's year-to-date balance.
    /// Increments CurrentYearCopayBalance and appends to YearToDateCopayTransactions.
    /// </summary>
    Task AddCopayTransactionAsync(
        string billingActionId,
        string actionTypeDescription,
        decimal amount,
        DateTime serviceDate,
        bool isExempt);
}
