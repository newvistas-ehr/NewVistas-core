// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.EventSourcing;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Per-patient append-only clinical event stream — the legal source of truth
/// for everything that has ever happened to a patient's clinical record.
///
/// Backed by Orleans <c>JournaledGrain</c> with a log-based consistency provider,
/// so the framework persists every appended envelope and rebuilds the projection
/// (<see cref="PatientStateSnapshot"/>) on activation. <c>RaiseEvent</c> +
/// <c>ConfirmEvents</c> provide the durable append; <c>RetrieveConfirmedEvents</c>
/// provides forensic reads.
///
/// Grain key: patient ICN/ID.
///
/// §170.315(d)(2) tamper-resistance: every envelope's <c>EventHash</c> chains
/// off the previous envelope's hash; <see cref="VerifyChainAsync"/> walks the
/// chain end-to-end.
/// </summary>
public interface IPatientClinicalEventStreamGrain : IGrainWithStringKey
{
    /// <summary>
    /// Append a clinical event envelope to this patient's chain.
    /// Idempotent on duplicate <see cref="EventEnvelope.EventId"/> — repeated
    /// calls with the same <c>EventId</c> are no-ops and return the existing
    /// version. The grain populates <see cref="EventEnvelope.PreviousEventHash"/>
    /// and <see cref="EventEnvelope.EventHash"/> at append time.
    /// </summary>
    /// <returns>The framework <c>Version</c> after this append (number of confirmed events).</returns>
    Task<int> AppendAsync(EventEnvelope envelope);

    /// <summary>
    /// Read a slice of confirmed envelopes from the chain, ordered by sequence.
    /// </summary>
    /// <param name="fromSequence">Zero-based sequence to start at (0 = first event).</param>
    /// <param name="max">Maximum number of envelopes to return.</param>
    Task<IReadOnlyList<EventEnvelope>> ReadAsync(int fromSequence, int max);

    /// <summary>
    /// Read confirmed envelopes filtered by domain and time range. Useful for
    /// forensic queries scoped to one clinical area.
    /// </summary>
    Task<IReadOnlyList<EventEnvelope>> ReadByDomainAsync(string domain, DateTime fromUtc, DateTime toUtc);

    /// <summary>
    /// Replay every confirmed envelope with <see cref="EventEnvelope.OccurredUtc"/>
    /// at or before <paramref name="asOfUtc"/> through a fresh
    /// <see cref="PatientStateSnapshot"/>, returning the reconstructed projection.
    /// Optionally filter by domain (e.g., "PROBLEMS") to scope the replay to
    /// one clinical area.
    /// </summary>
    Task<PatientStateSnapshot> ReplayUntilAsync(DateTime asOfUtc, string? domainFilter = null);

    /// <summary>
    /// Walk the entire chain, recompute each envelope's hash from its canonical
    /// fields, and confirm it matches the persisted <see cref="EventEnvelope.EventHash"/>.
    /// Returns false on the first mismatch (tampering detected).
    /// </summary>
    Task<bool> VerifyChainAsync();

    /// <summary>Current confirmed event count (<c>JournaledGrain.Version</c>).</summary>
    Task<int> GetVersionAsync();

    /// <summary>Hash of the most recently confirmed envelope (or genesis if none).</summary>
    Task<string> GetLastEventHashAsync();
}
