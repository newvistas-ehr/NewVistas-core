// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient pregnancy index grain.
/// Key: "OB-PREG-IDX:{patientId}"
///
/// Maintains a lightweight list of pregnancy summaries so the UI can
/// display all pregnancies without activating every individual grain.
/// </summary>
public interface IPregnancyIndexGrain : IGrainWithStringKey
{
    /// <summary>Returns all pregnancy index entries (newest first).</summary>
    Task<List<GrainStates.PregnancyIndexEntry>> GetAllAsync();

    /// <summary>Returns pregnancy index entries filtered by status.</summary>
    Task<List<GrainStates.PregnancyIndexEntry>> GetByStatusAsync(GrainStates.PregnancyStatus status);

    /// <summary>Returns the active (ongoing) pregnancy, if any.</summary>
    Task<GrainStates.PregnancyIndexEntry?> GetActiveAsync();

    /// <summary>Adds a new pregnancy summary entry.</summary>
    Task AddEntryAsync(GrainStates.PregnancyIndexEntry entry);

    /// <summary>Updates an existing pregnancy entry's status and outcome.</summary>
    Task UpdateEntryAsync(string pregnancyId, GrainStates.PregnancyStatus status,
        GrainStates.PregnancyOutcome outcome, GrainStates.PregnancyRiskLevel riskLevel);
}
