// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Singleton site-wide index grain for all HAI cases.
/// Key: "HAI-CASE-IDX"
/// </summary>
public interface IHAICaseIndexGrain : IGrainWithStringKey
{
    Task<List<HAICaseSummary>> GetAllCasesAsync();

    Task<List<HAICaseSummary>> GetActiveAsync();

    Task<List<HAICaseSummary>> GetByTypeAsync(HAIType haiType);

    Task<List<HAICaseSummary>> GetByLocationAsync(string locationId);

    Task<List<HAICaseSummary>> GetByOutbreakAsync(string outbreakId);

    Task UpsertCaseAsync(HAICaseSummary summary);

    Task RemoveCaseAsync(string caseId);
}
