// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient TBI screening index grain — key: "TBI-SCREEN-IDX:{patientId}"
/// Maintains a list of all TBI screening summaries for a given patient.
/// </summary>
public interface ITBIScreeningIndexGrain : IGrainWithStringKey
{
    /// <summary>Returns all screenings for this patient, newest first.</summary>
    Task<List<TBIScreeningSummaryEntry>> GetAllScreeningsAsync();

    /// <summary>Returns only screenings with result PositiveRequiresEvaluation.</summary>
    Task<List<TBIScreeningSummaryEntry>> GetPositiveScreeningsAsync();

    Task UpsertScreeningAsync(TBIScreeningSummaryEntry entry);

    Task RemoveScreeningAsync(string screeningId);
}
