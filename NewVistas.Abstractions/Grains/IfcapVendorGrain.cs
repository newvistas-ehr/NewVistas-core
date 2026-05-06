// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class IfcapVendorGrain : Grain, IIfcapVendorGrain
{
    private readonly IPersistentState<IfcapVendorState> _state;

    public IfcapVendorGrain(
        [PersistentState("ifcapVendorState", "ifcapVendorStore")]
        IPersistentState<IfcapVendorState> state)
    {
        _state = state;
    }

    public Task<IfcapVendorState> GetAsync()
        => Task.FromResult(_state.State);

    public async Task CreateAsync(
        string name,
        string vendorNumber,
        string address,
        string city,
        string state,
        string zipCode,
        string? phone,
        string? fax,
        string? email,
        bool isSmallBusiness,
        bool isWomanOwned,
        bool isVeteranOwned,
        string? duns,
        string? contactName)
    {
        _state.State.VendorId         = this.GetPrimaryKeyString();
        _state.State.Name             = name;
        _state.State.VendorNumber     = vendorNumber;
        _state.State.Address          = address;
        _state.State.City             = city;
        _state.State.State            = state;
        _state.State.ZipCode          = zipCode;
        _state.State.Phone            = phone;
        _state.State.Fax              = fax;
        _state.State.Email            = email;
        _state.State.IsActive         = true;
        _state.State.IsSmallBusiness  = isSmallBusiness;
        _state.State.IsWomanOwned     = isWomanOwned;
        _state.State.IsVeteranOwned   = isVeteranOwned;
        _state.State.DUNS             = duns;
        _state.State.ContactName      = contactName;
        _state.State.CreatedDate      = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateAsync(
        string name,
        string address,
        string city,
        string state,
        string zipCode,
        string? phone,
        string? fax,
        string? email,
        string? contactName)
    {
        _state.State.Name             = name;
        _state.State.Address          = address;
        _state.State.City             = city;
        _state.State.State            = state;
        _state.State.ZipCode          = zipCode;
        _state.State.Phone            = phone;
        _state.State.Fax              = fax;
        _state.State.Email            = email;
        _state.State.ContactName      = contactName;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DeactivateAsync()
    {
        _state.State.IsActive         = false;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
