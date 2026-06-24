// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Reverse-index shard for one VA drug class: the set of patients who currently have
/// an active medication in this class. Keyed by the (upper-cased) class code.
///
/// Maintained incrementally by <c>IPatientDrugClassIndexGrain</c> as prescriptions
/// change. This is the "patients on class X" lookup that powers safety-advisory
/// cohort resolution.
/// </summary>
[GenerateSerializer]
public class DrugClassCohortState
{
    /// <summary>The VA drug class code this shard tracks (upper-cased grain key).</summary>
    [Id(0)]
    public string ClassCode { get; set; } = string.Empty;

    /// <summary>Patient ids with an active medication in this class.</summary>
    [Id(1)]
    public HashSet<string> PatientIds { get; set; } = new();
}
