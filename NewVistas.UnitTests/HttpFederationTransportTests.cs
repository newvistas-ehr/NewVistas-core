// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Events.Clinical.Problems;
using NewVistas.Abstractions.Federation;
using NewVistas.Abstractions.GrainStates;
using NewVistas.SiloHost.Infrastructure.Federation;
using Orleans.Serialization;

namespace NewVistas.UnitTests;

/// <summary>
/// Behavioural tests for <see cref="HttpFederationTransport"/> against a
/// scripted <see cref="HttpMessageHandler"/>. No real network I/O.
/// </summary>
[TestFixture]
public class HttpFederationTransportTests
{
    private const string TestUrl = "https://hub.example.test/api/federation/inbound";
    private const string SenderClusterId = "SENDER-CLINIC";

    private static Serializer<EventEnvelope> BuildSerializer()
    {
        var services = new ServiceCollection();
        services.AddSerializer();
        ServiceProvider sp = services.BuildServiceProvider();
        return sp.GetRequiredService<Serializer<EventEnvelope>>();
    }

    private static EventEnvelope NewEnvelope(string patientId) =>
        EventEnvelope.Wrap(new ProblemAddedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = patientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = "USR-1",
            UserName = "Smith,Jane",
            ProblemId = $"PROB-{Guid.NewGuid()}",
            Snapshot = new ProblemEntry
            {
                ProblemId = "PROB-1",
                Diagnosis = "Hypertension",
                DiagnosisCode = "I10",
                Status = "ACTIVE",
                DateRecorded = DateTime.UtcNow
            }
        }) with
        {
            SourceClusterId = SenderClusterId,
            EventHash = "deadbeef",
            PreviousEventHash = "0000"
        };

    private static HttpFederationTransport BuildTransport(
        ScriptedHttpMessageHandler handler,
        Serializer<EventEnvelope>? serializer = null,
        int timeoutSeconds = 60)
    {
        var factory = new SingleHandlerHttpClientFactory(handler);
        var options = Options.Create(new HttpFederationTransportOptions
        {
            InboundUrl = TestUrl,
            TimeoutSeconds = timeoutSeconds
        });
        return new HttpFederationTransport(
            factory,
            serializer ?? BuildSerializer(),
            new StaticClusterIdentity(SenderClusterId, "099"),
            options,
            NullLogger<HttpFederationTransport>.Instance);
    }

    [Test]
    public async Task Send_2xxOkWithZeroErrors_ReturnsOk()
    {
        var handler = new ScriptedHttpMessageHandler(
            (_, _) => Task.FromResult(OkJson(new InboundApplyResult(1, 1, 0))));
        HttpFederationTransport transport = BuildTransport(handler);

        TransportResult result = await transport.SendAsync(
            new[] { NewEnvelope("PAT-1") }, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(handler.Calls, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Send_2xxOkWithErrors_ReturnsFail_DescribesPartial()
    {
        var handler = new ScriptedHttpMessageHandler(
            (_, _) => Task.FromResult(OkJson(new InboundApplyResult(3, 1, 2))));
        HttpFederationTransport transport = BuildTransport(handler);

        TransportResult result = await transport.SendAsync(
            new[] { NewEnvelope("PAT-1"), NewEnvelope("PAT-2"), NewEnvelope("PAT-3") },
            CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Does.Contain("partial").IgnoreCase);
        Assert.That(result.Error, Does.Contain("applied=1"));
        Assert.That(result.Error, Does.Contain("errors=2"));
    }

    [Test]
    public async Task Send_5xx_ReturnsFail_IncludesStatus()
    {
        var handler = new ScriptedHttpMessageHandler(
            (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                ReasonPhrase = "Internal Server Error"
            }));
        HttpFederationTransport transport = BuildTransport(handler);

        TransportResult result = await transport.SendAsync(
            new[] { NewEnvelope("PAT-1") }, CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Does.Contain("500"));
    }

    [Test]
    public async Task Send_HttpRequestException_ReturnsFail()
    {
        var handler = new ScriptedHttpMessageHandler((_, _) =>
            Task.FromException<HttpResponseMessage>(new HttpRequestException("connection refused")));
        HttpFederationTransport transport = BuildTransport(handler);

        TransportResult result = await transport.SendAsync(
            new[] { NewEnvelope("PAT-1") }, CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Does.Contain("HttpRequestException"));
        Assert.That(result.Error, Does.Contain("connection refused"));
    }

    [Test]
    public async Task Send_Timeout_ReturnsFail()
    {
        var handler = new ScriptedHttpMessageHandler(async (_, ct) =>
        {
            // Wait for the linked timeout to cancel us; if we ever hit 5s,
            // the test will fail with a timeout reading the wrong message.
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
            return OkJson(new InboundApplyResult(1, 1, 0));
        });
        HttpFederationTransport transport = BuildTransport(handler, timeoutSeconds: 1);

        TransportResult result = await transport.SendAsync(
            new[] { NewEnvelope("PAT-1") }, CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Does.Contain("timeout").IgnoreCase);
    }

    [Test]
    public async Task Send_RoundTripsBatchShape()
    {
        EventEnvelope env1 = NewEnvelope("PAT-A");
        EventEnvelope env2 = NewEnvelope("PAT-B");
        Serializer<EventEnvelope> ser = BuildSerializer();

        InboundFederationBatch? capturedBody = null;
        var handler = new ScriptedHttpMessageHandler(async (req, ct) =>
        {
            capturedBody = await req.Content!.ReadFromJsonAsync<InboundFederationBatch>(
                FederationJsonOptions.Default, ct);
            return OkJson(new InboundApplyResult(2, 2, 0));
        });

        HttpFederationTransport transport = BuildTransport(handler, serializer: ser);
        await transport.SendAsync(new[] { env1, env2 }, CancellationToken.None);

        Assert.That(capturedBody, Is.Not.Null);
        Assert.That(capturedBody!.FromClusterId, Is.EqualTo(SenderClusterId));
        Assert.That(capturedBody.EnvelopeBlobs, Has.Count.EqualTo(2));

        // Round-trip: the receiver should get back the same envelopes.
        EventEnvelope decoded1 = ser.Deserialize(capturedBody.EnvelopeBlobs[0]);
        EventEnvelope decoded2 = ser.Deserialize(capturedBody.EnvelopeBlobs[1]);
        Assert.That(decoded1.EventId, Is.EqualTo(env1.EventId));
        Assert.That(decoded2.EventId, Is.EqualTo(env2.EventId));
        Assert.That(decoded1.PatientId, Is.EqualTo("PAT-A"));
        Assert.That(decoded2.PatientId, Is.EqualTo("PAT-B"));
    }

    [Test]
    public async Task Send_EmptyBatch_DoesNotCallHttp()
    {
        var handler = new ScriptedHttpMessageHandler(
            (_, _) => Task.FromResult(OkJson(new InboundApplyResult(0, 0, 0))));
        HttpFederationTransport transport = BuildTransport(handler);

        TransportResult result = await transport.SendAsync(
            Array.Empty<EventEnvelope>(), CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(handler.Calls, Is.Empty,
            "Empty batch should short-circuit without an HTTP call.");
    }

    [Test]
    public void Constructor_WithoutUrl_Throws()
    {
        var handler = new ScriptedHttpMessageHandler(
            (_, _) => Task.FromResult(OkJson(new InboundApplyResult(0, 0, 0))));
        var factory = new SingleHandlerHttpClientFactory(handler);
        var options = Options.Create(new HttpFederationTransportOptions { InboundUrl = null });

        Assert.That(
            () => new HttpFederationTransport(
                factory,
                BuildSerializer(),
                new StaticClusterIdentity(SenderClusterId, "099"),
                options,
                NullLogger<HttpFederationTransport>.Instance),
            Throws.InvalidOperationException);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static HttpResponseMessage OkJson<T>(T body)
    {
        var resp = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(body, options: FederationJsonOptions.Default)
        };
        return resp;
    }

    private sealed class SingleHandlerHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;
        public SingleHandlerHttpClientFactory(HttpMessageHandler handler) => _handler = handler;
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    /// <summary>HTTP handler that runs a scripted callback and records each request.</summary>
    private sealed class ScriptedHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _script;

        public List<HttpRequestMessage> Calls { get; } = new();

        public ScriptedHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> script)
        {
            _script = script;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls.Add(request);
            return await _script(request, cancellationToken);
        }
    }
}
