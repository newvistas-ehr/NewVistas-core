// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton protocol index. Self-seeds with demo protocols.
/// Grain key: "RAD-PROTOCOL-INDEX"
/// </summary>
public interface IRadProtocolIndexGrain : IGrainWithStringKey
{
    Task<List<RadProtocolIndexEntry>> GetAllAsync();
    Task AddOrUpdateAsync(RadProtocolIndexEntry entry);
    Task<List<RadProtocolIndexEntry>> SearchAsync(string query);
    Task<List<RadProtocolIndexEntry>> GetByImagingTypeAsync(string imagingType);
}
