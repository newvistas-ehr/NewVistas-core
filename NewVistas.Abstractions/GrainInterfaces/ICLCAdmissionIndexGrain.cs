// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Facility-wide CLC census and admission index.
/// Singleton key: "CLC-ADMIT-IDX".
/// </summary>
public interface ICLCAdmissionIndexGrain : IGrainWithStringKey
{
    Task UpsertAdmissionAsync(CLCAdmissionIndexEntry entry);
    Task<List<CLCAdmissionIndexEntry>> GetAllAdmissionsAsync();
    Task<List<CLCAdmissionIndexEntry>> GetActiveCensusAsync();
    Task<List<CLCAdmissionIndexEntry>> GetAdmissionsByLevelOfCareAsync(GECLevelOfCare levelOfCare);
    Task<List<CLCAdmissionIndexEntry>> GetAdmissionsByWardAsync(string ward);
    Task<List<CLCAdmissionIndexEntry>> GetAnticipatedDischargesAsync(int withinDays);
}
