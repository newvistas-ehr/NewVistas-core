// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Consult Service Directory — singleton index of available consult services.
/// Key: "CONSULT-SVC-DIR" (singleton)
/// Maps to VistA SERVICE/SECTION file (#49).
/// </summary>
public interface IConsultServiceDirectoryGrain : IGrainWithStringKey
{
    Task<List<GrainStates.ConsultServiceEntry>> SearchServicesAsync(string searchText, int maxResults);
    Task<GrainStates.ConsultServiceEntry?> GetServiceAsync(string serviceId);
    Task AddServiceAsync(GrainStates.ConsultServiceEntry entry);
    Task RemoveServiceAsync(string serviceId);
    Task<List<GrainStates.ConsultServiceEntry>> GetAllServicesAsync();
    Task SeedDemoDataAsync();
}
