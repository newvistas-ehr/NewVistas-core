// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// IB Site Parameters grain — singleton facility billing configuration (VistA File #350.9).
/// Holds copay rate schedules, per diem rates, Medicare deductible amounts, and
/// site-wide billing settings. There is exactly one record per facility.
///
/// MUMPS routines: IBSITE.m (site parameter lookups), IBT*.m (table management).
/// Grain key: "IB-SITE-PARAMS"
/// </summary>
public interface IIBSiteParametersGrain : IGrainWithStringKey
{
    /// <summary>Returns the full site parameters record.</summary>
    Task<GrainStates.IBSiteParametersState> GetAsync();

    /// <summary>
    /// Updates site billing configuration. All copay rate parameters are required;
    /// optional parameters may be null to clear them.
    /// </summary>
    Task UpdateAsync(
        string siteName,
        string billingMailingAddress,
        string? billingEmail,
        decimal? outpatientCopayThreshold,
        decimal? inpatientPerDiemRate,
        decimal? nhcuPerDiemRate,
        decimal pharmacyCopayTier1Amount,
        decimal pharmacyCopayTier2Amount,
        decimal pharmacyCopayTier3Amount,
        decimal? medicareDeductibleAmount,
        decimal? copayCapAmount,
        bool isTricareSiteEnabled,
        string updatedByUserId);
}
