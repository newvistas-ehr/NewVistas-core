// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Directory of all proto-conditions (grain key <c>PROTOCONDITION-INDEX</c>). Holds projected
/// roll-up rows maintained by each proto grain; used for the surveillance list. Reads are open.
/// </summary>
public interface IProtoConditionIndexGrain : IGrainWithStringKey
{
    /// <summary>Adds or updates the roll-up row for a proto-condition.</summary>
    Task AddOrUpdateAsync(ProtoConditionSummary summary);

    /// <summary>Removes a proto-condition's row (retirement).</summary>
    Task RemoveAsync(string protoConditionId);

    /// <summary>All proto-conditions, newest activity first.</summary>
    Task<List<ProtoConditionSummary>> GetAllAsync();

    /// <summary>Only Active proto-conditions (the live surveillance set).</summary>
    Task<List<ProtoConditionSummary>> GetActiveAsync();
}

/// <summary>
/// Reverse-index shard for one proto-condition's CONFIRMED cohort (grain key
/// <c>PROTO-COHORT:{protoConditionId}</c>). Maintained by the proto grain on confirm/exclude.
/// </summary>
public interface IProtoCohortIndexGrain : IGrainWithStringKey
{
    Task AddAsync(string patientId);
    Task RemoveAsync(string patientId);
    Task<List<string>> GetPatientsAsync();
    Task<bool> ContainsAsync(string patientId);
    Task<int> GetCountAsync();
}
