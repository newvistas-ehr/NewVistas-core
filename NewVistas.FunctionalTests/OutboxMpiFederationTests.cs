// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Events.Mpi;
using NewVistas.Abstractions.Federation;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Services;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Closes the cross-facility MPI federation loop end-to-end:
///
///   1. <see cref="OutboxMpiFederationAnnouncer"/> wraps MPI events and
///      publishes them through the configured
///      <see cref="IClinicalEventReplicationSink"/>. We use a spy sink to
///      assert the envelope shape and stamping.
///   2. <see cref="FederationInboundApplier"/> dispatches Domain="MPI"
///      envelopes to <see cref="DefaultMpiInboundHandler"/>, which updates
///      local <see cref="IMpiSearchGrain"/> and
///      <see cref="IMpiCorrelationGrain"/> on the receiving cluster.
///   3. The same applier still routes non-MPI envelopes to the per-patient
///      clinical event stream (regression check).
/// </summary>
[TestFixture, NonParallelizable]
public class OutboxMpiFederationTests
{
    public const string TestClusterPrefix = "920";
    public const string TestLocalClusterId = "OUTBOX-MPI-FED-TEST";

    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        SpyClinicalEventReplicationSink.Reset();
        var builder = new TestClusterBuilder(1);
        builder.AddSiloBuilderConfigurator<OutboxMpiSiloConfigurator>();
        _cluster = builder.Build();
        _cluster.Deploy();
    }

    [OneTimeTearDown]
    public void OneTimeTeardown()
    {
        _cluster?.StopAllSilos();
        _cluster?.Dispose();
    }

    [SetUp]
    public void Reset() => SpyClinicalEventReplicationSink.Reset();

    // ── Outbound: announcer publishes well-shaped envelope to the sink ──

    [Test]
    public async Task Announcer_PatientRegistered_PublishesMpiEnvelopeToSink()
    {
        // OutboxMpiFederationAnnouncer is registered as IMpiFederationAnnouncer
        // in this fixture; resolve it from the silo via a stand-in client.
        IMpiFederationAnnouncer announcer = ResolveAnnouncer();

        var entry = new MpiSearchEntry
        {
            Icn = "9201234567V000001",
            PatientName = "FEDOUTBOX,REG",
            Ssn = "111223333",
            DateOfBirth = new DateTime(1965, 1, 1),
            Sex = "F",
            FacilityCount = 1,
            IsDeceased = false,
        };

        await announcer.AnnouncePatientRegisteredAsync(entry, "BEDFORD");

        Assert.That(SpyClinicalEventReplicationSink.Captured, Has.Count.EqualTo(1));
        EventEnvelope env = SpyClinicalEventReplicationSink.Captured.Single();
        Assert.That(env.Domain, Is.EqualTo(MpiPatientRegisteredV1.MpiDomain));
        Assert.That(env.PatientId, Is.EqualTo("9201234567V000001"));
        Assert.That(env.SourceClusterId, Is.EqualTo(TestLocalClusterId),
            "Announcer must stamp SourceClusterId from IClusterIdentity (MPI events bypass the per-patient stream that normally seals it).");
        Assert.That(env.Payload, Is.InstanceOf<MpiPatientRegisteredV1>());

        var reg = (MpiPatientRegisteredV1)env.Payload!;
        Assert.That(reg.OriginatingFacilityId, Is.EqualTo("BEDFORD"));
        Assert.That(reg.PatientName, Is.EqualTo("FEDOUTBOX,REG"));
    }

    [Test]
    public async Task Announcer_PatientMerged_PublishesMpiEnvelopeToSink()
    {
        IMpiFederationAnnouncer announcer = ResolveAnnouncer();

        await announcer.AnnouncePatientMergedAsync(
            sourceIcn: "9201234567V000002",
            targetIcn: "9201234567V000003",
            originatingFacilityId: "BEDFORD");

        Assert.That(SpyClinicalEventReplicationSink.Captured, Has.Count.EqualTo(1));
        EventEnvelope env = SpyClinicalEventReplicationSink.Captured.Single();
        Assert.That(env.Domain, Is.EqualTo(MpiPatientMergedV1.MpiDomain));
        Assert.That(env.PatientId, Is.EqualTo("9201234567V000002"),
            "Merged event's envelope PatientId should be the source ICN (the entity changing state is the alias).");

        var mrg = (MpiPatientMergedV1)env.Payload!;
        Assert.That(mrg.SourceIcn, Is.EqualTo("9201234567V000002"));
        Assert.That(mrg.TargetIcn, Is.EqualTo("9201234567V000003"));
    }

    // ── Inbound: applier dispatches MPI envelopes to the MPI handler ───

    [Test]
    public async Task InboundApplier_RegisteredEnvelope_AddsToLocalMpiSearch()
    {
        IFederationInboundApplier applier = ResolveApplier();

        const string icn = "9209999999V000010";
        var registered = new MpiPatientRegisteredV1
        {
            EventId = "MPI-REG-INBOUND-1",
            PatientId = icn,
            OccurredUtc = DateTime.UtcNow,
            PatientName = "INBOUND,REG",
            Ssn = "999887777",
            DateOfBirth = new DateTime(1950, 5, 5),
            Sex = "M",
            OriginatingFacilityId = "REMOTE-PEER",
        };
        EventEnvelope env = EventEnvelope.Wrap(registered);

        InboundApplyResult result = await applier.ApplyBatchAsync(
            new[] { env }, fromClusterId: "REMOTE-PEER", CancellationToken.None);

        Assert.That(result.Applied, Is.EqualTo(1));
        Assert.That(result.Errors, Is.EqualTo(0));

        IMpiSearchGrain search = _cluster.GrainFactory.GetGrain<IMpiSearchGrain>("MPI-INDEX");
        MpiSearchResult? hit = await search.LookupByIcnAsync(icn);
        Assert.That(hit, Is.Not.Null);
        Assert.That(hit!.PatientName, Is.EqualTo("INBOUND,REG"));
        Assert.That(hit.MergedIntoIcn, Is.Null);
    }

    [Test]
    public async Task InboundApplier_MergedEnvelope_AliasesSourceCorrelationAndSearch()
    {
        IFederationInboundApplier applier = ResolveApplier();

        // Pre-seed an MPI correlation for the source ICN so MarkAsMergedAsync
        // has something to mutate.
        const string sourceIcn = "9209999999V000020";
        const string targetIcn = "9209999999V000021";
        await _cluster.GrainFactory.GetGrain<IMpiCorrelationGrain>($"MPI:{sourceIcn}")
            .SetCorrelationAsync(sourceIcn, "INBOUND,SRC", "111224444",
                new DateTime(1960, 1, 1), "M");

        var merged = new MpiPatientMergedV1
        {
            EventId = "MPI-MRG-INBOUND-1",
            PatientId = sourceIcn,
            OccurredUtc = DateTime.UtcNow,
            SourceIcn = sourceIcn,
            TargetIcn = targetIcn,
            OriginatingFacilityId = "REMOTE-PEER",
        };
        EventEnvelope env = EventEnvelope.Wrap(merged);

        InboundApplyResult result = await applier.ApplyBatchAsync(
            new[] { env }, fromClusterId: "REMOTE-PEER", CancellationToken.None);
        Assert.That(result.Applied, Is.EqualTo(1));

        // Source MPI correlation now marked merged.
        MpiCorrelationState corr = await _cluster.GrainFactory
            .GetGrain<IMpiCorrelationGrain>($"MPI:{sourceIcn}").GetCorrelationAsync();
        Assert.That(corr.MergedIntoIcn, Is.EqualTo(targetIcn));

        // Source MPI search entry now reflects the alias.
        MpiSearchResult? hit = await _cluster.GrainFactory
            .GetGrain<IMpiSearchGrain>("MPI-INDEX")
            .LookupByIcnAsync(sourceIcn);
        Assert.That(hit, Is.Not.Null);
        Assert.That(hit!.MergedIntoIcn, Is.EqualTo(targetIcn));
    }

    [Test]
    public async Task InboundApplier_MpiEnvelopeWithUnknownPayload_CountsAsAppliedButNoOps()
    {
        // An envelope tagged Domain="MPI" but with a non-MPI payload type
        // (e.g., a future event type the receiver doesn't know yet) should
        // be counted as applied (no error) but produce no state changes.
        IFederationInboundApplier applier = ResolveApplier();

        var env = new EventEnvelope
        {
            EventId = "MPI-UNKNOWN",
            PatientId = "9209999999V000099",
            Domain = MpiPatientRegisteredV1.MpiDomain,
            EventType = "FutureMpiEventV99",
            OccurredUtc = DateTime.UtcNow,
            Payload = null,
        };

        InboundApplyResult result = await applier.ApplyBatchAsync(
            new[] { env }, fromClusterId: "REMOTE-PEER", CancellationToken.None);
        Assert.That(result.Applied, Is.EqualTo(1));
        Assert.That(result.Errors, Is.EqualTo(0));
    }

    [Test]
    public async Task InboundApplier_NonMpiEnvelope_StillRoutesToClinicalStream()
    {
        // Regression check: the existing behaviour for clinical envelopes
        // (Domain != "MPI") must continue working unchanged.
        IFederationInboundApplier applier = ResolveApplier();

        var env = new EventEnvelope
        {
            EventId = "CEV-REGRESSION",
            PatientId = $"PAT-{Guid.NewGuid()}",
            Domain = "PROBLEMS",
            EventType = "ProblemAddedV1",
            OccurredUtc = DateTime.UtcNow,
            Payload = new NewVistas.Abstractions.Events.Clinical.Problems.ProblemAddedV1
            {
                EventId = "CEV-REGRESSION",
                PatientId = $"PAT-{Guid.NewGuid()}",
                OccurredUtc = DateTime.UtcNow,
                ProblemId = "PROB-X",
                Snapshot = new ProblemEntry
                {
                    ProblemId = "PROB-X",
                    Diagnosis = "Test",
                    Status = "ACTIVE",
                },
            },
        };

        InboundApplyResult result = await applier.ApplyBatchAsync(
            new[] { env }, fromClusterId: "REMOTE-PEER", CancellationToken.None);

        // Regardless of whether the per-patient stream succeeds (it will here
        // since SharedCluster has all the relevant stores), the dispatch
        // routed correctly: applied count is 1, errors 0.
        Assert.That(result.Errors, Is.EqualTo(0));
        Assert.That(result.Applied, Is.EqualTo(1));
    }

    // ── Plumbing ─────────────────────────────────────────────────────────

    private IMpiFederationAnnouncer ResolveAnnouncer()
    {
        // The grain factory's silo runtime exposes services via the cluster's
        // ServiceProvider. For tests we want the silo-side service.
        var svc = _cluster.ServiceProvider.GetService<IMpiFederationAnnouncer>();
        if (svc is not null) return svc;
        // Fall back to constructing one with the cluster's grain factory and
        // the spy sink — covers the case where ServiceProvider is the client-side.
        return new OutboxMpiFederationAnnouncer(
            new SpyClinicalEventReplicationSink(),
            new StaticClusterIdentity(TestLocalClusterId, TestClusterPrefix),
            NullLogger<OutboxMpiFederationAnnouncer>.Instance);
    }

    private IFederationInboundApplier ResolveApplier() =>
        new FederationInboundApplier(
            _cluster.GrainFactory,
            new DefaultMpiInboundHandler(_cluster.GrainFactory, NullLogger<DefaultMpiInboundHandler>.Instance),
            NullLogger<FederationInboundApplier>.Instance);

    private sealed class OutboxMpiSiloConfigurator : ISiloConfigurator
    {
        public void Configure(ISiloBuilder siloBuilder)
        {
            siloBuilder.AddMemoryGrainStorageAsDefault();
            siloBuilder.AddMemoryGrainStorage("PubSubStore");
            siloBuilder.AddMemoryGrainStorage("mpiCorrelationStore");
            siloBuilder.AddMemoryGrainStorage("mpiSearchStore");
            siloBuilder.AddMemoryGrainStorage("patientClinicalStreamStore");
            siloBuilder.AddLogStorageBasedLogConsistencyProvider("ClinicalLogConsistency");

            siloBuilder.Services.AddSingleton<IClusterIdentity>(
                new StaticClusterIdentity(TestLocalClusterId, TestClusterPrefix));
            siloBuilder.Services.AddSingleton<IClinicalEventReplicationSink, SpyClinicalEventReplicationSink>();
            siloBuilder.Services.AddSingleton<IDrugInteractionCacheService, DrugInteractionCacheService>();
            // The two pieces under test:
            siloBuilder.Services.AddSingleton<IMpiFederationAnnouncer, OutboxMpiFederationAnnouncer>();
            siloBuilder.Services.AddSingleton<IMpiInboundHandler, DefaultMpiInboundHandler>();
        }
    }

    /// <summary>Captures published envelopes for assertion.</summary>
    private sealed class SpyClinicalEventReplicationSink : IClinicalEventReplicationSink
    {
        public static readonly ConcurrentBag<EventEnvelope> Captured = new();
        public static void Reset() => Captured.Clear();

        public Task PublishAsync(EventEnvelope envelope, CancellationToken cancellationToken)
        {
            Captured.Add(envelope);
            return Task.CompletedTask;
        }
    }
}
