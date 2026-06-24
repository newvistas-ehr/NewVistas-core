// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Per-patient projection of the VA drug classes the patient currently belongs to —
/// the union of every active medication's class set (primary + secondary). Keyed by
/// patient id.
///
/// This stored snapshot is what lets membership changes be applied as a diff against
/// the class→patient cohort shards: when it is recomputed, classes newly present add
/// the patient to their cohort and classes no longer present remove them. All codes
/// are stored upper-cased so set comparisons are case-insensitive without relying on
/// a serialized comparer.
/// </summary>
[GenerateSerializer]
public class PatientDrugClassIndexState
{
    /// <summary>Patient id (the grain key).</summary>
    [Id(0)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Upper-cased VA drug class codes the patient is currently in.</summary>
    [Id(1)]
    public HashSet<string> ActiveClassCodes { get; set; } = new();
}
