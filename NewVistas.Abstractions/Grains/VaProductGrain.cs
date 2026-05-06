// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// VA Product Grain implementation based on VistA NDF VA PRODUCT file (#50.68).
/// Stores the full reference data for one drug formulation in the national catalog.
/// </summary>
public class VaProductGrain : Grain, IVaProductGrain
{
    private readonly IPersistentState<VaProductState> _state;

    public VaProductGrain(
        [PersistentState("vaProductState", "vaProductStore")] IPersistentState<VaProductState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.Ien))
        {
            _state.State.Ien = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }

        return base.OnActivateAsync(cancellationToken);
    }

    public Task<VaProductState> GetProductAsync() =>
        Task.FromResult(_state.State);

    public async Task LoadProductAsync(
        string name,
        string vaGenericIen,
        string vaGenericName,
        string dosageFormIen,
        string dosageFormName,
        string strength,
        string strengthUnitIen,
        string strengthUnitName,
        string printName,
        string primaryDrugClassCode,
        string primaryDrugClassName,
        bool formularyIndicator,
        string? formularyRestrictions,
        string? controlledSubstanceSchedule,
        string? copayTier,
        decimal? maxSingleDose,
        decimal? minSingleDose,
        decimal? maxDailyDose,
        decimal? minDailyDose,
        decimal? maxCumulativeDose,
        string? doseUnit,
        bool isDosageCheckExcluded,
        bool isHazardousWaste,
        string? rxNormCode,
        string? vuid,
        bool isActive,
        DateTime? inactivationDate,
        List<DrugIngredient> ingredients,
        List<NdcEntry> ndcCodes,
        List<string> secondaryDrugClassCodes)
    {
        _state.State.Name = name;
        _state.State.VaGenericIen = vaGenericIen;
        _state.State.VaGenericName = vaGenericName;
        _state.State.DosageFormIen = dosageFormIen;
        _state.State.DosageFormName = dosageFormName;
        _state.State.Strength = strength;
        _state.State.StrengthUnitIen = strengthUnitIen;
        _state.State.StrengthUnitName = strengthUnitName;
        _state.State.PrintName = printName;
        _state.State.PrimaryDrugClassCode = primaryDrugClassCode;
        _state.State.PrimaryDrugClassName = primaryDrugClassName;
        _state.State.FormularyIndicator = formularyIndicator;
        _state.State.FormularyRestrictions = formularyRestrictions;
        _state.State.ControlledSubstanceSchedule = controlledSubstanceSchedule;
        _state.State.CopayTier = copayTier;
        _state.State.MaxSingleDose = maxSingleDose;
        _state.State.MinSingleDose = minSingleDose;
        _state.State.MaxDailyDose = maxDailyDose;
        _state.State.MinDailyDose = minDailyDose;
        _state.State.MaxCumulativeDose = maxCumulativeDose;
        _state.State.DoseUnit = doseUnit;
        _state.State.IsDosageCheckExcluded = isDosageCheckExcluded;
        _state.State.IsHazardousWaste = isHazardousWaste;
        _state.State.RxNormCode = rxNormCode;
        _state.State.Vuid = vuid;
        _state.State.IsActive = isActive;
        _state.State.InactivationDate = inactivationDate;
        _state.State.Ingredients = ingredients ?? new List<DrugIngredient>();
        _state.State.NdcCodes = ndcCodes ?? new List<NdcEntry>();
        _state.State.SecondaryDrugClassCodes = secondaryDrugClassCodes ?? new List<string>();
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public Task<bool> IsFormularyAsync() =>
        Task.FromResult(_state.State.FormularyIndicator);

    public Task<string?> GetFormularyRestrictionsAsync() =>
        Task.FromResult(_state.State.FormularyRestrictions);

    public Task<string> GetDosageStrengthSummaryAsync()
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(_state.State.DosageFormName))
            parts.Add(_state.State.DosageFormName);
        if (!string.IsNullOrEmpty(_state.State.Strength))
        {
            var strengthPart = _state.State.Strength;
            if (!string.IsNullOrEmpty(_state.State.StrengthUnitName))
                strengthPart += " " + _state.State.StrengthUnitName;
            parts.Add(strengthPart);
        }
        return Task.FromResult(string.Join(" ", parts));
    }

    public Task<string> GetCprsFormatAsync()
    {
        // Mirrors $$CPRS^PSNAPIS: "dosform^class^strength^units"
        var result = string.Join("^",
            _state.State.DosageFormName,
            _state.State.PrimaryDrugClassCode,
            _state.State.Strength,
            _state.State.StrengthUnitName);
        return Task.FromResult(result);
    }

    public Task<bool> IsActiveAsync() =>
        Task.FromResult(_state.State.IsActive);

    public async Task ActivateAsync()
    {
        _state.State.IsActive = true;
        _state.State.InactivationDate = null;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DeactivateAsync(DateTime? inactivationDate)
    {
        _state.State.IsActive = false;
        _state.State.InactivationDate = inactivationDate ?? DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddNdcEntryAsync(NdcEntry ndcEntry)
    {
        if (!_state.State.NdcCodes.Any(n => n.NdcCode == ndcEntry.NdcCode))
        {
            _state.State.NdcCodes.Add(ndcEntry);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task UpdateFormularyStatusAsync(bool inFormulary, string? restrictions, string? copayTier)
    {
        _state.State.FormularyIndicator = inFormulary;
        _state.State.FormularyRestrictions = restrictions;
        _state.State.CopayTier = copayTier;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
