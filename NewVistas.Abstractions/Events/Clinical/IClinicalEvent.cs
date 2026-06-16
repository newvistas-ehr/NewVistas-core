// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.Events.Clinical;

/// <summary>
/// Marker interface for all clinical domain events.
///
/// Clinical events are causal: a state change does not occur unless its
/// corresponding event has been durably recorded. This is the legal source
/// of truth for patient clinical records, distinct from the observational
/// audit log (<see cref="GrainStates.AuditEventState"/>).
///
/// Every implementation must:
///  - Be decorated with <c>[GenerateSerializer]</c> and have <c>[Id(n)]</c> on every property.
///  - Be immutable once constructed (use <c>init</c>-only properties).
///  - Carry a unique <see cref="EventId"/> and the <see cref="PatientId"/> of the subject.
///  - Use a version suffix on the type name (V1, V2, ...) — never mutate an existing shape.
///  - Implement <see cref="Canonicalize"/> returning a deterministic pipe-delimited
///    representation of every immutable field used in the hash chain.
/// </summary>
public interface IClinicalEvent
{
    /// <summary>Unique identifier for this event (e.g., "CEV-{guid}"). Used for idempotency.</summary>
    string EventId { get; }

    /// <summary>Patient ICN/ID this event pertains to.</summary>
    string PatientId { get; }

    /// <summary>Clinical domain (e.g., "PROBLEMS", "ORDERS", "NOTES").</summary>
    string Domain { get; }

    /// <summary>UTC instant the event occurred (assigned by the originating command).</summary>
    DateTime OccurredUtc { get; }

    /// <summary>Optional user identifier of the actor who caused the event.</summary>
    string? UserId { get; }

    /// <summary>Optional display name of the actor.</summary>
    string? UserName { get; }

    /// <summary>
    /// Deterministic pipe-delimited representation of every immutable field on
    /// this event. Combined with the envelope-level canonical form to feed the
    /// SHA-256 hash chain. Implementations must be stable across builds — any
    /// change to the field set requires a new <c>Vn</c> event type.
    /// </summary>
    string Canonicalize();
}
