// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Events;

namespace NewVistas.Abstractions.Federation;

/// <summary>
/// Receiving-side counterpart to the outbound replication sink. Consumes a
/// batch of envelopes shipped by another cluster's transport and applies each
/// one through the local <c>IPatientClinicalEventStreamGrain.AppendAsync</c>
/// — the same hash-chained, idempotent-by-EventId append the local domain
/// grains use.
///
/// <para>
/// The implementation lives in <c>FederationInboundApplier</c> in this
/// assembly so both <c>NewVistas.SiloHost</c> (silo-side use, e.g. a future
/// file-watcher service) and <c>NewVistas.WebServer</c> (the HTTP receive
/// surface) can register and resolve it from their respective DI containers
/// without an additional shared package.
/// </para>
///
/// <para>
/// Authentication is the transport's concern. By the time
/// <see cref="ApplyBatchAsync"/> is called, <paramref name="fromClusterId"/>
/// is the *authenticated* identity of the sender; envelope-level
/// <see cref="EventEnvelope.SourceClusterId"/> is the *origin* of each event
/// (which may differ in a hub-and-spoke flow).
/// </para>
/// </summary>
public interface IFederationInboundApplier
{
    /// <param name="envelopes">Envelopes to apply, in their wire order.</param>
    /// <param name="fromClusterId">
    /// Authenticated cluster id of the sender. Used to stamp
    /// <see cref="EventEnvelope.SourceClusterId"/> on any envelope arriving
    /// without one. Must be non-empty; an empty value is itself a transport
    /// authentication bug and should be rejected upstream.
    /// </param>
    Task<InboundApplyResult> ApplyBatchAsync(
        IReadOnlyList<EventEnvelope> envelopes,
        string fromClusterId,
        CancellationToken cancellationToken);
}
