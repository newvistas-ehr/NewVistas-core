// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
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
