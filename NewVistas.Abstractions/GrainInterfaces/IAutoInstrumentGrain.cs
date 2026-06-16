// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Automated Lab Instrument Grain Interface based on VistA AUTO INSTRUMENT file (#62.4)
/// and LA7 MESSAGE PARAMETER file (#62.48).
///
/// Grain Key: "LA-INST:{instrumentId}"
///
/// Each grain represents one physical lab analyzer (e.g., Beckman AU5800, Sysmex XN-1000)
/// or point-of-care device (e.g., i-STAT). Stores instrument configuration, connection
/// parameters, and test-to-LOINC mappings.
///
/// Mirrors VistA routines: LA7UCFG.m (Universal Interface Config), LA7PCFG.m (POC Config),
/// and the ^LAB(62.4,...) global structure.
/// </summary>
public interface IAutoInstrumentGrain : IGrainWithStringKey
{
    /// <summary>
    /// Gets the complete instrument configuration state.
    /// </summary>
    Task<GrainStates.AutoInstrumentState> GetConfigAsync();

    /// <summary>
    /// Configures or updates the instrument definition.
    /// Mirrors VistA LA7UCFG EN — edit instrument configuration.
    /// </summary>
    Task UpdateConfigAsync(
        string name,
        GrainStates.InstrumentType instrumentType,
        string? hl7Version,
        string? tcpHost,
        int? tcpPort,
        int? connectionTimeoutSeconds,
        bool autoDownload,
        bool autoRelease,
        string? manufacturer,
        string? model,
        string? serialNumber,
        string? labSection,
        string? location,
        string? sendingApplication,
        string? sendingFacility);

    /// <summary>
    /// Activates the instrument for message processing.
    /// Mirrors enabling STATUS in VistA file #62.48.
    /// </summary>
    Task EnableAsync();

    /// <summary>
    /// Deactivates the instrument — stops message processing.
    /// </summary>
    Task DisableAsync();

    /// <summary>
    /// Sets the instrument to maintenance mode.
    /// </summary>
    Task SetMaintenanceModeAsync();

    /// <summary>
    /// Gets all test mappings for this instrument.
    /// Mirrors VistA AUTO INSTRUMENT subfile #62.41.
    /// </summary>
    Task<List<GrainStates.InstrumentTestMapping>> GetTestMappingsAsync();

    /// <summary>
    /// Adds or updates a test mapping (external code → internal LOINC).
    /// Mirrors adding orderable/reportable tests in VistA #62.41.
    /// </summary>
    Task AddTestMappingAsync(GrainStates.InstrumentTestMapping mapping);

    /// <summary>
    /// Removes a test mapping by external test code.
    /// </summary>
    Task RemoveTestMappingAsync(string externalTestCode);

    /// <summary>
    /// Looks up the internal LOINC mapping for an external instrument test code.
    /// Used by the message queue grain when routing instrument results.
    /// </summary>
    Task<GrainStates.InstrumentTestMapping?> ResolveTestMappingAsync(string externalTestCode);

    /// <summary>
    /// Records that a message was received from this instrument.
    /// Updates statistics (total processed, last received date).
    /// </summary>
    Task RecordMessageReceivedAsync();

    /// <summary>
    /// Records a processing error for this instrument.
    /// </summary>
    Task RecordErrorAsync();

    /// <summary>
    /// Records a successful connection event.
    /// </summary>
    Task RecordConnectionAsync();
}
