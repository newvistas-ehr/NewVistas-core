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
/// Implements <see cref="IGpraSubmissionGrain"/>. See that interface for
/// behaviour and audit-key contract.
///
/// Filesystem note: this grain writes to the silo's local filesystem at
/// <c>outputDirectory</c>. For multi-silo deployments, the directory must be
/// a shared filesystem (NFS / SMB / Azure Files) that all silos and the
/// IHS-portal-uploading operator can reach. For single-silo deployments
/// (LocalhostDev, RemoteOnline, RemoteOffline) any writable directory works.
/// </summary>
public class GpraSubmissionGrain : Grain, IGpraSubmissionGrain
{
    private readonly IPersistentState<GpraSubmissionState> _state;
    private readonly IGpraSubmissionFormatter _formatter;

    public GpraSubmissionGrain(
        [PersistentState("gpraSubmissionState", "gpraSubmissionStore")]
        IPersistentState<GpraSubmissionState> state,
        IGpraSubmissionFormatter formatter)
    {
        _state = state;
        _formatter = formatter;
    }

    public Task<GpraSubmissionState> GetAsync() => Task.FromResult(_state.State);

    public async Task<GpraSubmissionState> PackageAsync(
        string reportId, string outputDirectory,
        string packagedById, string packagedByName)
    {
        if (string.IsNullOrWhiteSpace(reportId))
            throw new ArgumentException("reportId is required.", nameof(reportId));
        if (string.IsNullOrWhiteSpace(outputDirectory))
            throw new ArgumentException("outputDirectory is required.", nameof(outputDirectory));

        // Read the source report.
        IGpraReportGrain reportGrain = GrainFactory.GetGrain<IGpraReportGrain>($"GPRA-REPORT:{reportId}");
        GpraReportState report = await reportGrain.GetAsync();

        // Format (throws on incomplete report — see CsvGpraSubmissionFormatter).
        string contents = _formatter.Format(report);
        byte[] bytes = Encoding.UTF8.GetBytes(contents);
        string sha256 = HexHash(bytes);

        // Filename: deterministic per report+attempt so re-packaging produces
        // a distinct file (preserves the prior submission for audit).
        Directory.CreateDirectory(outputDirectory);
        int attempt = _state.State.PackagingAttempts + 1;
        string fileName =
            $"gpra-fy{report.FiscalYear}-{report.ReportingPeriod}-" +
            $"{Sanitize(report.FacilityId)}-attempt{attempt:D2}-" +
            $"{DateTime.UtcNow:yyyyMMddHHmmss}{_formatter.FileExtension}";
        string filePath = Path.Combine(outputDirectory, fileName);
        await File.WriteAllBytesAsync(filePath, bytes);

        // Persist the submission record.
        _state.State.SubmissionId = this.GetPrimaryKeyString();
        _state.State.ReportId = reportId;
        _state.State.Status = GpraSubmissionStatus.Packaged;
        _state.State.FilePath = filePath;
        _state.State.FormatVersion = _formatter.FormatVersion;
        _state.State.FileSizeBytes = bytes.LongLength;
        _state.State.FileSha256 = sha256;
        _state.State.PackagedDate = DateTime.UtcNow;
        _state.State.PackagedById = packagedById;
        _state.State.PackagedByName = packagedByName;
        _state.State.PackagingAttempts = attempt;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();

        return _state.State;
    }

    public async Task RecordTransmissionAsync(DateTime transmissionDate, string? trackingId)
    {
        if (_state.State.Status is not (GpraSubmissionStatus.Packaged or GpraSubmissionStatus.Rejected))
            throw new InvalidOperationException(
                $"Cannot record transmission from status {_state.State.Status}; package the submission first.");

        _state.State.TransmissionDate = transmissionDate;
        _state.State.TransmissionTrackingId = trackingId;
        _state.State.Status = GpraSubmissionStatus.Submitted;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordIhsResponseAsync(DateTime responseDate, bool accepted, string? responseReceipt)
    {
        if (_state.State.Status != GpraSubmissionStatus.Submitted)
            throw new InvalidOperationException(
                $"Cannot record IHS response from status {_state.State.Status}; record transmission first.");

        _state.State.IhsResponseDate = responseDate;
        _state.State.IhsAccepted = accepted;
        _state.State.IhsResponseReceipt = responseReceipt;
        _state.State.Status = accepted ? GpraSubmissionStatus.Accepted : GpraSubmissionStatus.Rejected;
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

    /// <summary>Strip filesystem-unsafe chars from a facility id used in the filename.</summary>
    private static string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "unknown";
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
            sb.Append(char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_');
        return sb.ToString();
    }
}
