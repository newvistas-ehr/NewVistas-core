// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Core tests for the <see cref="IProtoConditionGrain"/> — lifecycle guards, definition versioning,
/// the membership invariants (Excluded never resurrected, Confirmed never silently reversed,
/// machine-vs-human candidate persistence), the confirmed-cohort shard, the count-threshold alert
/// (fires exactly once for a static cohort), and the post-promotion freeze + eCR emission.
/// The SharedCluster has no auth filter, so these exercise logic not the security keys.
/// </summary>
[TestFixture]
public class ProtoConditionCoreTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private const string Epi = "EPI-TESTER";

    private IProtoConditionGrain NewProto(out string id)
    {
        id = Guid.NewGuid().ToString();
        return _cluster.GrainFactory.GetGrain<IProtoConditionGrain>($"PROTO:{id}");
    }

    private static ProtoFeature Feature(string featureId, ProtoFeatureKind kind, string code,
        ProtoFeatureOperator op = ProtoFeatureOperator.Present, ProtoFeatureRule rule = ProtoFeatureRule.Weighted,
        double weight = 1.0) => new()
        {
            FeatureId = featureId,
            Kind = kind,
            Display = featureId,
            Code = code,
            Operator = op,
            Rule = rule,
            Weight = weight
        };

    private static ProtoMatchResult Match(string patientId, string protoId, int version, double score, bool matches) => new()
    {
        PatientId = patientId,
        ProtoConditionId = protoId,
        DefinitionVersion = version,
        Score = score,
        Matches = matches
    };

    private async Task<(IProtoConditionGrain proto, string id)> ActiveProtoAsync()
    {
        IProtoConditionGrain proto = NewProto(out string id);
        await proto.CreateAsync("Novel respiratory cluster", "anosmia-predominant", Epi);
        await proto.AddOrUpdateFeatureAsync(Feature("anosmia", ProtoFeatureKind.Symptom, "44169009"), Epi);
        await proto.ActivateAsync(Epi);
        return (proto, id);
    }

    [Test]
    public async Task Definition_Edit_BumpsVersion()
    {
        IProtoConditionGrain proto = NewProto(out _);
        await proto.CreateAsync("cluster", "desc", Epi);
        Assert.That((await proto.GetAsync()).DefinitionVersion, Is.EqualTo(1));

        await proto.AddOrUpdateFeatureAsync(Feature("f1", ProtoFeatureKind.Symptom, "44169009"), Epi);
        await proto.SetMatchThresholdAsync(0.6, Epi);

        Assert.That((await proto.GetAsync()).DefinitionVersion, Is.EqualTo(3));
    }

    [Test]
    public async Task Guidance_Change_DoesNotBumpVersion()
    {
        IProtoConditionGrain proto = NewProto(out _);
        await proto.CreateAsync("cluster", "desc", Epi);
        int v = (await proto.GetAsync()).DefinitionVersion;

        await proto.SetGuidanceAsync(BedIsolationType.Droplet, "surgical mask", new(), Epi);

        ProtoConditionState state = await proto.GetAsync();
        Assert.That(state.DefinitionVersion, Is.EqualTo(v));
        Assert.That(state.IsolationRecommendation, Is.EqualTo(BedIsolationType.Droplet));
    }

    [Test]
    public async Task Upsert_Match_CreatesMachineCandidate_AndIndexCounts()
    {
        (IProtoConditionGrain proto, string id) = await ActiveProtoAsync();
        int v = (await proto.GetAsync()).DefinitionVersion;
        string patient = $"P-{Guid.NewGuid()}";

        await proto.UpsertEvaluationAsync(Match(patient, id, v, 0.9, matches: true));

        List<ProtoMember> candidates = await proto.GetMembersByStatusAsync(ProtoMemberStatus.Candidate);
        Assert.That(candidates.Select(m => m.PatientId), Does.Contain(patient));
        Assert.That(candidates.Single(m => m.PatientId == patient).Source, Is.EqualTo(ProtoMemberSource.Machine));

        ProtoConditionSummary summary = await proto.GetSummaryAsync();
        Assert.That(summary.CandidateCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Upsert_StaleVersion_IsDropped()
    {
        (IProtoConditionGrain proto, string id) = await ActiveProtoAsync();
        string patient = $"P-{Guid.NewGuid()}";

        // Definition is at v2 (create=1, +feature=2). An evaluation stamped v1 is stale.
        await proto.UpsertEvaluationAsync(Match(patient, id, 1, 0.9, matches: true));

        Assert.That(await proto.GetMembersByStatusAsync(ProtoMemberStatus.Candidate), Is.Empty);
    }

    [Test]
    public async Task Upsert_MachineCandidateStopsMatching_IsRemoved()
    {
        (IProtoConditionGrain proto, string id) = await ActiveProtoAsync();
        int v = (await proto.GetAsync()).DefinitionVersion;
        string patient = $"P-{Guid.NewGuid()}";

        await proto.UpsertEvaluationAsync(Match(patient, id, v, 0.9, matches: true));
        await proto.UpsertEvaluationAsync(Match(patient, id, v, 0.2, matches: false));

        Assert.That(await proto.GetMembersByStatusAsync(ProtoMemberStatus.Candidate), Is.Empty);
    }

    [Test]
    public async Task Suggest_HumanCandidate_PersistsThroughNonMatch()
    {
        (IProtoConditionGrain proto, string id) = await ActiveProtoAsync();
        int v = (await proto.GetAsync()).DefinitionVersion;
        string patient = $"P-{Guid.NewGuid()}";

        await proto.SuggestMemberAsync(patient, "DR-HOUSE");
        await proto.UpsertEvaluationAsync(Match(patient, id, v, 0.1, matches: false));

        List<ProtoMember> candidates = await proto.GetMembersByStatusAsync(ProtoMemberStatus.Candidate);
        ProtoMember? m = candidates.SingleOrDefault(x => x.PatientId == patient);
        Assert.That(m, Is.Not.Null, "human-suggested candidate must persist even when it stops matching");
        Assert.That(m!.Source, Is.EqualTo(ProtoMemberSource.ManualSuggestion));
    }

    [Test]
    public async Task Excluded_IsNeverResurrectedByMachine()
    {
        (IProtoConditionGrain proto, string id) = await ActiveProtoAsync();
        int v = (await proto.GetAsync()).DefinitionVersion;
        string patient = $"P-{Guid.NewGuid()}";

        await proto.UpsertEvaluationAsync(Match(patient, id, v, 0.9, matches: true));
        await proto.ExcludeMemberAsync(patient, Epi, "not this cluster");

        // A later strong machine match must NOT bring the excluded patient back.
        await proto.UpsertEvaluationAsync(Match(patient, id, v, 0.99, matches: true));

        Assert.That(await proto.GetMembersByStatusAsync(ProtoMemberStatus.Candidate), Is.Empty);
        Assert.That(await proto.GetMembersByStatusAsync(ProtoMemberStatus.Excluded),
            Has.Exactly(1).Matches<ProtoMember>(m => m.PatientId == patient));
    }

    [Test]
    public async Task Confirmed_StopsMatching_IsFlaggedNotReversed()
    {
        (IProtoConditionGrain proto, string id) = await ActiveProtoAsync();
        int v = (await proto.GetAsync()).DefinitionVersion;
        string patient = $"P-{Guid.NewGuid()}";

        await proto.UpsertEvaluationAsync(Match(patient, id, v, 0.9, matches: true));
        await proto.ConfirmMemberAsync(patient, Epi);
        await proto.UpsertEvaluationAsync(Match(patient, id, v, 0.1, matches: false));

        List<ProtoMember> confirmed = await proto.GetMembersByStatusAsync(ProtoMemberStatus.Confirmed);
        ProtoMember m = confirmed.Single(x => x.PatientId == patient);
        Assert.That(m.Status, Is.EqualTo(ProtoMemberStatus.Confirmed), "confirmed is never silently reversed");
        Assert.That(m.ReviewFlag, Is.True);
    }

    [Test]
    public async Task Confirm_AddsToCohortShard()
    {
        (IProtoConditionGrain proto, string id) = await ActiveProtoAsync();
        int v = (await proto.GetAsync()).DefinitionVersion;
        string patient = $"P-{Guid.NewGuid()}";

        await proto.UpsertEvaluationAsync(Match(patient, id, v, 0.9, matches: true));
        await proto.ConfirmMemberAsync(patient, Epi);

        IProtoCohortIndexGrain cohort = _cluster.GrainFactory.GetGrain<IProtoCohortIndexGrain>($"PROTO-COHORT:{id}");
        Assert.That(await cohort.ContainsAsync(patient), Is.True);

        // Excluding a confirmed member pulls it back out of the shard.
        await proto.ExcludeMemberAsync(patient, Epi, "reclassified");
        Assert.That(await cohort.ContainsAsync(patient), Is.False);
    }

    [Test]
    public async Task Alert_FiresExactlyOnce_ForStaticCohort()
    {
        (IProtoConditionGrain proto, string id) = await ActiveProtoAsync();
        int v = (await proto.GetAsync()).DefinitionVersion;
        await proto.SetAlertRuleAsync(new ProtoAlertRule
        {
            Threshold = 3,
            Recipients = new() { "QM3" },
            CooldownHours = 24
        }, Epi);

        // Confirm three members — the 3rd crosses the threshold and fires.
        for (int i = 0; i < 3; i++)
        {
            string p = $"P-{Guid.NewGuid()}";
            await proto.UpsertEvaluationAsync(Match(p, id, v, 0.9, matches: true));
            await proto.ConfirmMemberAsync(p, Epi);
        }

        ProtoAlertRule rule = (await proto.GetAsync()).AlertRule!;
        Assert.That(rule.TimesFired, Is.EqualTo(1));
        Assert.That(rule.LastFiredCount, Is.EqualTo(3));

        // A 4th confirm within the cooldown must NOT re-fire (alert-fatigue control).
        string p4 = $"P-{Guid.NewGuid()}";
        await proto.UpsertEvaluationAsync(Match(p4, id, v, 0.9, matches: true));
        await proto.ConfirmMemberAsync(p4, Epi);

        Assert.That((await proto.GetAsync()).AlertRule!.TimesFired, Is.EqualTo(1));
    }

    [Test]
    public async Task Alert_DoesNotFire_BelowThreshold()
    {
        (IProtoConditionGrain proto, string id) = await ActiveProtoAsync();
        int v = (await proto.GetAsync()).DefinitionVersion;
        await proto.SetAlertRuleAsync(new ProtoAlertRule { Threshold = 5, Recipients = new() { "QM3" } }, Epi);

        string p = $"P-{Guid.NewGuid()}";
        await proto.UpsertEvaluationAsync(Match(p, id, v, 0.9, matches: true));
        await proto.ConfirmMemberAsync(p, Epi);

        Assert.That((await proto.GetAsync()).AlertRule!.TimesFired, Is.EqualTo(0));
    }

    [Test]
    public async Task Promote_FreezesDefinition_ExpiresCandidates_EmitsEcrTrigger()
    {
        (IProtoConditionGrain proto, string id) = await ActiveProtoAsync();
        int v = (await proto.GetAsync()).DefinitionVersion;

        string confirmed = $"P-{Guid.NewGuid()}";
        string candidate = $"P-{Guid.NewGuid()}";
        await proto.UpsertEvaluationAsync(Match(confirmed, id, v, 0.9, matches: true));
        await proto.ConfirmMemberAsync(confirmed, Epi);
        await proto.UpsertEvaluationAsync(Match(candidate, id, v, 0.8, matches: true));

        await proto.PromoteAsync("COVID-19", new() { "U07.1" }, "840539006",
            new DateTime(2020, 4, 1), new() { "US", "MA" }, "WHO named it", Epi);

        ProtoConditionState state = await proto.GetAsync();
        Assert.That(state.Status, Is.EqualTo(ProtoConditionStatus.Promoted));
        Assert.That(state.PromotedIcd10Codes, Does.Contain("U07.1"));

        // The leftover candidate expired to Excluded; the confirmed member is on the migration log.
        Assert.That(await proto.GetMembersByStatusAsync(ProtoMemberStatus.Candidate), Is.Empty);
        Assert.That(state.MigrationLog.Select(e => e.PatientId), Does.Contain(confirmed));
        Assert.That(state.MigrationLog.Single(e => e.PatientId == confirmed).Status, Is.EqualTo(ProtoMigrationStatus.Pending));

        // The net closed into the coded pipeline — an eCR trigger now exists for U07.1.
        Assert.That(state.EcrTriggerId, Is.EqualTo($"PROTO-{id}"));
        EcrTriggerState trigger = await _cluster.GrainFactory
            .GetGrain<IEcrTriggerGrain>($"ECR-TRIGGER:PROTO-{id}").GetTriggerAsync();
        Assert.That(trigger.ConditionName, Is.EqualTo("COVID-19"));
        Assert.That(trigger.TriggerCodes.Select(c => c.Code), Does.Contain("U07.1"));
    }

    [Test]
    public async Task Promote_ThenMutate_Throws_ButMigrationStillAllowed()
    {
        (IProtoConditionGrain proto, string id) = await ActiveProtoAsync();
        int v = (await proto.GetAsync()).DefinitionVersion;
        string confirmed = $"P-{Guid.NewGuid()}";
        await proto.UpsertEvaluationAsync(Match(confirmed, id, v, 0.9, matches: true));
        await proto.ConfirmMemberAsync(confirmed, Epi);

        await proto.PromoteAsync("COVID-19", new() { "U07.1" }, "840539006", null, new() { "US" }, "", Epi);

        // Frozen definition — any matching mutator throws.
        Assert.ThrowsAsync<InvalidOperationException>(() =>
            proto.AddOrUpdateFeatureAsync(Feature("f2", ProtoFeatureKind.Symptom, "49727002"), Epi));
        Assert.ThrowsAsync<InvalidOperationException>(() => proto.ConfirmMemberAsync($"P-{Guid.NewGuid()}", Epi));

        // ...but migration bookkeeping (a post-promotion activity) still works.
        await proto.RecordMigrationAsync(confirmed, ProtoMigrationStatus.Migrated, "PROB-123", null, Epi);
        ProtoConditionState state = await proto.GetAsync();
        Assert.That(state.MigrationLog.Single(e => e.PatientId == confirmed).Status, Is.EqualTo(ProtoMigrationStatus.Migrated));
    }
}
