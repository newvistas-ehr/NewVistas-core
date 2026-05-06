// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient EPCS e-prescription index grain.
/// Key: "EPCS-RX-IDX:{patientId}"
/// </summary>
public interface IEpcsPrescriptionIndexGrain : IGrainWithStringKey
{
    Task<List<GrainStates.EpcsPrescriptionIndexEntry>> GetAllAsync();
    Task<List<GrainStates.EpcsPrescriptionIndexEntry>> GetByStatusAsync(GrainStates.EpcsTransmissionStatus status);
    Task AddEntryAsync(GrainStates.EpcsPrescriptionIndexEntry entry);
    Task UpdateEntryAsync(string epcsId, GrainStates.EpcsTransmissionStatus status, bool isSigned);
}
