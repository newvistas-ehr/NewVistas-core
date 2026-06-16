// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Means Test Billing Clock grain (VistA File #351 MEANS TEST BILLING CLOCK).
/// Tracks the eligibility clock that controls when copay charges are generated.
/// When a patient's means test results in copay obligation, this clock is set;
/// all billable visits and prescriptions during the active period generate charges.
///
/// MUMPS routines: IBCPBIL.m (billing clock), IBCP*.m (copay processing).
/// Grain key: "IB-CLOCK:{patientId}"
/// </summary>
public interface IMeansTestBillingClockGrain : IGrainWithStringKey
{
    /// <summary>Returns the full billing clock record.</summary>
    Task<GrainStates.MeansTestBillingClockState> GetAsync();

    /// <summary>
    /// Sets or updates the billing clock period.
    /// Typically called when a means test is completed or eligibility changes.
    /// </summary>
    Task SetClockAsync(
        string clockStatus,
        DateTime? startDate,
        DateTime? expirationDate,
        string? meansTestId,
        string? billingCategory,
        string? priorityGroup);

    /// <summary>
    /// Resets the clock (e.g., when a new means test supersedes the previous one).
    /// Records the reset reason and updates LastResetDate.
    /// </summary>
    Task ResetClockAsync(string resetReason);

    /// <summary>
    /// Suspends the clock without expiring it (e.g., pending appeal or data correction).
    /// Sets ClockStatus to SUSPENDED and records the reason.
    /// </summary>
    Task SuspendClockAsync(string reason);
}
