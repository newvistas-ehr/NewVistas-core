// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient index of nursing triage assessments.
/// Grain key: "NURS-TRIAGE-IDX:{patientId}"
/// </summary>
public interface INursingTriageIndexGrain : IGrainWithStringKey
{
    Task<List<NursingTriageIndexEntry>> GetAllAsync();
    Task AddOrUpdateAsync(NursingTriageIndexEntry entry);
}
