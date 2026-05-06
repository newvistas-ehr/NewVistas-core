// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Site-level configuration singleton for the IFCAP module.
/// Grain key: "IFCAP-SITE-PARAMS"
/// </summary>
public interface IIfcapSiteParametersGrain : IGrainWithStringKey
{
    /// <summary>Returns the current site parameters.</summary>
    Task<IfcapSiteParametersState> GetAsync();

    /// <summary>Updates site-level IFCAP configuration.</summary>
    Task UpdateAsync(
        string siteName,
        string facilityNumber,
        int fiscalYear,
        int defaultDeliveryDays,
        bool isAutoApprovalEnabled,
        decimal autoApprovalThreshold,
        string poNumberPrefix,
        string updatedByUserId);
}
