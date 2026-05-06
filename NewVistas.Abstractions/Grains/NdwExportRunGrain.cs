// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Reporting;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Implements <see cref="INdwExportRunGrain"/>. See that interface for
/// behaviour and audit-key contract.
///
/// Filesystem note: the formatter writes files to the silo's local
/// filesystem. For multi-silo deployments, use a shared filesystem so the
/// IHS-uploading operator can reach the files. For single-silo deployments,
/// any writable directory works.
/// </summary>
public class NdwExportRunGrain : Grain, INdwExportRunGrain
{
    private readonly IPersistentState<NdwExportRunState> _state;
    private readonly INdwExportFormatter _formatter;
    private readonly INdwExportSourceProvider _sourceProvider;

    public NdwExportRunGrain(
        [PersistentState("ndwExportRunState", "ndwExportRunStore")]
        IPersistentState<NdwExportRunState> state,
        INdwExportFormatter formatter,
        INdwExportSourceProvider sourceProvider)
    {
        _state = state;
        _formatter = formatter;
        _sourceProvider = sourceProvider;
    }

    public Task<NdwExportRunState> GetAsync() => Task.FromResult(_state.State);

    public async Task<NdwExportRunState> PackageAsync(
        string facilityId, DateTime periodStart, DateTime periodEnd,
        string outputDirectory, string packagedById, string packagedByName)
    {
        if (string.IsNullOrWhiteSpace(facilityId))
            throw new ArgumentException("facilityId is required.", nameof(facilityId));
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("outputDirectory is required.", nameof(outputDirectory));
        if (periodEnd < periodStart)
            throw new ArgumentException("periodEnd must be >= periodStart.", nameof(periodEnd));

        // Per-attempt subdirectory keeps prior attempts on disk for audit.
        int attempt = _state.State.PackagingAttempts + 1;
        string runDir = Path.Combine(
            outputDirectory,
            $"ndw-{facilityId}-{periodStart:yyyyMMdd}-{periodEnd:yyyyMMdd}-attempt{attempt:D2}");
        Directory.CreateDirectory(runDir);

        // Cohort selection.
        IReadOnlyList<string> icns = await _sourceProvider.GetPatientIcnsForExportAsync(
            facilityId, periodStart, periodEnd);

        // Format. The formatter writes per-domain files directly into runDir
        // and returns the relative filenames it wrote.
        var ctx = new NdwExportContext
        {
            OutputDirectory = runDir,
            GrainFactory = GrainFactory,
            PatientIcns = icns,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            FacilityId = facilityId,
        };
        IReadOnlyList<string> writtenFiles = await _formatter.WriteToAsync(ctx);

        // Compute per-file size + sha256 from disk after the formatter is done.
        var fileRecords = new List<NdwExportFile>(writtenFiles.Count);
        foreach (string rel in writtenFiles)
        {
            string abs = Path.Combine(runDir, rel);
            byte[] bytes = await File.ReadAllBytesAsync(abs);
            fileRecords.Add(new NdwExportFile
            {
                FileName = rel,
                FileSizeBytes = bytes.LongLength,
                Sha256 = HexHash(bytes),
            });
        }

        // Persist the run record.
        _state.State.RunId = this.GetPrimaryKeyString();
        _state.State.Status = NdwExportRunStatus.Packaged;
        _state.State.FacilityId = facilityId;
        _state.State.PeriodStart = periodStart;
        _state.State.PeriodEnd = periodEnd;
        _state.State.OutputDirectory = runDir;
        _state.State.FormatVersion = _formatter.FormatVersion;
        _state.State.PatientCount = icns.Count;
        _state.State.Files = fileRecords;
        _state.State.PackagingAttempts = attempt;
        _state.State.PackagedDate = DateTime.UtcNow;
        _state.State.PackagedById = packagedById;
        _state.State.PackagedByName = packagedByName;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();

        return _state.State;
    }

    public async Task RecordTransmissionAsync(DateTime transmissionDate, string? trackingId)
    {
        if (_state.State.Status is not (NdwExportRunStatus.Packaged or NdwExportRunStatus.Rejected))
            throw new InvalidOperationException(
                $"Cannot record transmission from status {_state.State.Status}; package the export first.");

        _state.State.TransmissionDate = transmissionDate;
        _state.State.TransmissionTrackingId = trackingId;
        _state.State.Status = NdwExportRunStatus.Submitted;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordIhsResponseAsync(DateTime responseDate, bool accepted, string? responseReceipt)
    {
        if (_state.State.Status != NdwExportRunStatus.Submitted)
            throw new InvalidOperationException(
                $"Cannot record IHS response from status {_state.State.Status}; record transmission first.");

        _state.State.IhsResponseDate = responseDate;
        _state.State.IhsAccepted = accepted;
        _state.State.IhsResponseReceipt = responseReceipt;
        _state.State.Status = accepted ? NdwExportRunStatus.Accepted : NdwExportRunStatus.Rejected;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    private static string HexHash(byte[] bytes)
    {
        byte[] hash = SHA256.HashData(bytes);
        var sb = new StringBuilder(hash.Length * 2);
        foreach (byte b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        return sb.ToString();
    }
}
