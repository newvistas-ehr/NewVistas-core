// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Lab EDI Configuration Grain Interface — stores configuration for
/// a single external reference laboratory partner.
///
/// Grain Key: "LAB-EDI-CFG:{referenceLabId}"
///
/// Each reference lab (e.g., Quest Diagnostics, LabCorp, ARUP) has its own
/// configuration grain defining connection parameters, HL7 version,
/// test mappings, and automation settings.
/// </summary>
public interface ILabEdiConfigGrain : IGrainWithStringKey
{
    /// <summary>
    /// Gets the complete configuration state.
    /// </summary>
    Task<LabEdiConfigState> GetConfigAsync();

    /// <summary>
    /// Configures or updates the reference lab definition.
    /// </summary>
    Task ConfigureAsync(
        string labName,
        string cliaNumber,
        string connectionType,
        string? host,
        int? port,
        string? ftpPath,
        string? hl7Version,
        string sendingFacilityId,
        string sendingFacilityName,
        bool autoAcknowledge,
        bool autoFileResults);

    /// <summary>
    /// Activates the reference lab for EDI order processing.
    /// </summary>
    Task ActivateAsync();

    /// <summary>
    /// Deactivates the reference lab — stops order processing.
    /// </summary>
    Task DeactivateAsync();

    /// <summary>
    /// Returns whether the reference lab is currently active.
    /// </summary>
    Task<bool> IsActiveAsync();

    /// <summary>
    /// Adds or updates a test mapping between local and reference lab test codes.
    /// </summary>
    Task UpdateTestMappingAsync(
        string localTestCode,
        string referenceLabTestCode,
        string? loincCode);
}
