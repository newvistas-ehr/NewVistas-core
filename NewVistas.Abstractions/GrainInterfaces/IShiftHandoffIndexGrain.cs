// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient shift handoff index.
/// Grain key: "NURS-HANDOFF-IDX:{patientId}"
/// </summary>
public interface IShiftHandoffIndexGrain : IGrainWithStringKey
{
    Task<List<ShiftHandoffIndexEntry>> GetAllAsync();
    Task AddOrUpdateAsync(ShiftHandoffIndexEntry entry);
    Task<ShiftHandoffIndexEntry?> GetLatestAsync();
}
