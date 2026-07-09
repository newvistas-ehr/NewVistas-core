// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Reverse-index shard for one coded symptom. Keyed by <c>SYMPTOM-COHORT:{code}</c>.
///
/// Holds TWO sets so prevalence denominators are honest: <see cref="Assessed"/> is everyone the
/// symptom was ever asked about (Present, Absent, or explicitly Unknown), and <see cref="Present"/>
/// is the subset who had it. Rate = |Present| / |Assessed| — "of the patients we actually asked
/// about smell, how many had it" — NOT |Present| / total-population (which would silently punish
/// low survey compliance). <see cref="Present"/> is always a subset of <see cref="Assessed"/>.
/// </summary>
[GenerateSerializer]
public class SymptomCohortState
{
    /// <summary>The symptom (SNOMED) code this shard tracks.</summary>
    [Id(0)] public string Code { get; set; } = string.Empty;

    /// <summary>Patients whose latest answer for this symptom is Present.</summary>
    [Id(1)] public HashSet<string> Present { get; set; } = new();

    /// <summary>Patients ever assessed for this symptom (Present ∪ Absent ∪ Unknown-answered).</summary>
    [Id(2)] public HashSet<string> Assessed { get; set; } = new();
}
