// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Text.Json;
using Microsoft.Extensions.Options;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Federation;
using Orleans.Serialization;

namespace NewVistas.SiloHost.Infrastructure.Federation;

/// <summary>
/// Receive surface for sneakernet federation. Periodically scans
/// <see cref="FileBundleOptions.InboundDirectory"/> for <c>.bundle</c>
/// files (delivered by USB / satellite uplink / etc.), deserializes each
/// one, and applies its envelopes through
/// <see cref="IFederationInboundApplier"/>.
///
/// On success → bundle moves to <see cref="FileBundleOptions.ProcessedDirectory"/>.
/// On applier-reported errors → bundle stays in place for retry.
/// On malformed bundle → file moves to a <c>failed/</c> subdirectory under
/// processed so the operator can find it.
/// </summary>
public sealed class FileBundleInboundService : BackgroundService
{
    private readonly IFederationInboundApplier _applier;
    private readonly Serializer<EventEnvelope> _serializer;
    private readonly FileBundleOptions _options;
    private readonly ILogger<FileBundleInboundService> _logger;

    public FileBundleInboundService(
        IFederationInboundApplier applier,
        Serializer<EventEnvelope> serializer,
        IOptions<FileBundleOptions> options,
        ILogger<FileBundleInboundService> logger)
    {
        _applier = applier;
        _serializer = serializer;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.InboundDirectory))
        {
            _logger.LogWarning(
                "FileBundleInboundService started but '{Section}:{Key}' is not configured.",
                FileBundleOptions.SectionName, nameof(FileBundleOptions.InboundDirectory));
            return;
        }

        Directory.CreateDirectory(_options.InboundDirectory);
        if (!string.IsNullOrWhiteSpace(_options.ProcessedDirectory))
            Directory.CreateDirectory(_options.ProcessedDirectory);

        TimeSpan interval = TimeSpan.FromSeconds(Math.Max(1, _options.ScanIntervalSeconds));
        _logger.LogInformation(
            "File-bundle inbound service started — watching {Dir} every {Interval}",
            _options.InboundDirectory, interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "File-bundle inbound cycle failed; will retry next scan.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Run one scan-and-process cycle. Public so tests can drive the loop
    /// deterministically without the timer.
    /// </summary>
    public async Task ProcessOnceAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.InboundDirectory)) return;
        if (!Directory.Exists(_options.InboundDirectory)) return;

        // Snapshot the directory listing — files written mid-scan get picked
        // up next cycle.
        string[] bundles = Directory.GetFiles(_options.InboundDirectory, "*.bundle")
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

        foreach (string path in bundles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ProcessOneBundleAsync(path, cancellationToken);
        }
    }

    private async Task ProcessOneBundleAsync(string bundlePath, CancellationToken cancellationToken)
    {
        InboundFederationBatch? batch;
        try
        {
            await using FileStream fs = File.OpenRead(bundlePath);
            batch = await JsonSerializer.DeserializeAsync<InboundFederationBatch>(
                fs, FederationJsonOptions.Default, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Bundle {Path} could not be deserialized; moving to failed/.", bundlePath);
            MoveToFailed(bundlePath);
            return;
        }

        if (batch is null || string.IsNullOrWhiteSpace(batch.FromClusterId))
        {
            _logger.LogWarning(
                "Bundle {Path} parsed but lacks FromClusterId; moving to failed/.", bundlePath);
            MoveToFailed(bundlePath);
            return;
        }

        // Per-blob deserialize — mirrors the HTTP controller's tolerance.
        var envelopes = new List<EventEnvelope>(batch.EnvelopeBlobs.Count);
        int blobErrors = 0;
        foreach (byte[] blob in batch.EnvelopeBlobs)
        {
            try
            {
                envelopes.Add(_serializer.Deserialize(blob));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Skipping malformed envelope blob in bundle {Path}.", bundlePath);
                blobErrors++;
            }
        }

        InboundApplyResult result =
            await _applier.ApplyBatchAsync(envelopes, batch.FromClusterId, cancellationToken);

        int totalErrors = result.Errors + blobErrors;
        if (totalErrors > 0)
        {
            // Leave the file in place so operator can investigate. The
            // applier is idempotent on EventId, so a future re-scan that
            // succeeds doesn't double-apply the events that already worked.
            _logger.LogWarning(
                "Bundle {Path} applied with {Errors} errors / {Total} total envelopes; left in inbound for retry.",
                bundlePath, totalErrors, batch.EnvelopeBlobs.Count);
            return;
        }

        _logger.LogInformation(
            "Bundle {Path}: {Applied} envelope(s) applied from cluster {Cluster}.",
            bundlePath, result.Applied, batch.FromClusterId);

        MoveToProcessed(bundlePath);
    }

    private void MoveToProcessed(string bundlePath)
    {
        if (string.IsNullOrWhiteSpace(_options.ProcessedDirectory))
        {
            // No processed dir configured: just delete the file. The
            // operator's bundle-shipping workflow can re-deliver from
            // their archive if needed.
            File.Delete(bundlePath);
            return;
        }

        // Defensive: ensure the directory exists. ExecuteAsync creates it on
        // startup, but ProcessOnceAsync may be called by tests/admin tools
        // that skip the warm-up.
        Directory.CreateDirectory(_options.ProcessedDirectory);
        string dest = Path.Combine(_options.ProcessedDirectory, Path.GetFileName(bundlePath));
        File.Move(bundlePath, dest, overwrite: true);
    }

    private void MoveToFailed(string bundlePath)
    {
        string failedDir = Path.Combine(_options.ProcessedDirectory ?? _options.InboundDirectory!, "failed");
        Directory.CreateDirectory(failedDir);
        string dest = Path.Combine(failedDir, Path.GetFileName(bundlePath));
        File.Move(bundlePath, dest, overwrite: true);
    }
}
