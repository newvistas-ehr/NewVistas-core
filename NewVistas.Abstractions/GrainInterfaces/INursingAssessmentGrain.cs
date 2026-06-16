// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Nursing Assessment Grain — VistA File #212.1 (NURSING ASSESSMENT).
/// Records a single head-to-toe nursing assessment.
/// Grain key: "NURS-ASSESS:{assessmentId}"
/// </summary>
public interface INursingAssessmentGrain : IGrainWithStringKey
{
    /// <summary>Returns the full assessment state.</summary>
    Task<NursingAssessmentState> GetAsync();

    /// <summary>Initialises the assessment. No-op if already created (idempotent).</summary>
    Task CreateAsync(NursingAssessmentState initialState);

    /// <summary>Signs the assessment, locking it from further edits.</summary>
    Task SignAsync(string signedById, string signedByName, DateTime signedDateTime);

    /// <summary>Amends a signed assessment, updating narrative notes and status.</summary>
    Task AmendAsync(string? narrativeNotes, string amendedById, string amendedByName);
}
