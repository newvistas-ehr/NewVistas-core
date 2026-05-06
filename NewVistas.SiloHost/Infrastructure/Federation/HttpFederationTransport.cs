// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Federation;
using Orleans.Serialization;

namespace NewVistas.SiloHost.Infrastructure.Federation;

/// <summary>
/// Real <see cref="IFederationTransport"/> implementation: serializes the
/// batch as <see cref="InboundFederationBatch"/>, POSTs JSON to the upstream
/// cluster's <c>/api/federation/inbound</c> endpoint, and interprets the
/// response.
///
/// <para>
/// Unauthenticated today — <see cref="HttpFederationTransport"/> sends just
/// JSON over HTTP(S). Authentication (mTLS pinning, shared-secret HMAC,
/// OIDC) is its own follow-up plan; production deploys must front the
/// upstream endpoint with a network ACL or reverse proxy until that lands.
/// </para>
/// </summary>
public sealed class HttpFederationTransport : IFederationTransport
{
    /// <summary>Named <see cref="HttpClient"/> registered via <see cref="IHttpClientFactory"/>.</summary>
    public const string HttpClientName = "FederationOutbound";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Serializer<EventEnvelope> _serializer;
    private readonly IClusterIdentity _clusterIdentity;
    private readonly HttpFederationTransportOptions _options;
    private readonly ILogger<HttpFederationTransport> _logger;

    public HttpFederationTransport(
        IHttpClientFactory httpClientFactory,
        Serializer<EventEnvelope> serializer,
        IClusterIdentity clusterIdentity,
        IOptions<HttpFederationTransportOptions> options,
        ILogger<HttpFederationTransport> logger)
    {
        _httpClientFactory = httpClientFactory;
        _serializer = serializer;
        _clusterIdentity = clusterIdentity;
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.InboundUrl))
        {
            throw new InvalidOperationException(
                $"{nameof(HttpFederationTransport)} registered but '{HttpFederationTransportOptions.SectionName}:{nameof(HttpFederationTransportOptions.InboundUrl)}' is not configured.");
        }
    }

    public async Task<TransportResult> SendAsync(
        IReadOnlyList<EventEnvelope> batch,
        CancellationToken cancellationToken)
    {
        if (batch.Count == 0) return TransportResult.Ok();

        // Wire format mirrors what the SQL outbox already stores — Orleans
        // binary blobs preserve the polymorphic payload faithfully across the wire.
        var dto = new InboundFederationBatch
        {
            FromClusterId = _clusterIdentity.LocalClusterId,
            EnvelopeBlobs = batch.Select(_serializer.SerializeToArray).ToList()
        };

        HttpClient client = _httpClientFactory.CreateClient(HttpClientName);

        // Per-call timeout: use a linked CTS so the configured timeout caps
        // the wait without disturbing the host's stopping token.
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            using HttpResponseMessage response = await client.PostAsJsonAsync(
                _options.InboundUrl!, dto, FederationJsonOptions.Default, linkedCts.Token);

            if (!response.IsSuccessStatusCode)
            {
                return TransportResult.Fail(
                    $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
            }

            InboundApplyResult? result;
            try
            {
                result = await response.Content.ReadFromJsonAsync<InboundApplyResult>(
                    FederationJsonOptions.Default, linkedCts.Token);
            }
            catch (Exception ex)
            {
                return TransportResult.Fail($"invalid response body: {ex.GetType().Name}: {ex.Message}");
            }

            if (result is null)
            {
                return TransportResult.Fail("receiver returned 2xx with empty body");
            }

            if (result.Errors > 0)
            {
                // Conservative: any per-envelope error at the receiver triggers
                // a retry. The receiver's grain dedupes already-applied envelopes,
                // so this is correct (if eventually wasteful for permanently-bad
                // envelopes — operator monitors via Attempts/LastError).
                return TransportResult.Fail(
                    $"receiver partial failure: applied={result.Applied} errors={result.Errors}/{result.Total}");
            }

            _logger.LogDebug(
                "HTTP transport shipped {Count} envelope(s) to {Url}; receiver applied={Applied}",
                batch.Count, _options.InboundUrl, result.Applied);
            return TransportResult.Ok();
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return TransportResult.Fail($"timeout after {_options.TimeoutSeconds}s");
        }
        catch (HttpRequestException ex)
        {
            return TransportResult.Fail($"{nameof(HttpRequestException)}: {ex.Message}");
        }
    }
}
