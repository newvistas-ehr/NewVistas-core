// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Per-patient index of all registered tumors.
/// Grain key: "ONC-TUMOR-IDX:{patientId}"
/// </summary>
[GenerateSerializer]
public class OncologyTumorIndexState
{
    /// <summary>Patient identifier this index belongs to.</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>All registered tumor summary entries for this patient.</summary>
    [Id(1)] public List<OncologyTumorIndexEntry> Tumors { get; set; } = new();

    /// <summary>Last modification timestamp.</summary>
    [Id(2)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
