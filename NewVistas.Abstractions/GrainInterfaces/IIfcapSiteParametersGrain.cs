// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
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
