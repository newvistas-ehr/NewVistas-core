// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Generates grounded, verifiable clinical summaries for a patient. Keyed by patient id.
///
/// Pipeline: assemble a grounded context from the patient's discrete grain data
/// (problems, meds, allergies, recent results) → compose a narrative via the swappable
/// <c>IClinicalNarrativeService</c> → verify every claim against the source facts →
/// persist a draft for clinician sign-off. The model narrates; the chart grounds; the
/// verifier checks; the clinician signs.
/// </summary>
public interface IPatientSummaryGrain : IGrainWithStringKey
{
    /// <summary>
    /// Generates a fresh summary draft for the given purpose (e.g., "pre-op",
    /// "consult handoff") and stores it pending sign-off.
    /// </summary>
    Task<ClinicalSummaryDraft> GenerateAsync(string purpose);

    /// <summary>Returns the current draft, or null if none has been generated.</summary>
    Task<ClinicalSummaryDraft?> GetCurrentDraftAsync();

    /// <summary>Clinician sign-off on the current draft.</summary>
    Task SignOffAsync(string clinicianId);
}
