// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.Options;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Federation;
using Orleans.Serialization;

namespace NewVistas.SiloHost.Infrastructure.Federation;

/// <summary>
/// Background worker that drains the federation outbox: read pending rows,
/// hand them to the configured <see cref="IFederationTransport"/>, mark
/// shipped on success, schedule retry with exponential backoff on failure.
///
/// Single-silo assumption — only registered by profiles that run a single silo
/// per cluster (RemoteOnline today). RemoteOffline registers the sink without
/// the drainer; events accumulate.
/// </summary>
public sealed class OutboxDrainerService : BackgroundService
{
    private readonly IOutboxRepository _repository;
    private readonly IFederationTransport _transport;
    private readonly Serializer<EventEnvelope> _serializer;
    private readonly OutboxOptions _options;
    private readonly ILogger<OutboxDrainerService> _logger;

    public OutboxDrainerService(
        IOutboxRepository repository,
        IFederationTransport transport,
        Serializer<EventEnvelope> serializer,
        IOptions<OutboxOptions> options,
        ILogger<OutboxDrainerService> logger)
    {
        _repository = repository;
        _transport = transport;
        _serializer = serializer;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(_options.StartupDelaySeconds), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        _logger.LogInformation(
            "Outbox drainer started — polling every {PollSec}s, batch size {Batch}",
            _options.PollingIntervalSeconds, _options.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DrainOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox drainer cycle failed; will retry next poll.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.PollingIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>
    /// Run one drain cycle. Exposed for unit testing — production callers go
    /// through <see cref="ExecuteAsync"/>.
    /// </summary>
    public async Task DrainOnceAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<PendingOutboxEntry> pending =
            await _repository.ReadPendingAsync(_options.BatchSize, cancellationToken);

        if (pending.Count == 0) return;

        // Deserialize each blob; if any one fails, skip it for retry rather than
        // poisoning the whole batch. A persistent deserialization failure is a
        // bug, not a transport problem — log and let it cycle through retries
        // until someone investigates.
        var envelopes = new List<EventEnvelope>(pending.Count);
        var deserializedIds = new List<string>(pending.Count);
        foreach (PendingOutboxEntry entry in pending)
        {
            try
            {
                EventEnvelope env = _serializer.Deserialize(entry.EnvelopeBlob);
                envelopes.Add(env);
                deserializedIds.Add(entry.EventId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to deserialize outbox row {EventId} (attempt {Attempts}); will retry next cycle.",
                    entry.EventId, entry.Attempts);
                await _repository.ScheduleRetryAsync(
                    new[] { entry.EventId },
                    $"deserialize: {ex.GetType().Name}: {ex.Message}",
                    ComputeBackoff(entry.Attempts),
                    cancellationToken);
            }
        }

        if (envelopes.Count == 0) return;

        TransportResult result;
        try
        {
            result = await _transport.SendAsync(envelopes, cancellationToken);
        }
        catch (Exception ex)
        {
            result = TransportResult.Fail($"{ex.GetType().Name}: {ex.Message}");
        }

        if (result.Success)
        {
            await _repository.MarkSentAsync(deserializedIds, cancellationToken);
            _logger.LogInformation("Outbox drainer shipped {Count} envelope(s).", deserializedIds.Count);
        }
        else
        {
            // All-or-nothing semantics for a batched transport: if the batch
            // failed, every row in it gets one more attempt count. Per-row
            // partial success would require a per-envelope ack from the
            // transport, which is the next plan's concern.
            int maxAttempts = pending.Max(p => p.Attempts);
            TimeSpan backoff = ComputeBackoff(maxAttempts);
            await _repository.ScheduleRetryAsync(
                deserializedIds,
                result.Error ?? "transport failure (no detail)",
                backoff,
                cancellationToken);
            _logger.LogWarning(
                "Outbox drainer transport failed for {Count} envelope(s); retry in {Backoff}. Error: {Error}",
                deserializedIds.Count, backoff, result.Error);
        }
    }

    /// <summary>
    /// Exponential backoff capped by <see cref="OutboxOptions.MaxRetrySeconds"/>.
    /// Public so unit tests in another assembly can assert the formula.
    /// </summary>
    public TimeSpan ComputeBackoff(int attempts)
    {
        // attempts is the number of *prior* failures. After the first failure
        // (attempts==0 in the row before this update) we want InitialRetrySeconds;
        // after the second (attempts==1), 2× that; and so on.
        long seconds = (long)_options.InitialRetrySeconds << Math.Min(attempts, 30);
        if (seconds > _options.MaxRetrySeconds) seconds = _options.MaxRetrySeconds;
        return TimeSpan.FromSeconds(seconds);
    }
}
