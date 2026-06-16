// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Tracks the packaging + transmission lifecycle of one IHS NDW (National
/// Data Warehouse) export run. One activation per run; grain key
/// <c>"NDW-EXPORT:{runId}"</c>.
///
/// <para>
/// IHS NDW submission is a manual, multi-step affair (similar to GPRA
/// submission but with multiple files per run): the data-warehouse
/// coordinator packages the run into per-domain files, transmits them to
/// IHS NDW out-of-band, then records the IHS response.
/// </para>
///
/// <para>
/// Mutating methods are gated by <see cref="SecurityKeys.CanSubmitNdw"/>
/// and audited via <c>[AuditAction]</c>.
/// </para>
/// </summary>
public interface INdwExportRunGrain : IGrainWithStringKey
{
    /// <summary>Get the current run state.</summary>
    Task<NdwExportRunState> GetAsync();

    /// <summary>
    /// Build the export: select patient cohort via the registered
    /// <see cref="Reporting.INdwExportSourceProvider"/>, format via the
    /// registered <see cref="Reporting.INdwExportFormatter"/>, write per-
    /// domain files into <paramref name="outputDirectory"/>, and persist
    /// the run record. Re-running overwrites and increments
    /// <see cref="NdwExportRunState.PackagingAttempts"/>.
    /// </summary>
    [RequiresSecurityKey(SecurityKeys.CanSubmitNdw)]
    [AuditAction("NDW", "PACKAGE", EntityType = "NdwExportRun", IsClinicalWrite = false)]
    Task<NdwExportRunState> PackageAsync(
        string facilityId,
        DateTime periodStart,
        DateTime periodEnd,
        string outputDirectory,
        string packagedById,
        string packagedByName);

    /// <summary>Operator confirms transmission to IHS NDW.</summary>
    [RequiresSecurityKey(SecurityKeys.CanSubmitNdw)]
    [AuditAction("NDW", "RECORD_TRANSMISSION", EntityType = "NdwExportRun", IsClinicalWrite = false)]
    Task RecordTransmissionAsync(DateTime transmissionDate, string? trackingId);

    /// <summary>Operator records the IHS NDW response (acceptance or rejection).</summary>
    [RequiresSecurityKey(SecurityKeys.CanSubmitNdw)]
    [AuditAction("NDW", "RECORD_IHS_RESPONSE", EntityType = "NdwExportRun", IsClinicalWrite = false)]
    Task RecordIhsResponseAsync(DateTime responseDate, bool accepted, string? responseReceipt);
}
