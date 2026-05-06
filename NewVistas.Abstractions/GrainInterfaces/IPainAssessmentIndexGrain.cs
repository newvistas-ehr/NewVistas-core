// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient pain assessment index.
/// Grain key: "NURS-PAIN-IDX:{patientId}"
/// </summary>
public interface IPainAssessmentIndexGrain : IGrainWithStringKey
{
    Task<List<PainAssessmentIndexEntry>> GetAllAsync();
    Task AddOrUpdateAsync(PainAssessmentIndexEntry entry);
    Task<PainAssessmentIndexEntry?> GetLatestAsync();
}
