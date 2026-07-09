// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Integration test for the analytics worker (<see cref="IProtoAnalyticsGrain"/>): it reads a
/// proto's confirmed cohort and assembles symptom backgrounds from the live cohort shards. Verdicts
/// themselves are covered exhaustively by the pure unit tests; this proves the wiring and the
/// assessed-denominator counts flow through end to end.
/// </summary>
[TestFixture]
public class ProtoAnalyticsGrainTests
{
    private TestCluster _cluster = null!;

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private const string Epi = "EPI-TESTER";
    private const string Anosmia = "44169009";

    [Test]
    public async Task Analyze_ReflectsConfirmedCohort_WithAssessedCounts()
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
        await proto.ActivateAsync(Epi);

        // Three patients with anosmia → screen → confirm.
        for (int i = 0; i < 3; i++)
        {
            string patient = $"ANL-{Guid.NewGuid()}";
            await _cluster.GrainFactory.GetGrain<IPatientSymptomGrain>($"SYMPTOMS:{patient}")
                .RecordObservationsAsync(new()
                {
                    new SymptomObservation { Code = Anosmia, Presence = SymptomPresence.Present, Source = SymptomSource.Survey, RecordedBy = "N1" }
                });
            await _cluster.GrainFactory.GetGrain<IProtoConditionScreeningGrain>($"PROTO-SCREEN:{patient}")
                .EvaluateAndRecordAsync(id);
            await proto.ConfirmMemberAsync(patient, Epi);
        }

        ProtoAnalyticsReport report = await _cluster.GrainFactory
            .GetGrain<IProtoAnalyticsGrain>($"PROTO-ANALYTICS:{id}").AnalyzeAsync();

        Assert.That(report.ConfirmedCount, Is.EqualTo(3));
        FeatureSignal anosmia = report.Signals.Single(s => s.FeatureId == "anosmia");
        Assert.That(anosmia.ClusterPresent, Is.EqualTo(3));
        Assert.That(anosmia.ClusterAssessed, Is.EqualTo(3));
        Assert.That(anosmia.BackgroundSource, Is.Not.Empty);
    }
}
