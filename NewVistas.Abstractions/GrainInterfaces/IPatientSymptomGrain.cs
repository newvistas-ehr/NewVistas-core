// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient coded symptom record. Grain key: <c>SYMPTOMS:{patientId}</c>.
///
/// Append-only history plus a latest-per-code projection; maintains the
/// <see cref="ISymptomCohortIndexGrain"/> reverse shards (Present + Assessed) on every write so
/// the analytics engine has honest denominators ("of the patients we actually asked about smell,
/// how many had it"). Reads are open; recording is gated (front-door clinicians + epi import).
/// </summary>
public interface IPatientSymptomGrain : IGrainWithStringKey
{
    /// <summary>
    /// Records a batch of coded symptom answers (each appended to history; the latest-per-code
    /// projection and the cohort shards are updated). Only observations with catalog codes are
    /// accepted. Returns the number of observations accepted.
    /// </summary>
    [RequiresSecurityKey(SecurityKeys.PROVIDER, SecurityKeys.ORES, SecurityKeys.ORELSE, SecurityKeys.EPI_MANAGER)]
    Task<int> RecordObservationsAsync(List<SymptomObservation> observations);

    /// <summary>Full state (history + projection).</summary>
    Task<PatientSymptomState> GetAsync();

    /// <summary>The current answer for every symptom the patient has ever been assessed for.</summary>
    Task<List<SymptomObservation>> GetLatestAsync();

    /// <summary>The current answer for one symptom code, or null if never assessed.</summary>
    Task<SymptomObservation?> GetLatestForCodeAsync(string code);
}
