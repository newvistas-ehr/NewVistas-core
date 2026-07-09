// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for the deterministic proto-condition matcher. Pure logic — no cluster. Covers the
/// fixed-denominator scoring, trinary symptom semantics, hard include/exclude rules, numeric lab/
/// vital comparison, recency windows, and the "never guess — report not-assessed" contract.
/// </summary>
[TestFixture]
public class ProtoConditionMatcherTests
{
    private const string Anosmia = "44169009";
    private const string Cough = "49727002";
    private const string SpO2Loinc = "59408-5";

    private static ProtoFeature F(string id, ProtoFeatureKind kind, string code,
        ProtoFeatureOperator op, ProtoFeatureRule rule = ProtoFeatureRule.Weighted,
        double weight = 1.0, string? value = null, string? value2 = null, int? recencyDays = null) => new()
    {
        FeatureId = id,
        Kind = kind,
        Display = id,
        Code = code,
        Operator = op,
        Rule = rule,
        Weight = weight,
        Value = value,
        Value2 = value2,
        RecencyWindowDays = recencyDays
    };

    private static ProtoConditionState Proto(double threshold, params ProtoFeature[] features) => new()
    {
        ProtoConditionId = "PC1",
        DefinitionVersion = 3,
        MatchThreshold = threshold,
        Features = features.ToList()
    };

    private static PatientFeatureSnapshot Snapshot(Action<PatientFeatureSnapshot> configure)
    {
        var s = new PatientFeatureSnapshot { PatientId = "P1", AssembledAt = new DateTime(2026, 7, 9) };
        configure(s);
        return s;
    }

    [Test]
    public void FixedDenominator_ScoreIsSatisfiedWeightOverAllWeight()
    {
        ProtoConditionState proto = Proto(0.5,
            F("s1", ProtoFeatureKind.Symptom, Anosmia, ProtoFeatureOperator.Present, weight: 3),
            F("s2", ProtoFeatureKind.Symptom, Cough, ProtoFeatureOperator.Present, weight: 1));

        PatientFeatureSnapshot s = Snapshot(x =>
        {
            x.Symptoms[Anosmia] = SymptomPresence.Present;  // satisfied (weight 3)
            x.Symptoms[Cough] = SymptomPresence.Absent;     // assessed, not satisfied (weight 1)
        });

        ProtoMatchResult r = ProtoConditionMatcher.Evaluate(proto, s);
        Assert.That(r.Score, Is.EqualTo(0.75).Within(1e-9)); // 3 / (3+1)
        Assert.That(r.Matches, Is.True);
    }

    [Test]
    public void UnassessedFeature_CountsAsZero_ButStaysInDenominator()
    {
        ProtoConditionState proto = Proto(0.5,
            F("s1", ProtoFeatureKind.Symptom, Anosmia, ProtoFeatureOperator.Present, weight: 1),
            F("s2", ProtoFeatureKind.Symptom, Cough, ProtoFeatureOperator.Present, weight: 1));

        // Only anosmia was asked; cough was never asked — it must still be in the denominator.
        PatientFeatureSnapshot s = Snapshot(x => x.Symptoms[Anosmia] = SymptomPresence.Present);

        ProtoMatchResult r = ProtoConditionMatcher.Evaluate(proto, s);
        Assert.That(r.Score, Is.EqualTo(0.5).Within(1e-9)); // 1 / (1+1), NOT 1/1
        FeatureContribution cough = r.Contributions.Single(c => c.FeatureId == "s2");
        Assert.That(cough.Assessed, Is.False);
        Assert.That(cough.Evidence, Is.EqualTo("not asked"));
    }

    [Test]
    public void HardExclude_Disqualifies_RegardlessOfScore()
    {
        ProtoConditionState proto = Proto(0.1,
            F("s1", ProtoFeatureKind.Symptom, Anosmia, ProtoFeatureOperator.Present, weight: 1),
            F("x", ProtoFeatureKind.Diagnosis, "J45.909", ProtoFeatureOperator.Present, ProtoFeatureRule.HardExclude));

        PatientFeatureSnapshot s = Snapshot(x =>
        {
            x.Symptoms[Anosmia] = SymptomPresence.Present;
            x.Problems.Add("J45.909"); // the disqualifier
        });

        ProtoMatchResult r = ProtoConditionMatcher.Evaluate(proto, s);
        Assert.That(r.HardExcluded, Is.True);
        Assert.That(r.Matches, Is.False);
    }

    [Test]
    public void HardInclude_MustBeSatisfied_ToMatch()
    {
        ProtoConditionState proto = Proto(0.1,
            F("must", ProtoFeatureKind.Diagnosis, "J96.0*", ProtoFeatureOperator.Present, ProtoFeatureRule.HardInclude),
            F("s1", ProtoFeatureKind.Symptom, Anosmia, ProtoFeatureOperator.Present, weight: 1));

        PatientFeatureSnapshot noRespFailure = Snapshot(x => x.Symptoms[Anosmia] = SymptomPresence.Present);
        Assert.That(ProtoConditionMatcher.Evaluate(proto, noRespFailure).Matches, Is.False);

        // Wildcard J96.0* matches J96.01 on the problem list → hard include satisfied.
        PatientFeatureSnapshot withRespFailure = Snapshot(x =>
        {
            x.Symptoms[Anosmia] = SymptomPresence.Present;
            x.Problems.Add("J96.01");
        });
        Assert.That(ProtoConditionMatcher.Evaluate(proto, withRespFailure).Matches, Is.True);
    }

