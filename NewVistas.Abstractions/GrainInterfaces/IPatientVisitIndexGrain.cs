// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient index of PCE encounter visits (File #9000010).
/// Grain key: "PCE-VISITS:{patientId}"
/// </summary>
public interface IPatientVisitIndexGrain : IGrainWithStringKey
{
    Task<List<PceVisitEntry>> GetVisitsAsync(int maxResults);
    Task AddOrUpdateVisitAsync(PceVisitEntry entry);
    Task RemoveVisitAsync(string visitId);
}
