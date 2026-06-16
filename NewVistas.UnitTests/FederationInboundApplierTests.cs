// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.Logging.Abstractions;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Events.Clinical.Problems;
using NewVistas.Abstractions.Federation;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.UnitTests;

/// <summary>
/// Behavioural tests for the inbound federation applier — feeds envelopes
/// through the real <c>SharedCluster</c> stream grain and asserts on its
/// observable state. The applier itself is pure logic; the integration with
/// the grain's idempotent dedupe and source-cluster preservation is what
/// matters.
/// </summary>
[TestFixture]
public class FederationInboundApplierTests
{
    private TestCluster _cluster = default!;
    private FederationInboundApplier _applier = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
        _applier = new FederationInboundApplier(
            _cluster.GrainFactory,
            new DefaultMpiInboundHandler(_cluster.GrainFactory, NullLogger<DefaultMpiInboundHandler>.Instance),
            NullLogger<FederationInboundApplier>.Instance);
    }

    private IPatientClinicalEventStreamGrain Stream(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientClinicalEventStreamGrain>(patientId);

    private static EventEnvelope NewEnvelope(string patientId, string? sourceClusterId = null) =>
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
            SourceClusterId = sourceClusterId ?? string.Empty
        };

    [Test]
    public async Task ApplyBatch_FreshEnvelopes_AllApplied_VersionAdvances()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        var batch = new[]
        {
            NewEnvelope(patientId, "UPSTREAM"),
            NewEnvelope(patientId, "UPSTREAM"),
            NewEnvelope(patientId, "UPSTREAM"),
        };

        InboundApplyResult result =
            await _applier.ApplyBatchAsync(batch, fromClusterId: "HUB", CancellationToken.None);

        Assert.That(result.Total, Is.EqualTo(3));
        Assert.That(result.Applied, Is.EqualTo(3));
        Assert.That(result.Errors, Is.EqualTo(0));
        int version = await Stream(patientId).GetVersionAsync();
        Assert.That(version, Is.EqualTo(3));
    }

    [Test]
    public async Task ApplyBatch_PreservesUpstreamSourceClusterId()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        EventEnvelope env = NewEnvelope(patientId, sourceClusterId: "UPSTREAM-A");

        await _applier.ApplyBatchAsync(new[] { env }, fromClusterId: "HUB", CancellationToken.None);

        IReadOnlyList<EventEnvelope> persisted = await Stream(patientId).ReadAsync(0, 10);
        Assert.That(persisted, Has.Count.EqualTo(1));
        Assert.That(persisted[0].SourceClusterId, Is.EqualTo("UPSTREAM-A"),
            "Hub-forwarded envelope must keep the original origin id, not be re-attributed to the hub.");
    }

    [Test]
    public async Task ApplyBatch_StampsFromClusterIdWhenSourceMissing()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        EventEnvelope env = NewEnvelope(patientId, sourceClusterId: null);  // empty

        await _applier.ApplyBatchAsync(new[] { env }, fromClusterId: "DIRECT-PEER", CancellationToken.None);

        IReadOnlyList<EventEnvelope> persisted = await Stream(patientId).ReadAsync(0, 10);
        Assert.That(persisted, Has.Count.EqualTo(1));
        Assert.That(persisted[0].SourceClusterId, Is.EqualTo("DIRECT-PEER"),
            "Envelope arriving without origin attribution should be stamped with the authenticated sender.");
    }

    [Test]
    public async Task ApplyBatch_DuplicateEventIds_AreNoOps_CountedAsApplied()
    {
        string patientId = $"PAT-{Guid.NewGuid()}";
        EventEnvelope env = NewEnvelope(patientId, "UPSTREAM");

        InboundApplyResult first =
            await _applier.ApplyBatchAsync(new[] { env }, "HUB", CancellationToken.None);
        InboundApplyResult second =
            await _applier.ApplyBatchAsync(new[] { env }, "HUB", CancellationToken.None);

        // Both calls succeed at the applier level — the grain dedupes silently,
        // which from the sender's perspective is "applied".
        Assert.That(first.Applied, Is.EqualTo(1));
        Assert.That(second.Applied, Is.EqualTo(1));
        Assert.That(second.Errors, Is.EqualTo(0));

        // Stream version stays at 1 — the second append was dedup'd.
        int version = await Stream(patientId).GetVersionAsync();
        Assert.That(version, Is.EqualTo(1));
    }

    [Test]
    public async Task ApplyBatch_EmptyPatientId_IsCountedAsError()
    {
        EventEnvelope env = NewEnvelope(patientId: string.Empty, sourceClusterId: "UPSTREAM");

        InboundApplyResult result =
            await _applier.ApplyBatchAsync(new[] { env }, "HUB", CancellationToken.None);

        Assert.That(result.Total, Is.EqualTo(1));
        Assert.That(result.Applied, Is.EqualTo(0));
        Assert.That(result.Errors, Is.EqualTo(1));
    }

    [Test]
    public async Task ApplyBatch_MixedValidAndInvalid_PartialSuccess()
    {
        string validPatient = $"PAT-{Guid.NewGuid()}";
        var batch = new[]
        {
            NewEnvelope(validPatient, "UPSTREAM"),                   // valid
            NewEnvelope(patientId: string.Empty, "UPSTREAM"),        // bad — no patient
            NewEnvelope(validPatient, "UPSTREAM"),                   // valid
        };

        InboundApplyResult result =
            await _applier.ApplyBatchAsync(batch, "HUB", CancellationToken.None);

        Assert.That(result.Total, Is.EqualTo(3));
        Assert.That(result.Applied, Is.EqualTo(2));
        Assert.That(result.Errors, Is.EqualTo(1));
        int version = await Stream(validPatient).GetVersionAsync();
        Assert.That(version, Is.EqualTo(2));
    }

    [Test]
    public async Task ApplyBatch_EmptyBatch_ReturnsEmptyResult()
    {
        InboundApplyResult result =
            await _applier.ApplyBatchAsync(Array.Empty<EventEnvelope>(), "HUB", CancellationToken.None);

        Assert.That(result, Is.EqualTo(InboundApplyResult.Empty));
    }

    [Test]
    public void ApplyBatch_EmptyFromClusterId_Throws()
    {
        Assert.That(
            async () => await _applier.ApplyBatchAsync(
                new[] { NewEnvelope("PAT-1", "UPSTREAM") },
                fromClusterId: "",
                CancellationToken.None),
            Throws.ArgumentException);
    }

    [Test]
    public async Task ApplyBatch_ChainVerifiesAfterApplying_AcrossSourceClusters()
    {
        // Mixed origins on the same patient's chain — the local hash chain
        // must still verify because each envelope's hash is sealed by the
        // local stream grain at append time, regardless of upstream origin.
        string patientId = $"PAT-{Guid.NewGuid()}";
        var batch = new[]
        {
            NewEnvelope(patientId, "UPSTREAM-A"),
            NewEnvelope(patientId, "UPSTREAM-B"),
            NewEnvelope(patientId, sourceClusterId: null),  // gets stamped with "HUB"
        };

        await _applier.ApplyBatchAsync(batch, "HUB", CancellationToken.None);

        bool ok = await Stream(patientId).VerifyChainAsync();
        Assert.That(ok, Is.True);
    }
}
