// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Transfusion Grain — represents a single blood product transfusion administration.
///
/// Derived from VistA Blood Bank module (BBTRAN.m, BBTR1.m):
///   File #65.01  — TRANSFUSION (patient, unit, start/end, reaction)
///
/// Grain key: "BB-TX:{transfusionId}"
/// </summary>
public interface ITransfusionGrain : IGrainWithStringKey
{
    Task<TransfusionState> GetTransfusionAsync();

    /// <summary>Starts a transfusion — unit leaves inventory and enters the patient.</summary>
    Task StartAsync(
        string patientId,
        string unitId,
        string? crossmatchId,
        string productType,
        string aboType,
        string rhType,
        string administeredByUserId,
        string administeredByUserName,
        string orderedByUserId,
        string orderedByUserName,
        string? infusionSite,
        string? preTransfusionVitals);

    /// <summary>Records successful completion of the transfusion.</summary>
    Task CompleteAsync(
        DateTime endDateTime,
        decimal? volumeML,
        string? postTransfusionVitals);

    /// <summary>
    /// Stops a transfusion early — records the stop reason.
    /// If a transfusion reaction occurred, records the reaction type.
    /// </summary>
    Task StopAsync(
        DateTime endDateTime,
        string stopReason,
        TransfusionReactionType reactionType,
        string? reactionNotes);
}
