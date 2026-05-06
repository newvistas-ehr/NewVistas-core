// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class ARSiteParametersGrain : Grain, IARSiteParametersGrain
{
    private readonly IPersistentState<ARSiteParametersState> _state;

    public ARSiteParametersGrain(
        [PersistentState("arSiteParamsState", "arSiteParamsStore")]
        IPersistentState<ARSiteParametersState> state)
    {
        _state = state;
    }

    public Task<ARSiteParametersState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task UpdateAsync(
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
        string updatedByUserId)
    {
        if (string.IsNullOrEmpty(_state.State.SiteId))
            _state.State.SiteId = this.GetPrimaryKeyString();

        _state.State.SiteName                = siteName;
        _state.State.ARFacilityNumber        = arFacilityNumber;
        _state.State.InterestRate            = interestRate;
        _state.State.AdminCost               = adminCost;
        _state.State.PenaltyRate             = penaltyRate;
        _state.State.MinimumPaymentAmount    = minimumPaymentAmount;
        _state.State.MaxPaymentPlanMonths    = maxPaymentPlanMonths;
        _state.State.IsAutoInterestEnabled   = isAutoInterestEnabled;
        _state.State.IsPenaltyEnabled        = isPenaltyEnabled;
        _state.State.StatementFrequencyDays  = statementFrequencyDays;
        _state.State.CollectionThreshold     = collectionThreshold;
        _state.State.IsFmsEnabled            = isFmsEnabled;
        _state.State.IsTreasuryOffsetEnabled = isTreasuryOffsetEnabled;
        _state.State.LastUpdatedDate         = DateTime.UtcNow;
        _state.State.LastUpdatedByUserId     = updatedByUserId;
        await _state.WriteStateAsync();
    }
}
