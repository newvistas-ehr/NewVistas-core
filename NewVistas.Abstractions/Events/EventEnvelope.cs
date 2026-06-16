// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.Events.Clinical;

namespace NewVistas.Abstractions.Events;

/// <summary>
/// Type-tagged wrapper used to ferry clinical events between the writing
/// domain grain (where they are produced) and the per-patient clinical event
/// stream grain (where they are persisted).
///
/// The envelope carries:
///  - Indexable metadata (PatientId, Domain, EventType, OccurredUtc, actor) so
///    consumers can filter without deserializing the payload.
///  - The strongly-typed payload (kept as <see cref="IClinicalEvent"/> so the
///    union of all event types serializes via Orleans polymorphic serialization).
///  - Hash-chain bookkeeping (<see cref="PreviousEventHash"/> / <see cref="EventHash"/>)
///    populated by the stream grain at append time per §170.315(d)(2).
///
/// Envelopes are <c>[GenerateSerializer]</c> immutable records.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record EventEnvelope
{
    [Id(0)] public string EventId { get; init; } = string.Empty;
    [Id(1)] public string PatientId { get; init; } = string.Empty;
    [Id(2)] public string Domain { get; init; } = string.Empty;
    [Id(3)] public string EventType { get; init; } = string.Empty;
    [Id(4)] public DateTime OccurredUtc { get; init; }
    [Id(5)] public string? UserId { get; init; }
    [Id(6)] public string? UserName { get; init; }

    /// <summary>
    /// The actual event payload. Polymorphic — concrete type is identified by
    /// <see cref="EventType"/>. Stored as the interface so unknown types from
    /// future code can still round-trip.
    /// </summary>
    [Id(7)] public IClinicalEvent? Payload { get; init; }

    /// <summary>
    /// Hash of the previous envelope in this patient's clinical-event chain.
    /// Set by the stream grain at append time. The first envelope uses
    /// <see cref="Security.HashChain.GenesisHash"/>.
    /// </summary>
    [Id(8)] public string PreviousEventHash { get; init; } = string.Empty;

    /// <summary>
    /// Hash of this envelope's canonical content concatenated with
    /// <see cref="PreviousEventHash"/>. Set by the stream grain at append time.
    /// Computed once and never recomputed; verification recomputes against the
    /// persisted canonical fields and must match.
    /// </summary>
    [Id(9)] public string EventHash { get; init; } = string.Empty;

    /// <summary>
    /// Identifier of the cluster (site/hospital) that originally produced this
    /// event. Stamped by <c>PatientClinicalEventStreamGrain.AppendAsync</c> on
    /// fresh writes from the silo's <c>IClusterIdentity</c>; preserved when an
    /// inbound replication applier appends an envelope received from another
    /// cluster. Included in <see cref="Canonicalize"/> so the hash chain
    /// protects source attribution from tampering.
    /// </summary>
    [Id(10)] public string SourceClusterId { get; init; } = string.Empty;

    /// <summary>
    /// Wrap a clinical event in an envelope. Hash-chain fields and source
    /// attribution are left empty — the stream grain populates them when the
    /// envelope is appended.
    /// </summary>
    public static EventEnvelope Wrap(IClinicalEvent evt) => new()
    {
        EventId = evt.EventId,
        PatientId = evt.PatientId,
        Domain = evt.Domain,
        EventType = evt.GetType().Name,
        OccurredUtc = evt.OccurredUtc,
        UserId = evt.UserId,
        UserName = evt.UserName,
        Payload = evt
    };

    /// <summary>
    /// Deterministic pipe-delimited representation of every immutable field —
    /// envelope-level metadata (including <see cref="SourceClusterId"/>)
    /// followed by the payload's canonical form. Used to feed the SHA-256 hash
    /// chain. Excludes <see cref="PreviousEventHash"/> and <see cref="EventHash"/>
    /// (those are the chain output, not input).
    /// <para>
    /// Prefixed with <c>"v1"</c> to position for a future versioned canonical
    /// format. If a subsequent field needs migration semantics, bump the prefix
    /// (and pair with a per-envelope version field) so old envelopes can still
    /// re-verify under their original canonicalization rules.
    /// </para>
    /// </summary>
    public string Canonicalize() => string.Join("|",
        "v1",
        EventId,
        PatientId,
        Domain,
        EventType,
        OccurredUtc.ToString("O"),
        UserId ?? string.Empty,
        UserName ?? string.Empty,
        SourceClusterId,
        Payload?.Canonicalize() ?? string.Empty);
}
