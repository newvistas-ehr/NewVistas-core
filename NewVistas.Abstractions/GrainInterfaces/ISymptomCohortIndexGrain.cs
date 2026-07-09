// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Reverse-index shard for one coded symptom — the "who has this symptom" and "who was asked"
/// sets. Grain key: <c>SYMPTOM-COHORT:{code}</c>. Maintained by <see cref="IPatientSymptomGrain"/>;
/// read by the proto matcher/analytics for honest, assessed-denominator prevalence.
/// </summary>
public interface ISymptomCohortIndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Records the patient's latest presence for this symptom: always marks them assessed, and
    /// adds/removes them from the present set (idempotent).
    /// </summary>
    Task RecordPresenceAsync(string patientId, bool present);

    /// <summary>Marks the patient assessed for this symptom without changing the present set (idempotent).</summary>
    Task MarkAssessedAsync(string patientId);

    /// <summary>Patients whose latest answer is Present.</summary>
    Task<List<string>> GetPresentAsync();

    /// <summary>Patients ever assessed for this symptom.</summary>
    Task<List<string>> GetAssessedAsync();

    /// <summary>Number of patients with this symptom present.</summary>
    Task<int> GetPresentCountAsync();

    /// <summary>Number of patients assessed for this symptom (the honest denominator).</summary>
    Task<int> GetAssessedCountAsync();

    /// <summary>True if the patient's latest answer for this symptom is Present.</summary>
    Task<bool> ContainsPresentAsync(string patientId);
}
