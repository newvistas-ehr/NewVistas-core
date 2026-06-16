// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Instrument Index Grain — singleton catalog of all registered automated lab instruments.
///
/// Grain Key: "LA-INST-INDEX" (singleton)
///
/// Mirrors VistA's ^LAB(62.4,"B") cross-reference (lookup by name) and the
/// LA7UCFG.m configuration display. Self-seeds with demo instruments when empty.
/// </summary>
public interface IInstrumentIndexGrain : IGrainWithStringKey
{
    /// <summary>
    /// Returns all registered instruments.
    /// Self-seeds demo data if the index is empty.
    /// </summary>
    Task<List<GrainStates.InstrumentEntry>> GetAllInstrumentsAsync();

    /// <summary>
    /// Adds or updates an instrument entry in the index.
    /// Called when an instrument grain is configured.
    /// </summary>
    Task AddOrUpdateInstrumentAsync(GrainStates.InstrumentEntry entry);

    /// <summary>
    /// Removes an instrument from the index.
    /// </summary>
    Task RemoveInstrumentAsync(string instrumentId);

    /// <summary>
    /// Searches instruments by name, lab section, or manufacturer.
    /// </summary>
    Task<List<GrainStates.InstrumentEntry>> SearchInstrumentsAsync(string term);

    /// <summary>
    /// Returns only active instruments.
    /// Used by the TCP listener service on startup.
    /// </summary>
    Task<List<GrainStates.InstrumentEntry>> GetActiveInstrumentsAsync();

    /// <summary>
    /// Seeds demo instrument data. Called automatically on first access.
    /// </summary>
    Task SeedDemoInstrumentsAsync();
}
