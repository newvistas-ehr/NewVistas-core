// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Phase E workflow-hook tests: the cover-sheet precaution banner (both the classic and specialty
/// cover sheets; confirmed-only; flag-gated) and the post-promotion problem-list migration with a
/// source citation. NonParallelizable because it toggles the global EMERGING_CONDITIONS feature.
/// </summary>
[TestFixture, NonParallelizable]
public class EmergingConditionWorkflowTests
{
    private TestCluster _cluster = null!;
    private const string Feature = "EMERGING_CONDITIONS";
    private const string Epi = "QM3";
    private const string Anosmia = "44169009";

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private ISiteParametersGrain SiteParams() => _cluster.GrainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");

    [SetUp]
    public async Task SetUp() => await SiteParams().EnableFeatureAsync(Feature);

    [TearDown]
    public async Task TearDown() => await SiteParams().EnableFeatureAsync(Feature); // leave the flag on for other fixtures

    private IPatientWorkflowGrain Wf(string patientId) => _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    private async Task<string> ActiveDropletProtoAsync()
    {
        string id = Guid.NewGuid().ToString();
        IProtoConditionGrain proto = _cluster.GrainFactory.GetGrain<IProtoConditionGrain>($"PROTO:{id}");
        await proto.CreateAsync("Novel respiratory cluster", "anosmia-predominant", Epi);
        await proto.AddOrUpdateFeatureAsync(new ProtoFeature
        {
            FeatureId = "anosmia", Kind = ProtoFeatureKind.Symptom, Display = "Loss of smell",
            Code = Anosmia, Operator = ProtoFeatureOperator.Present, Rule = ProtoFeatureRule.Weighted, Weight = 1
        }, Epi);
        await proto.ActivateAsync(Epi);
        await proto.SetGuidanceAsync(BedIsolationType.Droplet, "surgical mask + eye protection", new(), Epi);
        return id;
    }

    [Test]
    public async Task ConfirmedMember_ShowsBanner_OnBothCoverSheets()
    {
        string id = await ActiveDropletProtoAsync();
        string patient = $"ECW-{Guid.NewGuid()}";

        await Wf(patient).SuggestForProtoConditionAsync(id, Epi);
        await Wf(patient).ConfirmProtoMembershipAsync(id, Epi);

        CoverSheetState classic = await Wf(patient).GetCoverSheetAsync();
        Assert.That(classic.PrecautionBanners.Select(b => b.ProtoConditionId), Does.Contain(id));
        PrecautionBanner banner = classic.PrecautionBanners.Single(b => b.ProtoConditionId == id);
        Assert.That(banner.Isolation, Is.EqualTo(BedIsolationType.Droplet));
        Assert.That(banner.Message, Does.Contain("Droplet"));

        SpecialtyCoverSheet specialty = await Wf(patient).GetSpecialtyCoverSheetAsync(null, null);
        Assert.That(specialty.PrecautionBanners.Select(b => b.ProtoConditionId), Does.Contain(id));
    }

    [Test]
    public async Task CandidateOnly_ShowsNoBanner()
    {
        string id = await ActiveDropletProtoAsync();
        string patient = $"ECW-{Guid.NewGuid()}";

        // Suggested (candidate) but NOT confirmed — a candidate is not a clinical assertion.
        await Wf(patient).SuggestForProtoConditionAsync(id, Epi);

        CoverSheetState classic = await Wf(patient).GetCoverSheetAsync();
        Assert.That(classic.PrecautionBanners.Any(b => b.ProtoConditionId == id), Is.False);
    }

    [Test]
    public async Task FlagOff_ShowsNoBanner()
    {
        string id = await ActiveDropletProtoAsync();
        string patient = $"ECW-{Guid.NewGuid()}";
        await Wf(patient).SuggestForProtoConditionAsync(id, Epi);
        await Wf(patient).ConfirmProtoMembershipAsync(id, Epi);

        await SiteParams().DisableFeatureAsync(Feature);
        try
        {
            CoverSheetState classic = await Wf(patient).GetCoverSheetAsync();
            Assert.That(classic.PrecautionBanners, Is.Empty);
        }
        finally
        {
            await SiteParams().EnableFeatureAsync(Feature);
        }
    }

    [Test]
    public async Task Migrate_AddsPromotedProblem_WithCitation()
    {
        string id = await ActiveDropletProtoAsync();
        string patient = $"ECW-{Guid.NewGuid()}";
        await Wf(patient).SuggestForProtoConditionAsync(id, Epi);
        await Wf(patient).ConfirmProtoMembershipAsync(id, Epi);

        IProtoConditionGrain proto = _cluster.GrainFactory.GetGrain<IProtoConditionGrain>($"PROTO:{id}");
        await proto.PromoteAsync("COVID-19", new() { "U07.1" }, "840539006", new DateTime(2020, 4, 1), new() { "US", "MA" }, "", Epi);

        string problemId = await Wf(patient).MigratePromotedProtoProblemAsync(id, Epi);
        Assert.That(problemId, Is.Not.Empty);

        List<ProblemSummary> problems = await Wf(patient).GetActiveProblemsAsync();
        ProblemSummary added = problems.Single(p => p.DiagnosisCode == "U07.1");
        Assert.That(added.Diagnosis, Is.EqualTo("COVID-19"));

        // The proto's migration log now marks this member migrated with the new problem id.
        ProtoConditionState state = await proto.GetAsync();
        ProtoMigrationEntry entry = state.MigrationLog.Single(e => e.PatientId == patient);
        Assert.That(entry.Status, Is.EqualTo(ProtoMigrationStatus.Migrated));
        Assert.That(entry.ProblemId, Is.EqualTo(problemId));
    }
}
