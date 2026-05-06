// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Events.Clinical.Problems;
using NewVistas.Abstractions.Federation;
using NewVistas.Abstractions.GrainStates;
using NewVistas.SiloHost.Infrastructure.Federation;
using Orleans.Serialization;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Behavioural tests for <see cref="FileBundleFederationTransport"/>. Pure
/// in-process I/O against a per-test temp directory — no Orleans cluster
/// involved.
/// </summary>
[TestFixture]
public class FileBundleTransportTests
{
    private string _tempDir = default!;
    private Serializer<EventEnvelope> _serializer = default!;

    [SetUp]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"newvistas-bundle-{Guid.NewGuid():N}");
        // Don't pre-create — verify the transport creates it.

        var services = new ServiceCollection();
        services.AddSerializer();
        ServiceProvider sp = services.BuildServiceProvider();
        _serializer = sp.GetRequiredService<Serializer<EventEnvelope>>();
    }

    [TearDown]
    public void Teardown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private FileBundleFederationTransport BuildTransport(string clusterId = "TEST-OFFLINE")
    {
        var options = Options.Create(new FileBundleOptions
        {
            OutboundDirectory = Path.Combine(_tempDir, "outbound"),
        });
        return new FileBundleFederationTransport(
            _serializer,
            new StaticClusterIdentity(clusterId, "099"),
            options,
            NullLogger<FileBundleFederationTransport>.Instance);
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
            SourceClusterId = "TEST-OFFLINE",
            EventHash = "deadbeef",
            PreviousEventHash = "0000"
        };

    [Test]
    public async Task Send_WritesBundleFileToOutboundDirectory()
    {
        FileBundleFederationTransport transport = BuildTransport();
        var batch = new[] { NewEnvelope("PAT-A"), NewEnvelope("PAT-B"), NewEnvelope("PAT-C") };

        TransportResult result = await transport.SendAsync(batch, CancellationToken.None);

        Assert.That(result.Success, Is.True);

        string outboundDir = Path.Combine(_tempDir, "outbound");
        string[] files = Directory.GetFiles(outboundDir, "*.bundle");
        Assert.That(files, Has.Length.EqualTo(1));
        Assert.That(Directory.GetFiles(outboundDir, "*.tmp"), Is.Empty,
            "Temp file should have been renamed away.");

        // Round-trip: read the bundle back and confirm it deserializes to the same envelopes.
        await using FileStream fs = File.OpenRead(files[0]);
        InboundFederationBatch? roundtrip =
            await JsonSerializer.DeserializeAsync<InboundFederationBatch>(fs, FederationJsonOptions.Default);

        Assert.That(roundtrip, Is.Not.Null);
        Assert.That(roundtrip!.FromClusterId, Is.EqualTo("TEST-OFFLINE"));
        Assert.That(roundtrip.EnvelopeBlobs, Has.Count.EqualTo(3));

        for (int i = 0; i < batch.Length; i++)
        {
            EventEnvelope decoded = _serializer.Deserialize(roundtrip.EnvelopeBlobs[i]);
            Assert.That(decoded.EventId, Is.EqualTo(batch[i].EventId));
            Assert.That(decoded.PatientId, Is.EqualTo(batch[i].PatientId));
        }
    }

    [Test]
    public async Task Send_EmptyBatch_DoesNotWriteFile()
    {
        FileBundleFederationTransport transport = BuildTransport();

        TransportResult result = await transport.SendAsync(Array.Empty<EventEnvelope>(), CancellationToken.None);

        Assert.That(result.Success, Is.True);
        string outboundDir = Path.Combine(_tempDir, "outbound");
        Assert.That(Directory.Exists(outboundDir) && Directory.GetFiles(outboundDir).Length > 0, Is.False);
    }

    [Test]
    public async Task Send_DirectoryDoesNotExist_CreatedAutomatically()
    {
        FileBundleFederationTransport transport = BuildTransport();
        string outboundDir = Path.Combine(_tempDir, "outbound");
        Assert.That(Directory.Exists(outboundDir), Is.False, "Sanity: directory should not exist before send.");

        await transport.SendAsync(new[] { NewEnvelope("PAT-A") }, CancellationToken.None);

        Assert.That(Directory.Exists(outboundDir), Is.True);
        Assert.That(Directory.GetFiles(outboundDir, "*.bundle"), Has.Length.EqualTo(1));
    }

    [Test]
    public void Constructor_WithoutOutboundDirectory_Throws()
    {
        var options = Options.Create(new FileBundleOptions { OutboundDirectory = null });

        Assert.That(
            () => new FileBundleFederationTransport(
                _serializer,
                new StaticClusterIdentity("TEST", "099"),
                options,
                NullLogger<FileBundleFederationTransport>.Instance),
            Throws.InvalidOperationException);
    }

    [Test]
    public async Task Send_TwoBatches_ProduceDistinctFilenames()
    {
        FileBundleFederationTransport transport = BuildTransport();

        await transport.SendAsync(new[] { NewEnvelope("PAT-A") }, CancellationToken.None);
        await Task.Delay(10);  // micro-delay so even-fast machines move the clock
        await transport.SendAsync(new[] { NewEnvelope("PAT-B") }, CancellationToken.None);

        string[] files = Directory.GetFiles(Path.Combine(_tempDir, "outbound"), "*.bundle");
        Assert.That(files, Has.Length.EqualTo(2));
        Assert.That(files[0], Is.Not.EqualTo(files[1]));
    }
}
