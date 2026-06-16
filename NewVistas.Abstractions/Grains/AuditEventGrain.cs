// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Audit Event Grain — immutable record of a single auditable action.
/// Once written via RecordAsync, the state is never mutated.
/// Mirrors VistA AUDIT file (#1.1) record semantics from XUSEC LOG.
///
/// §170.315(d)(2) tamper-resistance: each event stores a SHA-256 hash of its
/// canonical content concatenated with the previous event's hash, forming an
/// append-only hash chain. Any retroactive modification breaks the chain.
/// </summary>
public class AuditEventGrain : Grain, IAuditEventGrain
{
    private readonly IPersistentState<AuditEventState> _state;

    public AuditEventGrain(
        [PersistentState("auditEventState", "auditEventStore")] IPersistentState<AuditEventState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.EventId))
        {
            _state.State.EventId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<AuditEventState> GetEventAsync() => Task.FromResult(_state.State);

    public async Task RecordAsync(
        string patientId,
        string domain,
        string action,
        string entityType,
        string entityId,
        string? userId,
        string? userName,
        string? locationId,
        string? locationName,
        string? details,
        string? oldValue,
        string? newValue,
        string previousEventHash)
    {
        // Immutability: only write if not already recorded
        if (!string.IsNullOrEmpty(_state.State.PatientId))
            return;

        _state.State.PatientId = patientId;
        _state.State.Domain = domain;
        _state.State.Action = action;
        _state.State.EntityType = entityType;
        _state.State.EntityId = entityId;
        _state.State.UserId = userId;
        _state.State.UserName = userName;
        _state.State.LocationId = locationId;
        _state.State.LocationName = locationName;
        _state.State.Details = details;
        _state.State.OldValue = oldValue;
        _state.State.NewValue = newValue;
        _state.State.Timestamp = DateTime.UtcNow;
        _state.State.PreviousEventHash = previousEventHash;

        // Compute tamper-evident hash: SHA-256(canonical_content + previousHash)
        _state.State.EventHash = ComputeEventHash(_state.State);

        await _state.WriteStateAsync();
    }

    public Task<bool> VerifyIntegrityAsync()
    {
        if (string.IsNullOrEmpty(_state.State.EventHash))
            return Task.FromResult(false);

        string recomputed = ComputeEventHash(_state.State);
        bool valid = string.Equals(recomputed, _state.State.EventHash, StringComparison.Ordinal);
        return Task.FromResult(valid);
    }

    /// <summary>
    /// Compute the SHA-256 hash of the event's canonical content.
    /// The canonical form is a pipe-delimited string of all immutable fields
    /// concatenated with the previous event hash, ensuring chain integrity.
    ///
    /// Implementation delegates to <see cref="HashChain.Compute"/>; the canonical
    /// byte format is preserved exactly so existing audit events still verify.
    /// </summary>
    internal static string ComputeEventHash(AuditEventState state)
    {
        string canonical = string.Join("|",
            state.EventId,
            state.PatientId,
            state.Domain,
            state.Action,
            state.EntityType,
            state.EntityId,
            state.UserId ?? string.Empty,
            state.UserName ?? string.Empty,
            state.LocationId ?? string.Empty,
            state.LocationName ?? string.Empty,
            state.Details ?? string.Empty,
            state.OldValue ?? string.Empty,
            state.NewValue ?? string.Empty,
            state.Timestamp.ToString("O"));

        return HashChain.Compute(canonical, state.PreviousEventHash);
    }
}
