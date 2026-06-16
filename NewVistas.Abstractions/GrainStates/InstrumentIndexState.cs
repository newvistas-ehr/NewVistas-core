// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for the Instrument Index grain — singleton catalog of all registered
/// lab instruments. Mirrors VistA's ^LAB(62.4,"B") cross-reference.
/// </summary>
[GenerateSerializer]
public class InstrumentIndexState
{
    /// <summary>
    /// All registered instrument entries
    /// </summary>
    [Id(0)]
    public List<InstrumentEntry> Instruments { get; set; } = new();
}

/// <summary>
/// Lightweight summary entry for instrument index lookups.
/// </summary>
[GenerateSerializer]
public class InstrumentEntry
{
    /// <summary>
    /// Instrument grain key — "LA-INST:{id}"
    /// </summary>
    [Id(0)]
    public string InstrumentId { get; set; } = string.Empty;

    /// <summary>
    /// Instrument display name
    /// </summary>
    [Id(1)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Instrument type (Universal, POC, Shipping)
    /// </summary>
    [Id(2)]
    public InstrumentType InstrumentType { get; set; } = InstrumentType.UNIVERSAL;

    /// <summary>
    /// Current operational status
    /// </summary>
    [Id(3)]
    public InstrumentStatus Status { get; set; } = InstrumentStatus.INACTIVE;

    /// <summary>
    /// Lab section (e.g., "CHEMISTRY", "HEMATOLOGY")
    /// </summary>
    [Id(4)]
    public string? LabSection { get; set; }

    /// <summary>
    /// Manufacturer (e.g., "Beckman Coulter")
    /// </summary>
    [Id(5)]
    public string? Manufacturer { get; set; }

    /// <summary>
    /// Model name
    /// </summary>
    [Id(6)]
    public string? Model { get; set; }

    /// <summary>
    /// TCP port this instrument listens on
    /// </summary>
    [Id(7)]
    public int TcpPort { get; set; }
}
