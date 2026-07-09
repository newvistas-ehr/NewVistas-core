// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for the net-closing analytics — the signal-vs-noise lift verdicts, assessed-denominator
/// rates, refinement suggestions, and split co-occurrence. Pure logic — no cluster. These encode the
/// demo's headline result: anosmia is Signal (~8× background), hearing-change is Noise (~1×).
/// </summary>
[TestFixture]
public class ProtoConditionAnalyticsTests
{
    private static ProtoFeature Feature(string id) => new()
    {
        FeatureId = id,
        Kind = ProtoFeatureKind.Symptom,
        Display = id,
        Code = id,
        Operator = ProtoFeatureOperator.Present,
        Rule = ProtoFeatureRule.Weighted,
        Weight = 1
    };

    /// <summary>Build a confirmed cohort where each feature is present/assessed per the supplied counts.</summary>
    private static ProtoConditionState ProtoWithCohort(
        int confirmedCount,
        params (string featureId, int present, int assessed)[] featureCounts)
    {
        var proto = new ProtoConditionState { ProtoConditionId = "PC1", DefinitionVersion = 1 };
        foreach (var fc in featureCounts)
            proto.Features.Add(Feature(fc.featureId));

        for (int i = 0; i < confirmedCount; i++)
        {
            var member = new ProtoMember { PatientId = $"P{i}", Status = ProtoMemberStatus.Confirmed };
            foreach (var fc in featureCounts)
            {
                bool assessed = i < fc.assessed;
                bool present = i < fc.present;
                member.Contributions.Add(new FeatureContribution
                {
                    FeatureId = fc.featureId,
                    Display = fc.featureId,
                    Kind = ProtoFeatureKind.Symptom,
                    Assessed = assessed || present,
                    Satisfied = present,
                    Weight = 1
                });
            }
            proto.Members.Add(member);
        }
        return proto;
    }

    private static BackgroundRate Bg(string featureId, double rate) =>
        new() { FeatureId = featureId, Rate = rate, Source = "test background" };

    [Test]
    public void Anosmia_HighLift_IsSignal_HearingChange_IsNoise()
    {
        // 12 confirmed: anosmia present in 9/12 (~0.75), hearing change present in 1/12 (~0.08).
        ProtoConditionState proto = ProtoWithCohort(12,
            ("anosmia", present: 9, assessed: 12),
            ("hearing", present: 3, assessed: 12));

        // Background: anosmia rare (~0.02 → lift ~37 → Signal), hearing common (~0.08 → lift ~1 → Noise).
        ProtoAnalyticsReport report = ProtoConditionAnalytics.Analyze(proto, new[]
        {
            Bg("anosmia", 0.02),
            Bg("hearing", 0.25)
        });

        FeatureSignal anosmia = report.Signals.Single(s => s.FeatureId == "anosmia");
        FeatureSignal hearing = report.Signals.Single(s => s.FeatureId == "hearing");

        Assert.That(anosmia.Verdict, Is.EqualTo(SignalVerdict.Signal));
        Assert.That(anosmia.Lift, Is.GreaterThan(2.0));
        Assert.That(hearing.Verdict, Is.EqualTo(SignalVerdict.Noise));
    }

    [Test]
    public void ClusterRate_UsesAssessedDenominator_NotConfirmedCount()
    {
        // 10 confirmed, but anosmia was only assessed in 4 of them, present in 3 → rate 3/4, NOT 3/10.
        ProtoConditionState proto = ProtoWithCohort(10, ("anosmia", present: 3, assessed: 4));

        ProtoAnalyticsReport report = ProtoConditionAnalytics.Analyze(proto, new[] { Bg("anosmia", 0.02) });
        FeatureSignal s = report.Signals.Single();

        Assert.That(s.ClusterAssessed, Is.EqualTo(4));
        Assert.That(s.ClusterPresent, Is.EqualTo(3));
        Assert.That(s.ClusterRate, Is.EqualTo(0.75).Within(1e-9));
    }

