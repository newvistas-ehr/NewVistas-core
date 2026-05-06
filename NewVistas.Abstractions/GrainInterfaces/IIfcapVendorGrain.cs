// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Manages an IFCAP procurement vendor record.
/// Grain key: "IFCAP-VENDOR:{vendorId}"
/// </summary>
public interface IIfcapVendorGrain : IGrainWithStringKey
{
    /// <summary>Returns the current vendor state.</summary>
    Task<IfcapVendorState> GetAsync();

    /// <summary>Creates the vendor record.</summary>
    Task CreateAsync(
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
        string? contactName);

    /// <summary>Updates vendor contact and address information.</summary>
    Task UpdateAsync(
        string name,
        string address,
        string city,
        string state,
        string zipCode,
        string? phone,
        string? fax,
        string? email,
        string? contactName);

    /// <summary>Marks the vendor as inactive.</summary>
    Task DeactivateAsync();
}
