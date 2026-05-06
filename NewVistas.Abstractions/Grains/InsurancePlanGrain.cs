// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class InsurancePlanGrain : Grain, IInsurancePlanGrain
{
    private readonly IPersistentState<InsurancePlanState> _state;

    public InsurancePlanGrain(
        [PersistentState("insurancePlanState", "insurancePlanStore")]
        IPersistentState<InsurancePlanState> state)
    {
        _state = state;
    }

    public Task<InsurancePlanState> GetAsync() => Task.FromResult(_state.State);

    public async Task<string> CreateAsync(
        string groupPlanName,
        string insuranceCompanyName,
        string? planType,
        string? groupNumber,
        string? coverageType,
        DateTime? effectiveDate,
        DateTime? expirationDate,
        decimal? coinsurancePercent,
        decimal? deductibleAmount,
        decimal? annualMaxBenefit,
        string? claimsAddress,
        string? claimsPhone,
        string? pharmacyBinNumber,
        string? pharmacyPcnNumber,
        int? filingTimeFrameDays,
        bool isPreCertRequired,
        bool allowsElectronicVerification,
        string? notes)
    {
        string planId = this.GetPrimaryKeyString().Replace("IB-PLAN:", string.Empty);

        _state.State.PlanId                   = planId;
        _state.State.GroupPlanName            = groupPlanName;
        _state.State.InsuranceCompanyName     = insuranceCompanyName;
        _state.State.PlanType                 = planType;
        _state.State.GroupNumber              = groupNumber;
        _state.State.CoverageType             = coverageType;
        _state.State.EffectiveDate            = effectiveDate;
        _state.State.ExpirationDate           = expirationDate;
        _state.State.CoinsurancePercent       = coinsurancePercent;
        _state.State.DeductibleAmount         = deductibleAmount;
        _state.State.AnnualMaxBenefit         = annualMaxBenefit;
        _state.State.ClaimsAddress            = claimsAddress;
        _state.State.ClaimsPhone              = claimsPhone;
        _state.State.PharmacyBinNumber        = pharmacyBinNumber;
        _state.State.PharmacyPcnNumber        = pharmacyPcnNumber;
        _state.State.FilingTimeFrameDays      = filingTimeFrameDays;
        _state.State.IsPreCertRequired        = isPreCertRequired;
        _state.State.AllowsElectronicVerification = allowsElectronicVerification;
        _state.State.Notes                    = notes;
        _state.State.IsActive                 = true;
        _state.State.CreatedDate              = DateTime.UtcNow;
        _state.State.LastModifiedDate         = DateTime.UtcNow;

        await _state.WriteStateAsync();
        return planId;
    }

    public async Task VerifyAsync(string verificationSource, DateTime verificationDateTime)
    {
        _state.State.VerificationSource   = verificationSource;
        _state.State.VerificationDateTime = verificationDateTime;
        _state.State.LastModifiedDate     = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DeactivateAsync()
    {
        _state.State.IsActive         = false;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