    [Test]
    public void NumericLab_ComparesParsedValue()
    {
        ProtoConditionState proto = Proto(0.5,
            F("spo2", ProtoFeatureKind.LabResult, SpO2Loinc, ProtoFeatureOperator.LessThan, weight: 1, value: "92"));

        PatientFeatureSnapshot low = Snapshot(x => x.Labs.Add(new SnapshotLab { Loinc = SpO2Loinc, Value = "88 %", ResultedDate = new DateTime(2026, 7, 8) }));
        Assert.That(ProtoConditionMatcher.Evaluate(proto, low).Matches, Is.True);

        PatientFeatureSnapshot normal = Snapshot(x => x.Labs.Add(new SnapshotLab { Loinc = SpO2Loinc, Value = "97 %", ResultedDate = new DateTime(2026, 7, 8) }));
        Assert.That(ProtoConditionMatcher.Evaluate(proto, normal).Matches, Is.False);
    }

    [Test]
    public void NonNumericValue_ForNumericOperator_IsNotAssessed_NeverGuessed()
    {
        ProtoConditionState proto = Proto(0.5,
            F("spo2", ProtoFeatureKind.LabResult, SpO2Loinc, ProtoFeatureOperator.LessThan, weight: 1, value: "92"));

        PatientFeatureSnapshot s = Snapshot(x => x.Labs.Add(new SnapshotLab { Loinc = SpO2Loinc, Value = "see chart" }));
        ProtoMatchResult r = ProtoConditionMatcher.Evaluate(proto, s);

        FeatureContribution c = r.Contributions.Single();
        Assert.That(c.Assessed, Is.False);
        Assert.That(c.Satisfied, Is.False);
        Assert.That(c.Evidence, Does.Contain("not numeric"));
    }

    [Test]
    public void QualitativeLab_EqualsOperator_MatchesInterpretation()
    {
        ProtoConditionState proto = Proto(0.5,
            F("flu", ProtoFeatureKind.LabResult, "80382-5", ProtoFeatureOperator.Equals, weight: 1, value: "Negative"));

        PatientFeatureSnapshot fluNeg = Snapshot(x => x.Labs.Add(new SnapshotLab { Loinc = "80382-5", Value = "negative" }));
        Assert.That(ProtoConditionMatcher.Evaluate(proto, fluNeg).Matches, Is.True);
    }

    [Test]
    public void StaleResult_OutsideRecencyWindow_IsNotAssessed()
    {
        ProtoConditionState proto = Proto(0.5,
            F("spo2", ProtoFeatureKind.LabResult, SpO2Loinc, ProtoFeatureOperator.LessThan, weight: 1, value: "92", recencyDays: 7));

        // Result is 30 days before the snapshot's AssembledAt — outside the 7-day window.
        PatientFeatureSnapshot s = Snapshot(x => x.Labs.Add(new SnapshotLab { Loinc = SpO2Loinc, Value = "80", ResultedDate = new DateTime(2026, 6, 9) }));
        ProtoMatchResult r = ProtoConditionMatcher.Evaluate(proto, s);

        Assert.That(r.Contributions.Single().Assessed, Is.False);
        Assert.That(r.Matches, Is.False);
    }

    [Test]
    public void Demographic_AgeInRange_And_Sex()
    {
        ProtoConditionState proto = Proto(1.0,
            F("age", ProtoFeatureKind.Demographic, "AGE", ProtoFeatureOperator.InRange, weight: 1, value: "18", value2: "65"),
            F("sex", ProtoFeatureKind.Demographic, "SEX", ProtoFeatureOperator.Equals, weight: 1, value: "M"));

        PatientFeatureSnapshot s = Snapshot(x => { x.Age = 40; x.Sex = "M"; });
        Assert.That(ProtoConditionMatcher.Evaluate(proto, s).Matches, Is.True);

        PatientFeatureSnapshot old = Snapshot(x => { x.Age = 80; x.Sex = "M"; });
        Assert.That(ProtoConditionMatcher.Evaluate(proto, old).Matches, Is.False);
    }

    [Test]
    public void Exposure_FacilityMembership()
    {
        ProtoConditionState proto = Proto(1.0,
            F("fac", ProtoFeatureKind.Exposure, "LAHEY-BURLINGTON", ProtoFeatureOperator.Present, weight: 1));

        PatientFeatureSnapshot s = Snapshot(x => x.Facilities.AddRange(new[] { "500", "LAHEY-BURLINGTON" }));
        Assert.That(ProtoConditionMatcher.Evaluate(proto, s).Matches, Is.True);
    }

    [Test]
    public void EmptyDefinition_MatchesNoOne()
    {
        ProtoConditionState proto = Proto(0.0);
        Assert.That(ProtoConditionMatcher.Evaluate(proto, Snapshot(_ => { })).Matches, Is.False);
    }

    [Test]
    public void EveryFeature_ProducesAContribution_WithEvidence()
    {
        ProtoConditionState proto = Proto(0.5,
            F("s1", ProtoFeatureKind.Symptom, Anosmia, ProtoFeatureOperator.Present, weight: 1),
            F("d1", ProtoFeatureKind.Diagnosis, "U07.1", ProtoFeatureOperator.Present, weight: 1));

        PatientFeatureSnapshot s = Snapshot(x => x.Symptoms[Anosmia] = SymptomPresence.Present);
        ProtoMatchResult r = ProtoConditionMatcher.Evaluate(proto, s);

        Assert.That(r.Contributions, Has.Count.EqualTo(2));
        Assert.That(r.Contributions.All(c => !string.IsNullOrEmpty(c.Evidence)), Is.True);
    }
}
