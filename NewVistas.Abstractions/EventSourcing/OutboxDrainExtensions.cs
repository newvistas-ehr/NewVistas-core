// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.GrainInterfaces;

namespace NewVistas.Abstractions.EventSourcing;

/// <summary>
/// Extension methods that drain a domain grain's in-state outbox of pending
/// clinical event envelopes into the per-patient
/// <see cref="IPatientClinicalEventStreamGrain"/>.
///
/// At-least-once delivery: <see cref="IPatientClinicalEventStreamGrain.AppendAsync"/>
/// is idempotent on <see cref="EventEnvelope.EventId"/>, so a retry after a
/// crash is safe.
///
/// Call pattern:
///   - From a command method, after <c>WriteStateAsync</c>:
///     <c>await this.DrainOutboxAsync(_state, GrainFactory);</c>
///   - On grain activation (drains anything left over from a prior crash):
///     <c>await this.DrainOutboxAsync(_state, GrainFactory);</c>
///
/// The drain is awaited (not fire-and-forget) so its trailing
/// <c>WriteStateAsync</c> cannot race with the next command's write. Storage
/// failures inside the drain are caught and logged — the events are already
/// confirmed at the stream side (idempotent on EventId), so a transient
/// failure to clear the outbox is harmless and resolves on the next drain.
/// </summary>
public static class OutboxDrainExtensions
{
    /// <summary>
    /// Drain pending envelopes from the grain's state outbox to the patient
    /// clinical event stream. Stops on the first delivery failure and leaves
    /// the remaining envelopes in place for the next retry.
    /// </summary>
    public static async Task DrainOutboxAsync<TState>(
        this Grain grain,
        IPersistentState<TState> state,
        IGrainFactory grainFactory,
        ILogger? logger = null)
        where TState : EventSourcedStateBase
    {
        if (state.State.PendingEvents.Count == 0) return;

        // Snapshot so we don't iterate a list we mutate.
        List<EventEnvelope> pending = state.State.PendingEvents.ToList();
        var drainedIds = new HashSet<string>();

        foreach (EventEnvelope envelope in pending)
        {
            if (string.IsNullOrEmpty(envelope.PatientId))
            {
                // Malformed envelope — drop it rather than poison the outbox forever.
                drainedIds.Add(envelope.EventId);
                logger?.LogWarning(
                    "Dropping outbox envelope with no PatientId — EventId {EventId}",
                    envelope.EventId);
                continue;
            }

            try
            {
                IPatientClinicalEventStreamGrain stream =
                    grainFactory.GetGrain<IPatientClinicalEventStreamGrain>(envelope.PatientId);
                await stream.AppendAsync(envelope);
                drainedIds.Add(envelope.EventId);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex,
                    "Outbox drain failed for envelope {EventId}; will retry. {Remaining} envelope(s) remain pending.",
                    envelope.EventId, pending.Count - drainedIds.Count);
                break;
            }
        }

        if (drainedIds.Count > 0)
        {
            state.State.PendingEvents.RemoveAll(e => drainedIds.Contains(e.EventId));
            try
            {
                await state.WriteStateAsync();
            }
            catch (Exception ex)
            {
                // Storage write conflict (e.g. etag mismatch from a parallel
                // activation drain). The envelopes are already confirmed at
                // the stream and will be ignored on retry due to idempotency
                // on EventId. Log and move on.
                logger?.LogWarning(ex,
                    "Outbox drain succeeded but trailing WriteStateAsync failed; {Count} envelope(s) will be re-shipped harmlessly on next drain.",
                    drainedIds.Count);
            }
        }
    }
}
