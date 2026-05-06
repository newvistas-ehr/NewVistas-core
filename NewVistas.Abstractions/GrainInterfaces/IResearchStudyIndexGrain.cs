// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton index of all research studies at the facility.
/// Key pattern: "IRB-STUDY-IDX".
/// </summary>
public interface IResearchStudyIndexGrain : IGrainWithStringKey
{
    Task UpsertStudyAsync(IrbStudyIndexEntry entry);
    Task<List<IrbStudyIndexEntry>> GetAllStudiesAsync();
    Task<List<IrbStudyIndexEntry>> GetOpenStudiesAsync();
    Task<List<IrbStudyIndexEntry>> GetStudiesByTypeAsync(IrbStudyType studyType);
    Task<List<IrbStudyIndexEntry>> GetStudiesByPIAsync(string principalInvestigator);
    Task<List<IrbStudyIndexEntry>> GetStudiesExpiringAsync(int withinDays);
}