    [Test]
    public void BelowMinN_IsInsufficient_NotSignal()
    {
        // Only 2 present — below the min-present guard — must not be called a Signal however high the lift.
        ProtoConditionState proto = ProtoWithCohort(4, ("rare", present: 2, assessed: 4));

        ProtoAnalyticsReport report = ProtoConditionAnalytics.Analyze(proto, new[] { Bg("rare", 0.001) });
        Assert.That(report.Signals.Single().Verdict, Is.EqualTo(SignalVerdict.Insufficient));
    }

    [Test]
    public void NoBackground_IsInsufficient_WithNote()
    {
        ProtoConditionState proto = ProtoWithCohort(10, ("dx", present: 8, assessed: 10));

        // No background supplied for "dx".
        ProtoAnalyticsReport report = ProtoConditionAnalytics.Analyze(proto, Array.Empty<BackgroundRate>());
        FeatureSignal s = report.Signals.Single();

        Assert.That(s.Verdict, Is.EqualTo(SignalVerdict.Insufficient));
        Assert.That(s.BackgroundSource, Does.Contain("no background"));
    }

    [Test]
    public void NoiseFeature_ProducesDropSuggestion()
    {
        ProtoConditionState proto = ProtoWithCohort(12,
            ("anosmia", present: 9, assessed: 12),
            ("hearing", present: 3, assessed: 12));

        ProtoAnalyticsReport report = ProtoConditionAnalytics.Analyze(proto, new[]
        {
            Bg("anosmia", 0.02),
            Bg("hearing", 0.25)
        });

        Assert.That(report.Suggestions.Any(x => x.Kind == RefinementKind.DropFeature && x.FeatureId == "hearing"), Is.True);
    }

    [Test]
    public void DemoSeedRatios_Anosmia_IsSignal_Hearing_IsNoise()
    {
        // Locks the EmergingConditionSeed tuning: 9 confirmed members, anosmia present in 6 (0.67),
        // hearing present in 3 (0.33). Backgrounds from the seed's 43-patient assessed population:
        // anosmia 8/43 ≈ 0.186, hearing 12/43 ≈ 0.279.
        ProtoConditionState proto = ProtoWithCohort(9,
            ("anosmia", present: 6, assessed: 9),
            ("hearing", present: 3, assessed: 9));

        ProtoAnalyticsReport report = ProtoConditionAnalytics.Analyze(proto, new[]
        {
            Bg("anosmia", 8.0 / 43.0),
            Bg("hearing", 12.0 / 43.0)
        });

        Assert.That(report.Signals.Single(s => s.FeatureId == "anosmia").Verdict, Is.EqualTo(SignalVerdict.Signal));
        Assert.That(report.Signals.Single(s => s.FeatureId == "hearing").Verdict, Is.EqualTo(SignalVerdict.Noise));
    }

    [Test]
    public void SplitEvidence_AntiCorrelatedSignals_SurfaceAsCoOccurrence()
    {
        // Two signal features that never co-occur: members have respiratory OR cardiac, never both.
        var proto = new ProtoConditionState { ProtoConditionId = "PC1", DefinitionVersion = 1 };
        proto.Features.Add(Feature("resp"));
        proto.Features.Add(Feature("card"));
        for (int i = 0; i < 10; i++)
        {
            bool resp = i < 5;
            var m = new ProtoMember { PatientId = $"P{i}", Status = ProtoMemberStatus.Confirmed };
            m.Contributions.Add(new FeatureContribution { FeatureId = "resp", Assessed = true, Satisfied = resp, Weight = 1 });
            m.Contributions.Add(new FeatureContribution { FeatureId = "card", Assessed = true, Satisfied = !resp, Weight = 1 });
            proto.Members.Add(m);
        }

        ProtoAnalyticsReport report = ProtoConditionAnalytics.Analyze(proto, new[]
        {
            Bg("resp", 0.05),
            Bg("card", 0.05)
        });

        FeatureCoOccurrence pair = report.CoOccurrences.Single();
        Assert.That(pair.BothPresent, Is.EqualTo(0));
        Assert.That(pair.AntiCorrelation, Is.EqualTo(1.0).Within(1e-9)); // perfectly anti-correlated → candidate split
    }
}
