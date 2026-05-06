// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class FeeVendorGrain : Grain, IFeeVendorGrain
{
    private readonly IPersistentState<FeeVendorState> _state;

    public FeeVendorGrain(
        [PersistentState("feeVendorState", "feeVendorStore")]
        IPersistentState<FeeVendorState> state)
    {
        _state = state;
    }

    public Task<FeeVendorState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task CreateAsync(
        string vendorName,
        string vendorType,
        string? specialtyCode,
        string? specialtyName,
        string? npi,
        string? taxId,
        string? address,
        string? phone,
        string? fax,
        string? contractNumber,
        DateTime? contractStartDate,
        DateTime? contractEndDate,
        string? notes)
    {
        _state.State.VendorId           = this.GetPrimaryKeyString();
        _state.State.VendorName         = vendorName;
        _state.State.VendorType         = Enum.TryParse<FeeVendorType>(vendorType, out FeeVendorType vt) ? vt : FeeVendorType.Individual;
        _state.State.SpecialtyCode      = specialtyCode;
        _state.State.SpecialtyName      = specialtyName;
        _state.State.NPI                = npi;
        _state.State.TaxId              = taxId;
        _state.State.Address            = address;
        _state.State.Phone              = phone;
        _state.State.Fax                = fax;
        _state.State.ContractNumber     = contractNumber;
        _state.State.ContractStartDate  = contractStartDate;
        _state.State.ContractEndDate    = contractEndDate;
        _state.State.Notes              = notes;
        _state.State.CreatedDate        = DateTime.UtcNow;
        _state.State.LastModifiedDate   = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateAsync(
        string vendorName,
        string vendorType,
        string? specialtyCode,
        string? specialtyName,
        string? npi,
        string? taxId,
        string? address,
        string? phone,
        string? fax,
        string? contractNumber,
        DateTime? contractStartDate,
        DateTime? contractEndDate,
        string? notes)
    {
        _state.State.VendorName         = vendorName;
        _state.State.VendorType         = Enum.TryParse<FeeVendorType>(vendorType, out FeeVendorType vt) ? vt : FeeVendorType.Individual;
        _state.State.SpecialtyCode      = specialtyCode;
        _state.State.SpecialtyName      = specialtyName;
        _state.State.NPI                = npi;
        _state.State.TaxId              = taxId;
        _state.State.Address            = address;
        _state.State.Phone              = phone;
        _state.State.Fax                = fax;
        _state.State.ContractNumber     = contractNumber;
        _state.State.ContractStartDate  = contractStartDate;
        _state.State.ContractEndDate    = contractEndDate;
        _state.State.Notes              = notes;
        _state.State.LastModifiedDate   = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetActiveAsync(bool isActive)
    {
        _state.State.IsActive         = isActive;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
