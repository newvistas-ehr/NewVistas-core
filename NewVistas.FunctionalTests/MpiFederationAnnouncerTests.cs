// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.Eligibility;
using NewVistas.Abstractions.Federation;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Services;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Validates that <see cref="IMpiFederationAnnouncer"/> is invoked at the
/// right moments by the registration and merge workflows. Uses a dedicated
/// <see cref="TestCluster"/> wired with a spy announcer (in addition to
/// <see cref="IhsTribalEligibilityPolicy"/> so the registration flow stamps
/// IHS eligibility, exercising the realistic pipeline).
///
/// This is the pre-flight check for cross-cluster MPI federation: if the
/// announce calls fire correctly here, swapping <see cref="SpyMpiFederationAnnouncer"/>
/// for an outbox-backed announcer is the only remaining work to make peer
/// clusters' MPI search/correlation grains stay in sync with this cluster's.
/// </summary>
[TestFixture, NonParallelizable]
public class MpiFederationAnnouncerTests
{
    public const string TestClusterPrefix = "920";
    public const string TestLocalClusterId = "MPI-FED-ANNOUNCE-TEST";

    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        SpyMpiFederationAnnouncer.Reset();
        var builder = new TestClusterBuilder(1);
        builder.AddSiloBuilderConfigurator<SpyAnnouncerSiloConfigurator>();
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
    public void ResetSpy() => SpyMpiFederationAnnouncer.Reset();

    private IPatientRegistrationGrain Registration() =>
        _cluster.GrainFactory.GetGrain<IPatientRegistrationGrain>("REGISTRATION");

    private static RegistrationRequest TribalRequest(string suffix)
    {
        return new RegistrationRequest
        {
            PatientName = $"FEDANNOUNCE,{suffix}",
            Ssn = "111223333",
            DateOfBirth = new DateTime(1965, 6, 12),
            Sex = "F",
            FacilityDfn = $"DFN-{suffix}",
            IsTribalMember = true,
            TribalAffiliation = "Cherokee Nation",
            ResidesInChsda = true,
            ChsdaResidencyDays = 365,
        };
    }

    [Test]
    public async Task Register_FiresAnnouncePatientRegistered()
    {
        string icn = await Registration().RegisterPatientAsync(TribalRequest("reg"));

        Assert.That(SpyMpiFederationAnnouncer.RegisteredAnnouncements,
            Has.Count.GreaterThanOrEqualTo(1),
            "Registration must invoke AnnouncePatientRegisteredAsync.");

        var (entry, originatingFacility) =
            SpyMpiFederationAnnouncer.RegisteredAnnouncements.Last();
        Assert.That(entry.Icn, Is.EqualTo(icn));
        Assert.That(entry.PatientName, Does.StartWith("FEDANNOUNCE"));
        Assert.That(originatingFacility, Is.EqualTo(TestLocalClusterId));
    }

    [Test]
    public async Task Register_AnnouncementCarriesEligibilityHints()
    {
        // The announcement entry should carry enough demographics for peers
        // to populate their MPI search index meaningfully.
        await Registration().RegisterPatientAsync(TribalRequest("hints"));

        var (entry, _) = SpyMpiFederationAnnouncer.RegisteredAnnouncements.Last();
        Assert.That(entry.Ssn, Is.EqualTo("111223333"));
        Assert.That(entry.DateOfBirth, Is.EqualTo(new DateTime(1965, 6, 12)));
        Assert.That(entry.Sex, Is.EqualTo("F"));
        Assert.That(entry.IsDeceased, Is.False);
    }

