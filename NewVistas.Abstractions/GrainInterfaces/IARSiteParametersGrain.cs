// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton grain holding facility-wide Accounts Receivable configuration
/// (VistA File #342 AR SITE PARAMETER).
/// Grain key: "AR-SITE-PARAMS"
/// </summary>
public interface IARSiteParametersGrain : IGrainWithStringKey
{
    /// <summary>Returns the current AR site parameters.</summary>
    Task<ARSiteParametersState> GetAsync();

    /// <summary>
    /// Updates all configurable AR site parameters.
    /// </summary>
    Task UpdateAsync(
        string siteName,
        string arFacilityNumber,
        decimal interestRate,
        decimal adminCost,
        decimal penaltyRate,
        decimal minimumPaymentAmount,
        int maxPaymentPlanMonths,
        bool isAutoInterestEnabled,
        bool isPenaltyEnabled,
        int statementFrequencyDays,
        decimal collectionThreshold,
        bool isFmsEnabled,
        bool isTreasuryOffsetEnabled,
        string updatedByUserId);
}
