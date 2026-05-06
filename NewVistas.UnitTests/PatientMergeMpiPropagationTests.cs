// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Tests for the MPI-propagation step of <see cref="IPatientMergeGrain"/>:
/// after a merge, the source ICN's MPI correlation grain must reflect
/// <see cref="MpiCorrelationState.MergedIntoIcn"/> and the MPI search index
/// must mark the source entry as merged. Without this, cross-cluster lookups
/// by the source ICN would still surface a separate patient.
///
/// These tests use ICN-shaped patient IDs so the merge code's MPI branch
/// (skipped when source/target lack ICNs) actually fires.
/// </summary>
[TestFixture]
public class PatientMergeMpiPropagationTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private IPatientGrain Patient(string id) =>
        _cluster.GrainFactory.GetGrain<IPatientGrain>(id);

    private IPatientMergeGrain NewMergeGrain() =>
        _cluster.GrainFactory.GetGrain<IPatientMergeGrain>($"MERGE:{Guid.NewGuid()}");

    private IMpiCorrelationGrain Correlation(string icn) =>
        _cluster.GrainFactory.GetGrain<IMpiCorrelationGrain>($"MPI:{icn}");

    private IMpiSearchGrain Search() =>
        _cluster.GrainFactory.GetGrain<IMpiSearchGrain>("MPI-INDEX");

    /// <summary>
    /// Arranges a patient with demographics, an ICN on PatientState, an MPI
    /// correlation record, and an MPI search-index entry — i.e., a normal
    /// post-registration patient.
    /// </summary>
    private async Task<string> RegisterMpiPatientAsync(string icn, string name)
    {
        await Patient(icn).UpdateDemographicsAsync(name, "M", new DateTime(1960, 1, 1), "111223333");
        await Patient(icn).SetIcnAsync(icn);

        await Correlation(icn).SetCorrelationAsync(
            icn, name, "111223333", new DateTime(1960, 1, 1), "M");
        await Correlation(icn).AddLocalCorrelationAsync(
            "TEST-CLUSTER", "TEST-CLUSTER", $"DFN-{icn}", DateTime.UtcNow);

        await Search().AddOrUpdatePatientAsync(new MpiSearchEntry
        {
            Icn = icn,
            PatientName = name,
            Ssn = "111223333",
            DateOfBirth = new DateTime(1960, 1, 1),
            Sex = "M",
            FacilityCount = 1,
            IsDeceased = false,
        });

        return icn;
    }

    private static string FreshIcn(string suffix)
    {
        // Test ICN shape: 099 prefix + 7-digit seq + V + 6-digit checksum.
        // Sequence salted from suffix so collisions across tests are unlikely.
        int seq = (Math.Abs(suffix.GetHashCode()) % 9_000_000) + 1;
        return $"099{seq:D7}V{(seq * 7) % 1_000_000:D6}";
    }

    [Test]
    public async Task Merge_MarksSourceMpiCorrelationAsMergedIntoTargetIcn()
    {
        string targetIcn = FreshIcn($"target-{Guid.NewGuid()}");
        string sourceIcn = FreshIcn($"source-{Guid.NewGuid()}");
        await RegisterMpiPatientAsync(targetIcn, "MERGEPROP,TARGET");
        await RegisterMpiPatientAsync(sourceIcn, "MERGEPROP,SOURCE");

        PatientMergeResult result = await NewMergeGrain().ExecuteMergeAsync(
            targetIcn, sourceIcn, "Duplicate", "USER1", "Admin");
        Assert.That(result.Success, Is.True, result.ErrorMessage);

        MpiCorrelationState sourceCorr = await Correlation(sourceIcn).GetCorrelationAsync();
        Assert.That(sourceCorr.MergedIntoIcn, Is.EqualTo(targetIcn));
    }

    [Test]
    public async Task Merge_LeavesTargetMpiCorrelationUnaliased()
    {
        string targetIcn = FreshIcn($"keep-target-{Guid.NewGuid()}");
        string sourceIcn = FreshIcn($"keep-source-{Guid.NewGuid()}");
        await RegisterMpiPatientAsync(targetIcn, "MERGEPROP,KEEP-TARGET");
        await RegisterMpiPatientAsync(sourceIcn, "MERGEPROP,KEEP-SOURCE");

        await NewMergeGrain().ExecuteMergeAsync(targetIcn, sourceIcn, "Dup", "USER1", "Admin");

        MpiCorrelationState targetCorr = await Correlation(targetIcn).GetCorrelationAsync();
        Assert.That(targetCorr.MergedIntoIcn, Is.Null,
            "Target ICN must remain the live primary record after a merge.");
    }

    [Test]
    public async Task Merge_StampsSearchIndexEntryWithMergedIntoIcn()
    {
        string targetIcn = FreshIcn($"search-target-{Guid.NewGuid()}");
        string sourceIcn = FreshIcn($"search-source-{Guid.NewGuid()}");
        await RegisterMpiPatientAsync(targetIcn, "SEARCHPROP,TARGET");
        await RegisterMpiPatientAsync(sourceIcn, "SEARCHPROP,SOURCE");

        await NewMergeGrain().ExecuteMergeAsync(targetIcn, sourceIcn, "Dup", "USER1", "Admin");

        MpiSearchResult? sourceHit = await Search().LookupByIcnAsync(sourceIcn);
        Assert.That(sourceHit, Is.Not.Null);
        // MpiSearchResult exposes MergedIntoIcn (added in this change).
        Assert.That(sourceHit!.MergedIntoIcn, Is.EqualTo(targetIcn),
            "MPI search result for the source ICN must surface the alias.");
    }

    [Test]
    public async Task Merge_WhenSourceLacksIcn_SkipsMpiPropagation_DoesNotThrow()
    {
        // Pre-ICN-issuance legacy data path: source has no ICN. The merge
        // should still complete (clinical data moves) but the MPI branch is
        // skipped. This test pins that we don't crash on null Icn.
        string targetIcn = FreshIcn($"legacy-target-{Guid.NewGuid()}");
        await RegisterMpiPatientAsync(targetIcn, "LEGACY,TARGET");

        string sourceLegacyId = $"LEGACY-PATIENT-{Guid.NewGuid()}";
        await Patient(sourceLegacyId).UpdateDemographicsAsync(
            "LEGACY,SOURCE", "M", new DateTime(1960, 1, 1), "111223333");
        // No SetIcnAsync, no MPI correlation, no search-index entry — simulates
        // a patient created before ICN issuance was wired up.

        PatientMergeResult result = await NewMergeGrain().ExecuteMergeAsync(
            targetIcn, sourceLegacyId, "Legacy duplicate", "USER1", "Admin");

        Assert.That(result.Success, Is.True, result.ErrorMessage);
    }

    [Test]
    public async Task MarkAsMergedAsync_IsIdempotent_OnRepeatedSameTarget()
    {
        string icn = FreshIcn($"idempotent-{Guid.NewGuid()}");
        string targetIcn = FreshIcn($"idempotent-target-{Guid.NewGuid()}");
        await RegisterMpiPatientAsync(icn, "IDEMP,SOURCE");

        await Correlation(icn).MarkAsMergedAsync(targetIcn);
        await Correlation(icn).MarkAsMergedAsync(targetIcn);   // second call — no-op

        MpiCorrelationState corr = await Correlation(icn).GetCorrelationAsync();
        Assert.That(corr.MergedIntoIcn, Is.EqualTo(targetIcn));
    }

    [Test]
    public async Task MarkAsMergedAsync_RefusesRemergeToDifferentTarget()
    {
        string icn = FreshIcn($"remerge-{Guid.NewGuid()}");
        string firstTarget = FreshIcn($"remerge-first-{Guid.NewGuid()}");
        string secondTarget = FreshIcn($"remerge-second-{Guid.NewGuid()}");
        await RegisterMpiPatientAsync(icn, "REMERGE,SOURCE");

        await Correlation(icn).MarkAsMergedAsync(firstTarget);

        Assert.That(
            async () => await Correlation(icn).MarkAsMergedAsync(secondTarget),
            Throws.InstanceOf<InvalidOperationException>().Or.InstanceOf<Orleans.Runtime.OrleansException>());
    }

    [Test]
    public async Task MarkAsMergedAsync_RejectsSelfMerge()
    {
        string icn = FreshIcn($"self-{Guid.NewGuid()}");
        await RegisterMpiPatientAsync(icn, "SELF,MERGE");

        Assert.That(
            async () => await Correlation(icn).MarkAsMergedAsync(icn),
            Throws.InstanceOf<ArgumentException>().Or.InstanceOf<Orleans.Runtime.OrleansException>());
    }
}
