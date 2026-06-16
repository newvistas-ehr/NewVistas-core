// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class IBSiteParametersGrain : Grain, IIBSiteParametersGrain
{
    private readonly IPersistentState<IBSiteParametersState> _state;

    public IBSiteParametersGrain(
        [PersistentState("ibSiteParamsState", "ibSiteParamsStore")]
        IPersistentState<IBSiteParametersState> state)
    {
        _state = state;
    }

    public Task<IBSiteParametersState> GetAsync() => Task.FromResult(_state.State);

    public async Task UpdateAsync(
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
        string updatedByUserId)
    {
        _state.State.SiteName                  = siteName;
        _state.State.BillingMailingAddress      = billingMailingAddress;
        _state.State.BillingEmail               = billingEmail;
        _state.State.OutpatientCopayThreshold   = outpatientCopayThreshold;
        _state.State.InpatientPerDiemRate       = inpatientPerDiemRate;
        _state.State.NhcuPerDiemRate            = nhcuPerDiemRate;
        _state.State.PharmacyCopayTier1Amount   = pharmacyCopayTier1Amount;
        _state.State.PharmacyCopayTier2Amount   = pharmacyCopayTier2Amount;
        _state.State.PharmacyCopayTier3Amount   = pharmacyCopayTier3Amount;
        _state.State.MedicareDeductibleAmount   = medicareDeductibleAmount;
        _state.State.CopayCapAmount             = copayCapAmount;
        _state.State.IsTricareSiteEnabled       = isTricareSiteEnabled;
        _state.State.LastUpdatedDate            = DateTime.UtcNow;
        _state.State.LastUpdatedByUserId        = updatedByUserId;
        await _state.WriteStateAsync();
    }
}
