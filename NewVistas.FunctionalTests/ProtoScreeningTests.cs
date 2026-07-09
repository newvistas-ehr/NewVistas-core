// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Integration tests for the screening worker (<see cref="IProtoConditionScreeningGrain"/>) and the
/// explicit sweep (<see cref="IProtoSweepGrain"/>): snapshot assembly from the read models, the
/// preview-vs-record split, and targeted-list re-clustering.
/// </summary>
[TestFixture]
public class ProtoScreeningTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private const string Epi = "EPI-TESTER";
    private const string Anosmia = "44169009";

    private async Task<(IProtoConditionGrain proto, string id)> ActiveAnosmiaProtoAsync()
    {
        string id = Guid.NewGuid().ToString();
        IProtoConditionGrain proto = _cluster.GrainFactory.GetGrain<IProtoConditionGrain>($"PROTO:{id}");
        await proto.CreateAsync("Anosmia cluster", "smell-loss predominant", Epi);
        await proto.AddOrUpdateFeatureAsync(new ProtoFeature
        {
            FeatureId = "anosmia",
            Kind = ProtoFeatureKind.Symptom,
            Display = "Loss of smell",
            Code = Anosmia,
            Operator = ProtoFeatureOperator.Present,
            Rule = ProtoFeatureRule.Weighted,
            Weight = 1
        }, Epi);
        await proto.SetMatchThresholdAsync(0.5, Epi);
        await proto.ActivateAsync(Epi);
        return (proto, id);
    }

    private async Task GiveAnosmiaAsync(string patientId)
    {
        await _cluster.GrainFactory.GetGrain<IPatientSymptomGrain>($"SYMPTOMS:{patientId}")
            .RecordObservationsAsync(new()
            {
                new SymptomObservation { Code = Anosmia, Presence = SymptomPresence.Present, Source = SymptomSource.Survey, RecordedBy = "N1" }
            });
    }

    private IProtoConditionScreeningGrain Screen(string patientId) =>
        _cluster.GrainFactory.GetGrain<IProtoConditionScreeningGrain>($"PROTO-SCREEN:{patientId}");

    [Test]
    public async Task Snapshot_IncludesRecordedSymptom()
    {
        string patient = $"SCR-{Guid.NewGuid()}";
        await GiveAnosmiaAsync(patient);

        PatientFeatureSnapshot snap = await Screen(patient).AssembleSnapshotAsync();

        Assert.That(snap.Symptoms.ContainsKey(Anosmia), Is.True);
        Assert.That(snap.Symptoms[Anosmia], Is.EqualTo(SymptomPresence.Present));
    }

    [Test]
    public async Task EvaluateAndRecord_MatchingPatient_BecomesCandidate()
    {
        (IProtoConditionGrain proto, string id) = await ActiveAnosmiaProtoAsync();
        string patient = $"SCR-{Guid.NewGuid()}";
        await GiveAnosmiaAsync(patient);

        ProtoMatchResult result = await Screen(patient).EvaluateAndRecordAsync(id);

        Assert.That(result.Matches, Is.True);
        List<ProtoMember> candidates = await proto.GetMembersByStatusAsync(ProtoMemberStatus.Candidate);
        Assert.That(candidates.Select(m => m.PatientId), Does.Contain(patient));
    }

    [Test]
    public async Task Evaluate_Preview_DoesNotRecord()
    {
        (IProtoConditionGrain proto, string id) = await ActiveAnosmiaProtoAsync();
        string patient = $"SCR-{Guid.NewGuid()}";
        await GiveAnosmiaAsync(patient);

        ProtoMatchResult result = await Screen(patient).EvaluateAsync(id);

        Assert.That(result.Matches, Is.True, "preview still computes the match");
        Assert.That(await proto.GetMembersByStatusAsync(ProtoMemberStatus.Candidate), Is.Empty,
            "preview must not record membership");
    }

    [Test]
    public async Task Sweep_TargetedList_RecordsOnlyMatchingPatients()
    {
        (IProtoConditionGrain proto, string id) = await ActiveAnosmiaProtoAsync();
        string a = $"SCR-{Guid.NewGuid()}";
        string b = $"SCR-{Guid.NewGuid()}";
        string noMatch = $"SCR-{Guid.NewGuid()}";
        await GiveAnosmiaAsync(a);
        await GiveAnosmiaAsync(b);
        // noMatch has no symptoms recorded → anosmia "not asked" → score 0.

        ProtoSweepRun run = await _cluster.GrainFactory.GetGrain<IProtoSweepGrain>("PROTO-SWEEP")
            .SweepPatientsAsync(id, new() { a, b, noMatch }, Epi);

        Assert.That(run.PatientsScreened, Is.EqualTo(3));
        Assert.That(run.MatchedCount, Is.EqualTo(2));
        Assert.That(run.TargetedMode, Is.True);

        List<string> candidates = (await proto.GetMembersByStatusAsync(ProtoMemberStatus.Candidate))
            .Select(m => m.PatientId).ToList();
        Assert.That(candidates, Does.Contain(a));
        Assert.That(candidates, Does.Contain(b));
        Assert.That(candidates, Does.Not.Contain(noMatch));
    }
}
