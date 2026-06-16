// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Text.Json;
using Microsoft.Extensions.Options;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Federation;
using Orleans.Serialization;

namespace NewVistas.SiloHost.Infrastructure.Federation;

/// <summary>
/// Sneakernet transport: writes each batch as a JSON bundle file in the
/// configured outbound directory. An operator periodically copies the
/// directory contents to a USB drive / satellite uplink / etc. for delivery
/// to peer clusters.
///
/// <para>
/// Bundle filename format:
/// <c>{occurredUtc:yyyyMMdd-HHmmss}-{shortGuid}.bundle</c> — sortable by
/// time when listed.
/// </para>
///
/// <para>
/// Drainer marks rows sent as soon as the file is fsync'd. From that point
/// on, the bundle file is the durable record; it persists until the
/// operator delivers it.
/// </para>
/// </summary>
public sealed class FileBundleFederationTransport : IFederationTransport
{
    private readonly Serializer<EventEnvelope> _serializer;
    private readonly IClusterIdentity _clusterIdentity;
    private readonly FileBundleOptions _options;
    private readonly ILogger<FileBundleFederationTransport> _logger;

    public FileBundleFederationTransport(
        Serializer<EventEnvelope> serializer,
        IClusterIdentity clusterIdentity,
        IOptions<FileBundleOptions> options,
        ILogger<FileBundleFederationTransport> logger)
    {
        _serializer = serializer;
        _clusterIdentity = clusterIdentity;
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.OutboundDirectory))
        {
            throw new InvalidOperationException(
                $"{nameof(FileBundleFederationTransport)} registered but '{FileBundleOptions.SectionName}:{nameof(FileBundleOptions.OutboundDirectory)}' is not configured.");
        }
    }

    public async Task<TransportResult> SendAsync(
        IReadOnlyList<EventEnvelope> batch,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0) return TransportResult.Ok();

        try
        {
            Directory.CreateDirectory(_options.OutboundDirectory!);

            var dto = new InboundFederationBatch
            {
                FromClusterId = _clusterIdentity.LocalClusterId,
                EnvelopeBlobs = batch.Select(_serializer.SerializeToArray).ToList()
            };

            string filename = BuildBundleFilename();
            string fullPath = Path.Combine(_options.OutboundDirectory!, filename);
            string tempPath = fullPath + ".tmp";

            // Write to a temp name then rename. Readers (the inbound service
            // on the peer side) only see complete files this way.
            await using (FileStream fs = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(fs, dto, FederationJsonOptions.Default, cancellationToken);
                await fs.FlushAsync(cancellationToken);
            }
            File.Move(tempPath, fullPath);

            _logger.LogInformation(
                "[file-bundle] wrote {Count} envelope(s) to {Filename}",
                batch.Count, filename);

            return TransportResult.Ok();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return TransportResult.Fail($"file-bundle write failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private static string BuildBundleFilename()
    {
        // 8 random hex chars are enough to make collisions astronomically unlikely
        // for two files written in the same second.
        Span<byte> rand = stackalloc byte[4];
        System.Security.Cryptography.RandomNumberGenerator.Fill(rand);
        string suffix = Convert.ToHexString(rand);
        return $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{suffix}.bundle";
    }
}
