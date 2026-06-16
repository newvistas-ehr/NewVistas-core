// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Mental Health Instrument Library — singleton reference data for screening instruments.
/// Key: "MH-INSTRUMENTS" (singleton)
/// Provides instrument definitions, scoring rules, and question sets.
/// Maps to VistA YS MH INSTRUMENT (#601.71) instrument definitions.
/// </summary>
public interface IMentalHealthInstrumentLibraryGrain : IGrainWithStringKey
{
    /// <summary>
    /// Get all instrument definitions in the library.
    /// </summary>
    Task<List<GrainStates.MhInstrumentDefinition>> GetAllInstrumentsAsync();

    /// <summary>
    /// Get a specific instrument definition by name.
    /// </summary>
    Task<GrainStates.MhInstrumentDefinition?> GetInstrumentAsync(string instrumentName);

    /// <summary>
    /// Add or update an instrument definition in the library.
    /// </summary>
    Task AddInstrumentAsync(GrainStates.MhInstrumentDefinition instrument);

    /// <summary>
    /// Seed the library with standard mental health screening instruments.
    /// </summary>
    Task SeedDemoDataAsync();
}
