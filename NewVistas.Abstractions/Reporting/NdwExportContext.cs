// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.Reporting;

/// <summary>
/// Inputs handed to <see cref="INdwExportFormatter.WriteToAsync"/> for a single
/// NDW export run. The formatter uses <see cref="GrainFactory"/> to read each
/// patient's data on demand (so per-domain output can stream rather than
/// load everything in memory) and writes its files into <see cref="OutputDirectory"/>.
/// </summary>
public sealed class NdwExportContext
{
    /// <summary>Absolute path to a writable directory; the formatter creates one or more files here.</summary>
    public required string OutputDirectory { get; init; }

    /// <summary>Orleans grain factory the formatter uses to read per-patient grain state on demand.</summary>
    public required IGrainFactory GrainFactory { get; init; }

    /// <summary>ICNs included in this export run, supplied by <see cref="INdwExportSourceProvider"/>.</summary>
    public required IReadOnlyList<string> PatientIcns { get; init; }

    /// <summary>Reporting period start (inclusive).</summary>
    public required DateTime PeriodStart { get; init; }

    /// <summary>Reporting period end (inclusive).</summary>
    public required DateTime PeriodEnd { get; init; }

    /// <summary>Submitting facility identifier (the cluster id when single-facility; the tribal authority id when consolidating).</summary>
    public required string FacilityId { get; init; }
}
