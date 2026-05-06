// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.Reporting;

/// <summary>
/// Per-deployment policy for serializing a National Data Warehouse (NDW)
/// export — the per-patient extract IHS Office of Information Technology
/// expects for tribal-authority-level submission.
///
/// <para>
/// One implementation is registered as a singleton per silo. The default
/// (<see cref="CsvNdwExportFormatter"/>) produces a folder of CSV files —
/// one per data domain (patients, problems, immunizations) — with column
/// layouts based on documented IHS conventions. Deployments that have the
/// authoritative NDW spec from IHS register their own formatter without
/// touching the run grain or any caller.
/// </para>
///
/// Same architectural pattern as <see cref="IGpraSubmissionFormatter"/>;
/// the only difference is multi-file output (NDW emits per-domain files
/// in a single directory, vs. GPRA's single submission file).
/// </summary>
// TODO: replace CsvNdwExportFormatter with the official IHS NDW spec impl
// once obtained from the IHS Office of Information Technology — the same
// IHS-coordination calendar item that covers the GPRA submission spec.
public interface INdwExportFormatter
{
    /// <summary>
    /// Stable version identifier persisted on the run record so an operator
    /// can later tell which format spec was used.
    /// </summary>
    string FormatVersion { get; }

    /// <summary>
    /// Write the NDW export into <see cref="NdwExportContext.OutputDirectory"/>.
    /// Returns the relative paths of the files written (relative to the
    /// output directory) so the run grain can persist them in the audit trail.
    /// </summary>
    Task<IReadOnlyList<string>> WriteToAsync(NdwExportContext context);
}
