// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Reporting;

/// <summary>
/// Per-deployment policy for serializing a completed GPRA report into the
/// file format the IHS national office expects for submission.
///
/// <para>
/// One implementation is registered as a singleton per silo. The default
/// (<see cref="CsvGpraSubmissionFormatter"/>) produces a CSV file with a
/// reasonable column layout based on documented IHS conventions; deployments
/// that need to match the current authoritative IHS GPRA+ submission spec
/// register their own formatter (e.g., a fixed-width or XML implementation)
/// without changing any caller code.
/// </para>
///
/// The formatter is pure: it transforms <see cref="GpraReportState"/> into a
/// string. The <see cref="GrainInterfaces.IGpraSubmissionGrain"/> handles the
/// file I/O, persistence, and audit trail.
/// </summary>
// TODO: replace CsvGpraSubmissionFormatter with the official IHS GPRA+
// submission format once obtained from the IHS Office of Information
// Technology (the IHS-coordination calendar item from the tribal-deployment
// plan). The interface stays; only the implementation swaps.
public interface IGpraSubmissionFormatter
{
    /// <summary>
    /// File extension (including the leading dot) appropriate for the format
    /// — e.g., <c>".csv"</c>, <c>".xml"</c>, <c>".txt"</c>.
    /// </summary>
    string FileExtension { get; }

    /// <summary>
    /// Stable version identifier persisted on the submission record so an
    /// operator can later tell which format spec was used.
    /// </summary>
    string FormatVersion { get; }

    /// <summary>
    /// Produce the submission file contents from a completed GPRA report.
    /// </summary>
    /// <exception cref="ArgumentException">If the report is incomplete (no indicators) or in a non-Completed status.</exception>
    string Format(GpraReportState report);
}
