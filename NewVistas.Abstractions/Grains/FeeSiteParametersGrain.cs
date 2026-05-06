// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class FeeSiteParametersGrain : Grain, IFeeSiteParametersGrain
{
    private readonly IPersistentState<FeeSiteParametersState> _state;

    public FeeSiteParametersGrain(
        [PersistentState("feeSiteParamsState", "feeSiteParamsStore")]
        IPersistentState<FeeSiteParametersState> state)
    {
        _state = state;
    }

    public Task<FeeSiteParametersState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task UpdateAsync(
        string siteName,
        bool isFeeBasisEnabled,
        int fiscalYear,
        decimal? annualBudget,
        int maxAuthorizationDays,
        bool requiresPreAuthorization,
        decimal? autoApprovalLimit,
        string defaultPaymentMethod,
        string updatedByUserId)
    {
        _state.State.SiteName                 = siteName;
        _state.State.IsFeeBasisEnabled        = isFeeBasisEnabled;
        _state.State.FiscalYear               = fiscalYear;
        _state.State.AnnualBudget             = annualBudget;
        _state.State.MaxAuthorizationDays     = maxAuthorizationDays;
        _state.State.RequiresPreAuthorization = requiresPreAuthorization;
        _state.State.AutoApprovalLimit        = autoApprovalLimit;
        _state.State.DefaultPaymentMethod     = defaultPaymentMethod;
        _state.State.LastUpdatedByUserId      = updatedByUserId;
        _state.State.LastUpdatedDate          = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
