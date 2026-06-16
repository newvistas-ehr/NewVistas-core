// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient fee basis summary grain tracking community care eligibility and
/// aggregate authorization / payment totals (VistA File #162 patient subfile).
/// Grain key: "FEE-PATIENT:{patientId}".
/// </summary>
public interface IFeePatientGrain : IGrainWithStringKey
{
    /// <summary>Returns the current fee patient state.</summary>
    Task<FeePatientState> GetAsync();

    /// <summary>
    /// Initializes the grain for the given patient if not already created.
    /// Idempotent — safe to call on every workflow interaction.
    /// </summary>
    Task EnsureInitializedAsync(string patientId);

    /// <summary>
    /// Recalculates aggregate totals from the authorization index.
    /// Called after each authorization is created or updated.
    /// </summary>
    Task UpdateSummaryAsync(decimal totalAuthorized, decimal totalPaid, int activeAuthorizationCount);

    /// <summary>Sets community care eligibility and optional effective date range.</summary>
    Task SetEligibilityAsync(bool isEligible, DateTime? startDate, DateTime? endDate);
}
