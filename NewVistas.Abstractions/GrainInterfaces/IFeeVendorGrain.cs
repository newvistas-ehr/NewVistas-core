// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Community care vendor (non-VA provider) grain (VistA File #162.5 FEE BASIS VENDOR).
/// Grain key: "FEE-VENDOR:{vendorId}".
/// Managed by FBVEND.m MUMPS routine.
/// </summary>
public interface IFeeVendorGrain : IGrainWithStringKey
{
    /// <summary>Returns the current vendor state.</summary>
    Task<FeeVendorState> GetAsync();

    /// <summary>Creates a new vendor record. Idempotent — should only be called once per grain.</summary>
    Task CreateAsync(
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
        string? notes);

    /// <summary>Updates an existing vendor's contact and contract details.</summary>
    Task UpdateAsync(
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
        string? notes);

    /// <summary>Activates or deactivates this vendor for new authorizations.</summary>
    Task SetActiveAsync(bool isActive);
}
