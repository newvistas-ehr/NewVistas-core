// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Events;

namespace NewVistas.Abstractions.Federation;

/// <summary>
/// Ships a batch of clinical event envelopes from the local cluster's outbox
/// to wherever the deployment's federation peer expects them — an upstream
/// HTTP endpoint, a Direct Project address, a file-bundle drop directory,
/// a Cosmos container that another cluster's change-feed reader subscribes to,
/// etc.
///
/// Batched (not per-envelope) so transports with per-call overhead — TLS
/// handshake, Direct Project S/MIME envelope, file-system fsync — can amortize.
///
/// Implementations should be synchronous-failure-tolerant: a network blip or
/// destination outage returns <see cref="TransportResult.Fail"/>, and the
/// drainer reschedules the batch with exponential backoff. Throwing is treated
/// the same way; either is fine.
/// </summary>
public interface IFederationTransport
{
    /// <summary>
    /// Attempt to ship this batch. Implementations should be idempotent on
    /// <see cref="EventEnvelope.EventId"/> — the receiver will see retries.
    /// </summary>
    Task<TransportResult> SendAsync(IReadOnlyList<EventEnvelope> batch, CancellationToken cancellationToken);
}
