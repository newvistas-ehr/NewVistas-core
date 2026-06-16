// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// LOINC Code Grain — represents a single entry in the Regenstrief Institute's
/// Logical Observation Identifiers Names and Codes (LOINC) system.
///
/// Grain Key: "LOINC:{code}" (e.g., "LOINC:2345-7", "LOINC:718-7").
///
/// LOINC codes are the standard for identifying laboratory tests, clinical
/// observations, and document types. In VistA, they map to:
///   - Lab test definitions (File #60 WKLD CODE field)
///   - Health Summary components
///   - TIU document type identification
///
/// Related VistA APIs:
///   LOINC^LA7QRY  — LOINC code lookup for lab results
/// </summary>
public interface ILoincCodeGrain : IGrainWithStringKey
{
    /// <summary>
    /// Gets the full LOINC code state.
    /// </summary>
    Task<GrainStates.LoincCodeState> GetCodeAsync();

    /// <summary>
    /// Loads/updates the LOINC code data.
    /// Called during bulk import from LOINC reference files.
    /// </summary>
    Task LoadCodeAsync(
        string code,
        string component,
        string property,
        string timeAspect,
        string system,
        string scale,
        string method,
        string shortName,
        string longCommonName,
        string status,
        string classType);

    /// <summary>
    /// Checks if this code is currently active.
    /// </summary>
    Task<bool> IsActiveAsync();

    /// <summary>
    /// Activates this code.
    /// </summary>
    Task ActivateAsync();

    /// <summary>
    /// Deactivates this code.
    /// </summary>
    Task DeactivateAsync();
}
