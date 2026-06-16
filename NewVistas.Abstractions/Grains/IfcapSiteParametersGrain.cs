// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class IfcapSiteParametersGrain : Grain, IIfcapSiteParametersGrain
{
    private readonly IPersistentState<IfcapSiteParametersState> _state;

    public IfcapSiteParametersGrain(
        [PersistentState("ifcapSiteParametersState", "ifcapSiteParamsStore")]
        IPersistentState<IfcapSiteParametersState> state)
    {
        _state = state;
    }

    public Task<IfcapSiteParametersState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task UpdateAsync(
        string siteName,
        string facilityNumber,
        int fiscalYear,
        int defaultDeliveryDays,
        bool isAutoApprovalEnabled,
        decimal autoApprovalThreshold,
        string poNumberPrefix,
        string updatedByUserId)
    {
        _state.State.SiteId                  = this.GetPrimaryKeyString();
        _state.State.SiteName                = siteName;
        _state.State.FacilityNumber          = facilityNumber;
        _state.State.FiscalYear              = fiscalYear;
        _state.State.DefaultDeliveryDays     = defaultDeliveryDays;
        _state.State.IsAutoApprovalEnabled   = isAutoApprovalEnabled;
        _state.State.AutoApprovalThreshold   = autoApprovalThreshold;
        _state.State.PONumberPrefix          = poNumberPrefix;
        _state.State.IsActive                = true;
        _state.State.UpdatedByUserId         = updatedByUserId;
        _state.State.LastUpdatedDate         = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