    [Test]
    public async Task Register_AnnouncerFailure_DoesNotFailRegistration()
    {
        SpyMpiFederationAnnouncer.ThrowOnNextRegistered = true;
        // The grain swallows announce failures so local registration succeeds
        // even if the federation transport is misconfigured or temporarily down.
        string icn = await Registration().RegisterPatientAsync(TribalRequest("fail-tolerant"));
        Assert.That(icn, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task Merge_FiresAnnouncePatientMerged()
    {
        // Enable PATIENT_MERGE feature first.
        ISiteParametersGrain siteParams =
            _cluster.GrainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
        await siteParams.EnableFeatureAsync("PATIENT_MERGE");

        // Register two patients to merge.
        string targetIcn = await Registration().RegisterPatientAsync(TribalRequest("merge-target"));
        string sourceIcn = await Registration().RegisterPatientAsync(TribalRequest("merge-source"));

        // Reset the spy after registration so we only capture the merge announcement.
        SpyMpiFederationAnnouncer.Reset();

        // Execute the merge directly via the merge grain.
        IPatientMergeGrain mergeGrain = _cluster.GrainFactory.GetGrain<IPatientMergeGrain>($"MERGE:{Guid.NewGuid()}");
        PatientMergeResult result = await mergeGrain.ExecuteMergeAsync(
            targetIcn, sourceIcn, "Duplicate (test)", "ADMIN1", "Admin");
        Assert.That(result.Success, Is.True, result.ErrorMessage);

        Assert.That(SpyMpiFederationAnnouncer.MergedAnnouncements,
            Has.Count.EqualTo(1),
            "Merge must invoke AnnouncePatientMergedAsync exactly once.");

        var (announcedSource, announcedTarget, originatingFacility) =
            SpyMpiFederationAnnouncer.MergedAnnouncements.Single();
        Assert.That(announcedSource, Is.EqualTo(sourceIcn));
        Assert.That(announcedTarget, Is.EqualTo(targetIcn));
        Assert.That(originatingFacility, Is.EqualTo(TestLocalClusterId));
    }

    [Test]
    public async Task Merge_AnnouncerFailure_DoesNotFailMerge()
    {
        ISiteParametersGrain siteParams =
            _cluster.GrainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
        await siteParams.EnableFeatureAsync("PATIENT_MERGE");

        string targetIcn = await Registration().RegisterPatientAsync(TribalRequest("mt-tolerant"));
        string sourceIcn = await Registration().RegisterPatientAsync(TribalRequest("ms-tolerant"));

        SpyMpiFederationAnnouncer.Reset();
        SpyMpiFederationAnnouncer.ThrowOnNextMerged = true;

        IPatientMergeGrain mergeGrain = _cluster.GrainFactory.GetGrain<IPatientMergeGrain>($"MERGE:{Guid.NewGuid()}");
        PatientMergeResult result = await mergeGrain.ExecuteMergeAsync(
            targetIcn, sourceIcn, "Duplicate", "ADMIN1", "Admin");

        Assert.That(result.Success, Is.True,
            "Local merge must succeed even when the federation announcer throws.");
    }

    [Test]
    public async Task Merge_OnPatientWithoutIcn_DoesNotAnnounce()
    {
        // Create two non-ICN patients (legacy data path simulation).
        string sourceLegacy = $"LEGACY-{Guid.NewGuid()}";
        string targetLegacy = $"LEGACY-{Guid.NewGuid()}";
        await _cluster.GrainFactory.GetGrain<IPatientGrain>(sourceLegacy)
            .UpdateDemographicsAsync("LEGACY,SOURCE", "M", new DateTime(1970, 1, 1), "111223333");
        await _cluster.GrainFactory.GetGrain<IPatientGrain>(targetLegacy)
            .UpdateDemographicsAsync("LEGACY,TARGET", "M", new DateTime(1971, 1, 1), "111223334");

        ISiteParametersGrain siteParams =
            _cluster.GrainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");
        await siteParams.EnableFeatureAsync("PATIENT_MERGE");

        SpyMpiFederationAnnouncer.Reset();

        IPatientMergeGrain mergeGrain = _cluster.GrainFactory.GetGrain<IPatientMergeGrain>($"MERGE:{Guid.NewGuid()}");
        PatientMergeResult result = await mergeGrain.ExecuteMergeAsync(
            targetLegacy, sourceLegacy, "Legacy duplicate", "ADMIN1", "Admin");
        Assert.That(result.Success, Is.True, result.ErrorMessage);

        Assert.That(SpyMpiFederationAnnouncer.MergedAnnouncements, Is.Empty,
            "Merges of pre-ICN legacy patients must not generate federation announcements (no ICN to alias).");
    }

    // ── Spy + configuration plumbing ─────────────────────────────────────────

    private sealed class SpyAnnouncerSiloConfigurator : ISiloConfigurator
    {
        public void Configure(ISiloBuilder siloBuilder)
        {
            siloBuilder.AddMemoryGrainStorageAsDefault();
            siloBuilder.AddMemoryGrainStorage("PubSubStore");
            siloBuilder.AddMemoryGrainStorage("icnIssuerStore");
            siloBuilder.AddMemoryGrainStorage("mpiCorrelationStore");
            siloBuilder.AddMemoryGrainStorage("mpiSearchStore");
            siloBuilder.AddMemoryGrainStorage("patientStore");
            // Diagnosis provenance & revision statistics (ADR-006) — AddProblemAsync opens
            // a diagnostic episode, so any silo exercising the problem list needs these.
            siloBuilder.AddMemoryGrainStorage("dxEpisodeStore");
            siloBuilder.AddMemoryGrainStorage("dxOutcomeStore");
            siloBuilder.AddMemoryGrainStorage("patientHistoryIndexStore");
            siloBuilder.AddMemoryGrainStorage("patientIndexStore");
            siloBuilder.AddMemoryGrainStorage("patientClinicalStreamStore");
            siloBuilder.AddMemoryGrainStorage("patientEnrollmentStore");
            siloBuilder.AddMemoryGrainStorage("autoEligibilityDeterminationStore");
            siloBuilder.AddMemoryGrainStorage("patientMergeStore");
            // Merge now copies PSO-INDEX prescription entries between patients.
            siloBuilder.AddMemoryGrainStorage("prescriptionIndexStore");
            siloBuilder.AddMemoryGrainStorage("siteParametersStore");
            siloBuilder.AddLogStorageBasedLogConsistencyProvider("ClinicalLogConsistency");

            siloBuilder.Services.AddSingleton<IClusterIdentity>(
                new StaticClusterIdentity(TestLocalClusterId, TestClusterPrefix));
            siloBuilder.Services.AddSingleton<IClinicalEventReplicationSink, NullClinicalEventReplicationSink>();
            siloBuilder.Services.AddSingleton<IDrugInteractionCacheService, DrugInteractionCacheService>();
            siloBuilder.Services.AddSingleton<IRegistrationEligibilityPolicy, IhsTribalEligibilityPolicy>();
            siloBuilder.Services.AddSingleton<IMpiFederationAnnouncer, SpyMpiFederationAnnouncer>();
        }
    }

    /// <summary>
    /// Test double for <see cref="IMpiFederationAnnouncer"/>. Captures all calls
    /// in static collections for assertion. Static state is acceptable because
    /// the fixture is <c>NonParallelizable</c> and resets between tests.
    /// </summary>
    private sealed class SpyMpiFederationAnnouncer : IMpiFederationAnnouncer
    {
        public static readonly ConcurrentBag<(MpiSearchEntry Entry, string Facility)>
            RegisteredAnnouncements = new();

        public static readonly ConcurrentBag<(string Source, string Target, string Facility)>
            MergedAnnouncements = new();

        public static bool ThrowOnNextRegistered { get; set; }
        public static bool ThrowOnNextMerged { get; set; }

        public static void Reset()
        {
            RegisteredAnnouncements.Clear();
            MergedAnnouncements.Clear();
            ThrowOnNextRegistered = false;
            ThrowOnNextMerged = false;
        }

        public Task AnnouncePatientRegisteredAsync(MpiSearchEntry entry, string originatingFacility)
        {
            if (ThrowOnNextRegistered)
            {
                ThrowOnNextRegistered = false;
                throw new InvalidOperationException("Spy: simulated federation outage on registered announce.");
            }
            RegisteredAnnouncements.Add((entry, originatingFacility));
            return Task.CompletedTask;
        }

        public Task AnnouncePatientMergedAsync(string sourceIcn, string targetIcn, string originatingFacility)
        {
            if (ThrowOnNextMerged)
            {
                ThrowOnNextMerged = false;
                throw new InvalidOperationException("Spy: simulated federation outage on merged announce.");
            }
            MergedAnnouncements.Add((sourceIcn, targetIcn, originatingFacility));
            return Task.CompletedTask;
        }
    }
}
