// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Tracks the packaging + transmission lifecycle of a single GPRA submission.
/// One activation per source GPRA report; grain key <c>"GPRA-SUB:{reportId}"</c>.
///
/// <para>
/// IHS national-office submission is a manual, multi-step affair: the
/// quality coordinator packages a completed report into the IHS-required
/// file format, uploads it via the IHS portal (out of band), then records
/// IHS's acceptance/rejection back into the system. This grain holds the
/// state for each step so the audit trail and re-packaging logic are
/// centralized.
/// </para>
///
/// All mutating methods are gated by
/// <see cref="SecurityKeys.CanSubmitGpra"/>; reads are open. The format is
/// pluggable via the registered <see cref="Reporting.IGpraSubmissionFormatter"/>.
/// </summary>
public interface IGpraSubmissionGrain : IGrainWithStringKey
{
    /// <summary>Get the current submission state. Null FilePath means packaging has not yet run.</summary>
    Task<GpraSubmissionState> GetAsync();

    /// <summary>
    /// Read the source GPRA report, format it via the registered
    /// <see cref="Reporting.IGpraSubmissionFormatter"/>, write the file to
    /// <paramref name="outputDirectory"/>, and persist the submission record.
    /// Re-packaging an already-packaged submission overwrites the file and
    /// increments <see cref="GpraSubmissionState.PackagingAttempts"/>; status
    /// resets to <see cref="GpraSubmissionStatus.Packaged"/>.
    /// </summary>
    /// <param name="reportId">The source <c>IGpraReportGrain</c> report id (must be in Completed status).</param>
    /// <param name="outputDirectory">Absolute path to a directory the silo can write to.</param>
    /// <param name="packagedById">Operator user id triggering the packaging.</param>
    /// <param name="packagedByName">Operator display name.</param>
    [RequiresSecurityKey(SecurityKeys.CanSubmitGpra)]
    [AuditAction("GPRA", "PACKAGE_SUBMISSION", EntityType = "GpraReport", IsClinicalWrite = false)]
    Task<GpraSubmissionState> PackageAsync(
        string reportId,
        string outputDirectory,
        string packagedById,
        string packagedByName);

    /// <summary>
    /// Operator confirms the file was transmitted to IHS (uploaded to the
    /// portal, FTP'd, etc.). Transitions status to
    /// <see cref="GpraSubmissionStatus.Submitted"/>. Optional
    /// <paramref name="trackingId"/> is the receipt number from the IHS portal.
    /// </summary>
    [RequiresSecurityKey(SecurityKeys.CanSubmitGpra)]
    [AuditAction("GPRA", "RECORD_TRANSMISSION", EntityType = "GpraReport", IsClinicalWrite = false)]
    Task RecordTransmissionAsync(DateTime transmissionDate, string? trackingId);

    /// <summary>
    /// Operator records IHS's response. <paramref name="accepted"/>=true →
    /// status becomes <see cref="GpraSubmissionStatus.Accepted"/>; false →
    /// <see cref="GpraSubmissionStatus.Rejected"/>.
    /// <paramref name="responseReceipt"/> captures the verbatim IHS message
    /// (acceptance receipt or rejection error list).
    /// </summary>
    [RequiresSecurityKey(SecurityKeys.CanSubmitGpra)]
    [AuditAction("GPRA", "RECORD_IHS_RESPONSE", EntityType = "GpraReport", IsClinicalWrite = false)]
    Task RecordIhsResponseAsync(DateTime responseDate, bool accepted, string? responseReceipt);
}
