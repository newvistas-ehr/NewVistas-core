// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Grain representing a single infection outbreak cluster.
/// Key pattern: "HAI-OUTBREAK:{guid}"
/// </summary>
public interface IOutbreakGrain : IGrainWithStringKey
{
    Task<OutbreakState> GetOutbreakAsync();

    Task CreateOutbreakAsync(
        string outbreakId,
        string name,
        string description,
        HAIType haiType,
        DateTime? startDate,
        string locationId,
        string locationName,
        string pathogen);

    Task UpdateStatusAsync(OutbreakStatus status, DateTime? controlDate, DateTime? closeDate);

    Task AddCaseAsync(string caseId);

    Task RemoveCaseAsync(string caseId);

    Task NotifyPublicHealthAsync(DateTime notificationDate);
}
