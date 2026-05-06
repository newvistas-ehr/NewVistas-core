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
/// Behavioural tests for <see cref="FileBundleInboundService"/>. Pure
/// in-process I/O against a per-test temp directory, with a stub
/// <see cref="IFederationInboundApplier"/> that records calls.
/// </summary>
[TestFixture]
public class FileBundleInboundServiceTests
{
    private string _tempDir = default!;
    private string _inboundDir = default!;
    private string _processedDir = default!;
    private Serializer<EventEnvelope> _serializer = default!;
    private StubApplier _applier = default!;

    [SetUp]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"newvistas-bundle-in-{Guid.NewGuid():N}");
        _inboundDir = Path.Combine(_tempDir, "inbound");
        _processedDir = Path.Combine(_tempDir, "processed");
        Directory.CreateDirectory(_inboundDir);

        var services = new ServiceCollection();
        services.AddSerializer();
        ServiceProvider sp = services.BuildServiceProvider();
        _serializer = sp.GetRequiredService<Serializer<EventEnvelope>>();

        _applier = new StubApplier();
    }

    [TearDown]
    public void Teardown()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private FileBundleInboundService BuildService()
    {
        var options = Options.Create(new FileBundleOptions
        {
            InboundDirectory = _inboundDir,
            ProcessedDirectory = _processedDir,
            ScanIntervalSeconds = 60,
        });
        return new FileBundleInboundService(
            _applier, _serializer, options,
            NullLogger<FileBundleInboundService>.Instance);
    }

    private static EventEnvelope NewEnvelope(string patientId, string sourceClusterId = "PEER-A") =>
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
            SourceClusterId = sourceClusterId,
            EventHash = "deadbeef",
            PreviousEventHash = "0000"
        };

    private async Task WriteBundle(string filename, string fromClusterId, params EventEnvelope[] envelopes)
    {
        var batch = new InboundFederationBatch
        {
            FromClusterId = fromClusterId,
            EnvelopeBlobs = envelopes.Select(_serializer.SerializeToArray).ToList()
        };
        string path = Path.Combine(_inboundDir, filename);
        await using FileStream fs = File.Create(path);
        await JsonSerializer.SerializeAsync(fs, batch, FederationJsonOptions.Default);
    }

    // ── Tests ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Process_ValidBundle_AppliesAndMovesToProcessed()
    {
        EventEnvelope env1 = NewEnvelope("PAT-A");
        EventEnvelope env2 = NewEnvelope("PAT-B");
        await WriteBundle("20260427-100000-AAAAAAAA.bundle", "PEER-A", env1, env2);

        FileBundleInboundService service = BuildService();
        await service.ProcessOnceAsync(CancellationToken.None);

        Assert.That(_applier.Calls, Has.Count.EqualTo(1));
        Assert.That(_applier.Calls.Single().fromClusterId, Is.EqualTo("PEER-A"));
        Assert.That(_applier.Calls.Single().envelopes, Has.Count.EqualTo(2));
        Assert.That(Directory.GetFiles(_inboundDir, "*.bundle"), Is.Empty,
            "Successfully-applied bundle should be moved out of inbound.");
        Assert.That(Directory.GetFiles(_processedDir, "*.bundle"), Has.Length.EqualTo(1));
    }

    [Test]
    public async Task Process_ApplierReportsErrors_BundleStaysInInbound()
    {
        await WriteBundle("test.bundle", "PEER-A", NewEnvelope("PAT-A"));

        // Configure stub to report 1 error.
        _applier.Result = new InboundApplyResult(Total: 1, Applied: 0, Errors: 1);

        FileBundleInboundService service = BuildService();
        await service.ProcessOnceAsync(CancellationToken.None);

        Assert.That(_applier.Calls, Has.Count.EqualTo(1));
        Assert.That(Directory.GetFiles(_inboundDir, "*.bundle"), Has.Length.EqualTo(1),
            "Failed bundle stays put for retry/operator investigation.");
    }

    [Test]
    public async Task Process_MalformedJson_MovesToFailedSubdirectory()
    {
        File.WriteAllText(Path.Combine(_inboundDir, "garbage.bundle"), "not json at all");

        FileBundleInboundService service = BuildService();
        await service.ProcessOnceAsync(CancellationToken.None);

        Assert.That(_applier.Calls, Is.Empty);
        Assert.That(Directory.GetFiles(_inboundDir, "*.bundle"), Is.Empty);
        string failedDir = Path.Combine(_processedDir, "failed");
        Assert.That(Directory.Exists(failedDir), Is.True);
        Assert.That(Directory.GetFiles(failedDir, "*.bundle"), Has.Length.EqualTo(1));
    }

    [Test]
    public async Task Process_EmptyFromClusterId_MovesToFailed()
    {
        await WriteBundle("test.bundle", fromClusterId: "", NewEnvelope("PAT-A"));

        FileBundleInboundService service = BuildService();
        await service.ProcessOnceAsync(CancellationToken.None);

        Assert.That(_applier.Calls, Is.Empty);
        string failedDir = Path.Combine(_processedDir, "failed");
        Assert.That(Directory.GetFiles(failedDir, "*.bundle"), Has.Length.EqualTo(1));
    }

    [Test]
    public async Task Process_NoBundlesPresent_NoOp()
    {
        FileBundleInboundService service = BuildService();
        await service.ProcessOnceAsync(CancellationToken.None);

        Assert.That(_applier.Calls, Is.Empty);
    }

    [Test]
    public async Task Process_MultipleBundles_ProcessedInSortedOrder()
    {
        await WriteBundle("20260427-090000-AAAAAAAA.bundle", "PEER-A", NewEnvelope("PAT-1"));
        await WriteBundle("20260427-100000-BBBBBBBB.bundle", "PEER-A", NewEnvelope("PAT-2"));
        await WriteBundle("20260427-080000-CCCCCCCC.bundle", "PEER-A", NewEnvelope("PAT-3"));

        FileBundleInboundService service = BuildService();
        await service.ProcessOnceAsync(CancellationToken.None);

        Assert.That(_applier.Calls, Has.Count.EqualTo(3));
        // Filenames sort lexicographically; the timestamp prefix makes that
        // chronological. Verify by patient id.
        Assert.That(_applier.Calls[0].envelopes.Single().PatientId, Is.EqualTo("PAT-3"));  // 08:00
        Assert.That(_applier.Calls[1].envelopes.Single().PatientId, Is.EqualTo("PAT-1"));  // 09:00
        Assert.That(_applier.Calls[2].envelopes.Single().PatientId, Is.EqualTo("PAT-2"));  // 10:00
    }

    private sealed class StubApplier : IFederationInboundApplier
    {
        public List<(IReadOnlyList<EventEnvelope> envelopes, string fromClusterId)> Calls { get; } = new();
        public InboundApplyResult Result { get; set; } =
            new(Total: 0, Applied: 0, Errors: 0);

        public Task<InboundApplyResult> ApplyBatchAsync(
            IReadOnlyList<EventEnvelope> envelopes,
            string fromClusterId,
            CancellationToken cancellationToken)
        {
            Calls.Add((envelopes, fromClusterId));
            // Default: report all applied, no errors.
            if (Result.Total == 0 && Result.Applied == 0 && Result.Errors == 0)
            {
                return Task.FromResult(new InboundApplyResult(envelopes.Count, envelopes.Count, 0));
            }
            return Task.FromResult(Result);
        }
    }
}
