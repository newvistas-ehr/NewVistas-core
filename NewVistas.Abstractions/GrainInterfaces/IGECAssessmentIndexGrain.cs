// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient GEC assessment history index.
/// Key pattern: "GEC-ASSESS-IDX:{patientId}".
/// </summary>
public interface IGECAssessmentIndexGrain : IGrainWithStringKey
{
    Task UpsertAssessmentAsync(GECAssessmentIndexEntry entry);
    Task<List<GECAssessmentIndexEntry>> GetAllAssessmentsAsync();
    Task<List<GECAssessmentIndexEntry>> GetAssessmentsByTypeAsync(GECAssessmentType type);
    Task<GECAssessmentIndexEntry?> GetLatestAssessmentAsync();
}
