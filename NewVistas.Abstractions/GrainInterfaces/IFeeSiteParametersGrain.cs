// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton site-level configuration grain for the Fee Basis module.
/// Grain key: "FEE-SITE-PARAMS".
/// </summary>
public interface IFeeSiteParametersGrain : IGrainWithStringKey
{
    /// <summary>Returns the current fee site parameters.</summary>
    Task<FeeSiteParametersState> GetAsync();

    /// <summary>Updates site configuration for the Fee Basis program.</summary>
    Task UpdateAsync(
        string siteName,
        bool isFeeBasisEnabled,
        int fiscalYear,
        decimal? annualBudget,
        int maxAuthorizationDays,
        bool requiresPreAuthorization,
        decimal? autoApprovalLimit,
        string defaultPaymentMethod,
        string updatedByUserId);
}
