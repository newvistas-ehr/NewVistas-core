// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Per-institution transfer-center queue. One entry per request touching this
/// institution (as sender or receiver), upserted by the workflow on every transition.
/// </summary>
public class TransferCenterGrain : Grain, ITransferCenterGrain
{
    private static readonly string[] ActiveStatuses =
        { TransferRequestStatus.Requested, TransferRequestStatus.Accepted };

    private readonly IPersistentState<TransferCenterState> _state;

    public TransferCenterGrain(
        [PersistentState("transferCenter", "transferCenterStore")]
        IPersistentState<TransferCenterState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.InstitutionId))
        {
            string rawKey = this.GetPrimaryKeyString();
            _state.State.InstitutionId = rawKey.StartsWith("TRANSFER-CENTER:")
                ? rawKey["TRANSFER-CENTER:".Length..]
                : rawKey;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task AddOrUpdateAsync(TransferRequestEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.TransferId))
            return;

        _state.State.Requests[entry.TransferId] = entry;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<List<TransferRequestEntry>> GetIncomingAsync(bool activeOnly = true)
        => Task.FromResult(Filter(e => e.ReceivingInstitutionId == _state.State.InstitutionId, activeOnly));

    public Task<List<TransferRequestEntry>> GetOutgoingAsync(bool activeOnly = true)
        => Task.FromResult(Filter(e => e.SendingInstitutionId == _state.State.InstitutionId, activeOnly));

    public Task<int> GetPendingIncomingCountAsync()
        => Task.FromResult(_state.State.Requests.Values.Count(e =>
            e.ReceivingInstitutionId == _state.State.InstitutionId
            && e.Status == TransferRequestStatus.Requested));

    private List<TransferRequestEntry> Filter(Func<TransferRequestEntry, bool> side, bool activeOnly)
        => _state.State.Requests.Values
            .Where(side)
            .Where(e => !activeOnly || ActiveStatuses.Contains(e.Status))
            .OrderByDescending(e => e.Urgency == "EMERGENT")
            .ThenByDescending(e => e.Urgency == "URGENT")
            .ThenBy(e => e.RequestDateTime ?? DateTime.MaxValue)
            .ToList();
}
