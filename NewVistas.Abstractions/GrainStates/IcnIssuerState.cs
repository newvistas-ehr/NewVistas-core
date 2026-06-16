// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for the singleton ICN issuer grain. Holds the next sequence number
/// the local cluster will allocate. Persisted before each issued ICN is
/// returned, so a silo crash never reissues the same number.
/// </summary>
[GenerateSerializer]
public class IcnIssuerState
{
    /// <summary>
    /// Next sequence number to allocate. Starts at 1; advances by 1 on each
    /// successful issuance.
    /// </summary>
    [Id(0)]
    public long NextSequence { get; set; } = 1;
}
