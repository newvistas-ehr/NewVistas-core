// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Extracts discrete, source-anchored findings from one radiology report and runs the
/// acknowledgment gate over the material ones. Keyed by report id.
///
/// Pipeline: extract (model surfaces only what the radiologist wrote, each with its
/// verbatim source sentence) → verify each quote against the report → flag material
/// findings (≥ Moderate) as requiring a decision → a clinician acknowledges or
/// rejects-with-reason. A rejection is recorded and patient-visible — the forcing function.
/// </summary>
public interface IRadiologyFindingExtractionGrain : IGrainWithStringKey
{
    /// <summary>Extracts findings from the report, verifies them, and stores the result.</summary>
    Task<RadiologyExtractionState> ExtractAsync(string reportText, string patientId, string extractedBy);

    /// <summary>Returns the stored extraction (findings + acknowledgment state).</summary>
    Task<RadiologyExtractionState> GetAsync();

    /// <summary>Clinician acknowledges a finding (confirms it is noted).</summary>
    Task AcknowledgeAsync(string findingId, string clinicianId);

    /// <summary>
    /// Clinician rejects a finding. A non-empty <paramref name="reason"/> is required; the
    /// rejection and its reason are recorded and made patient-visible.
    /// </summary>
    Task RejectAsync(string findingId, string clinicianId, string reason);
}
