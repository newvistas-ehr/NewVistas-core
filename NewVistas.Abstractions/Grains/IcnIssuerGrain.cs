// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.Federation;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Singleton ICN issuer for the local cluster. Allocates ICNs in the format
/// <c>{prefix}{sequence:D7}V{checksum:D6}</c>, with the prefix sourced from
/// <see cref="IClusterIdentity.IcnPrefix"/> at issuance time. Persists the
/// advanced sequence before returning each ICN, so a silo crash mid-issuance
/// cannot cause a duplicate allocation.
///
/// Grain key: the constant <c>"ICN-ISSUER"</c>. There is exactly one
/// activation per cluster.
/// </summary>
public class IcnIssuerGrain : Grain, IIcnIssuerGrain
{
    private readonly IPersistentState<IcnIssuerState> _state;
    private readonly IClusterIdentity _clusterIdentity;

    public IcnIssuerGrain(
        [PersistentState("icnIssuerState", "icnIssuerStore")]
        IPersistentState<IcnIssuerState> state,
        IClusterIdentity clusterIdentity)
    {
        _state = state;
        _clusterIdentity = clusterIdentity;
    }

    public async Task<string> IssueNextAsync()
    {
        long sequence = _state.State.NextSequence;
        if (sequence > 9_999_999)
            throw new InvalidOperationException(
                $"ICN sequence exhausted for cluster prefix {_clusterIdentity.IcnPrefix} " +
                $"(reached {sequence:D7}). A new prefix must be allocated.");

        // Persist the advance BEFORE returning the value, so a crash here
        // burns the sequence rather than risking duplicate issuance.
        _state.State.NextSequence = sequence + 1;
        await _state.WriteStateAsync();

        string prefixAndSeq = _clusterIdentity.IcnPrefix + sequence.ToString("D7");
        string checksum = IcnChecksumCalculator.Compute(prefixAndSeq);
        return $"{prefixAndSeq}V{checksum}";
    }

    public Task<long> PeekNextSequenceAsync() =>
        Task.FromResult(_state.State.NextSequence);
}
