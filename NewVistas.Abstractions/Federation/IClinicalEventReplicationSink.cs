// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Events;

namespace NewVistas.Abstractions.Federation;

/// <summary>
/// Receiver for clinical event envelopes that have been durably committed to a
/// patient's hash-chained log, used to forward those envelopes out of the
/// cluster (to a remote site, an outbox table, a change feed, a file bundle,
/// or nowhere at all).
///
/// The sink is invoked from <c>PatientClinicalEventStreamGrain.AppendAsync</c>
/// **after** <c>ConfirmEvents()</c> has succeeded on a fresh append. This means:
///
///   • The envelope is already legally durable when the sink is called — the
///     sink's job is replication, not persistence of record.
///   • Sink failures are caught and logged by the grain; they do <b>not</b>
///     fail the append. Replication is best-effort, layered on top of the
///     legal log.
///   • Duplicate-<c>EventId</c> appends (domain-grain outbox retries) skip
///     the sink. At-least-once recovery on transient sink failure is the
///     responsibility of higher-level retry/drainer infrastructure that lives
///     in the concrete sink implementation, not this seam.
///
/// Implementations should be idempotent on <see cref="EventEnvelope.EventId"/>
/// — the seam itself does not deliver duplicates today, but receiving clusters
/// will (re-shipped events from another site's outbox), and a single contract
/// is simpler than two.
/// </summary>
public interface IClinicalEventReplicationSink
{
    /// <summary>
    /// Forward a sealed envelope (hash chain populated) for replication.
    /// May throw — the caller catches and logs.
    /// </summary>
    Task PublishAsync(EventEnvelope envelope, CancellationToken cancellationToken);
}
