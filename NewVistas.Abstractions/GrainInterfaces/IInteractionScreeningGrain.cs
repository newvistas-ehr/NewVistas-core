// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Grain representing a drug interaction screening for a single prescription.
///
/// VistA reference: DRGINT.m integration in PSO fill workflow (PSOORED.m).
/// Connects the existing IDrugInteractionCheckerGrain to the pharmacy
/// fill/refill/verify pipeline with blocking and pharmacist override.
///
/// Key format: IXSCREEN:{prescriptionId}
/// Store: interactionScreeningStore
///
/// Blocking policy: Significant and Contraindicated interactions block
/// fill/refill until a pharmacist overrides each one with a documented reason.
/// Minor and Moderate interactions are recorded as warnings but do not block.
/// </summary>
public interface IInteractionScreeningGrain : IGrainWithStringKey
{
    /// <summary>
    /// Performs the interaction screening by recording the findings.
    /// Called by the workflow grain after running the interaction checker.
    /// </summary>
    Task ScreenAsync(
        string prescriptionId,
        string patientId,
        string drugName,
        string? screenedBy,
        List<GrainStates.InteractionFinding> findings);

    /// <summary>Gets the full screening state.</summary>
    Task<GrainStates.InteractionScreeningState> GetAsync();

    /// <summary>
    /// Overrides a blocking interaction at the specified index.
    /// Only pharmacists may override. When all blocking interactions are
    /// overridden, status transitions to OverriddenByPharmacist.
    /// </summary>
    Task OverrideInteractionAsync(int findingIndex, string pharmacistId, string reason);

    /// <summary>Adds clinical notes to the screening.</summary>
    Task AddNotesAsync(string notes);
}
