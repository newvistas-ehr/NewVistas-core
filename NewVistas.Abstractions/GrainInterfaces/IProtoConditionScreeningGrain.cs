// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient proto-condition screening worker. Grain key: <c>PROTO-SCREEN:{patientId}</c>.
/// Stateless compute: assembles the patient's feature snapshot from the read models and evaluates
/// it against a proto-condition definition. The read-only <see cref="EvaluateAsync"/> previews a
/// match (nothing recorded); <see cref="EvaluateAndRecordAsync"/> is the deliberate path that
/// applies the result to the proto's membership.
/// </summary>
public interface IProtoConditionScreeningGrain : IGrainWithStringKey
{
    /// <summary>Assembles the patient's current feature snapshot (problems, labs, vitals, symptoms, demographics, exposures).</summary>
    Task<PatientFeatureSnapshot> AssembleSnapshotAsync();

    /// <summary>Evaluates the patient against a proto-condition and returns the match WITHOUT recording it (preview).</summary>
    Task<ProtoMatchResult> EvaluateAsync(string protoConditionId);

    /// <summary>Evaluates the patient and applies the result to the proto's membership (deliberate; EPI-gated).</summary>
    [RequiresSecurityKey(SecurityKeys.EPI_MANAGER)]
    Task<ProtoMatchResult> EvaluateAndRecordAsync(string protoConditionId);
}
