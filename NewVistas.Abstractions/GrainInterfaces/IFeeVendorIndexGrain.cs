// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton index grain for all fee basis vendors.
/// Grain key: "FEE-VENDOR-IDX".
/// </summary>
public interface IFeeVendorIndexGrain : IGrainWithStringKey
{
    /// <summary>Adds a new vendor entry or updates an existing one (matched by VendorId).</summary>
    Task AddOrUpdateAsync(FeeVendorIndexEntry entry);

    /// <summary>Returns all vendor entries regardless of active status.</summary>
    Task<List<FeeVendorIndexEntry>> GetAllAsync();

    /// <summary>Returns only active vendor entries eligible for new authorizations.</summary>
    Task<List<FeeVendorIndexEntry>> GetActiveAsync();
}
