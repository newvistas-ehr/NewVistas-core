// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Pure-function tests for diagnostic stewardship (ADR-006). These cover the places where being
/// wrong would be invisible: the refinement/correction split, the semantics table, and the
/// normalization that keeps grain keys parseable.
/// </summary>
[TestFixture]
public class DiagnosticStewardshipTests
{
    // ── DiagnosisCodeRelation ───────────────────────────────────────────────

    [Test]
    public void CodeRelation_SameCode_IsConfirmed()
        => Assert.That(DiagnosisCodeRelation.Propose("E11.9", "E119"),
            Is.EqualTo(DiagnosticEpisodeOutcome.Confirmed));

    [Test]
    public void CodeRelation_MoreSpecificWithinCategory_IsRefinedNotRevised()
    {
        // "Diabetes" → "type 2 diabetes" is the workup succeeding, not a clinician being wrong.
        // Counting it as an error would bury the real signal under ordinary good practice.
        Assert.That(DiagnosisCodeRelation.Propose("E11", "E11.9"),
            Is.EqualTo(DiagnosticEpisodeOutcome.Refined));
        Assert.That(DiagnosisCodeRelation.Propose("E11.9", "E11.65"),
            Is.EqualTo(DiagnosticEpisodeOutcome.Refined));
    }

    [Test]
    public void CodeRelation_LateralityAndEncounterSuffix_IsNotARevision()
    {
        // S72.001A → S72.001D is the same fracture at a later encounter. A design that flagged
        // this would report a misdiagnosis every time a patient came back.
        Assert.That(DiagnosisCodeRelation.Propose("S72.001A", "S72.001D"),
            Is.EqualTo(DiagnosticEpisodeOutcome.Refined));
    }

    [Test]
    public void CodeRelation_TypeTwoToTypeOneDiabetes_IsARevision()
        // Same disease family, different disease. The 3-character categories differ (E11 vs E10),
        // which is exactly the case the blunt rule has to get right.
        => Assert.That(DiagnosisCodeRelation.Propose("E11.9", "E10.9"),
            Is.EqualTo(DiagnosticEpisodeOutcome.Revised));

    [Test]
    public void CodeRelation_UtiToSepsis_IsARevision()
        => Assert.That(DiagnosisCodeRelation.Propose("N39.0", "A41.9"),
            Is.EqualTo(DiagnosticEpisodeOutcome.Revised));

    [Test]
    public void CodeRelation_TypingTheFlu_IsRefinementNotError()
    {
        // ICD-10 splits influenza across categories by whether the virus was identified
        // (J11 unidentified → J10 identified). Naming the organism is the workup succeeding.
        // The plain 3-character rule proposes Correction here, which would file every typed
        // flu as a misdiagnosis — hence the agent-identification family table.
        Assert.That(DiagnosisCodeRelation.Propose("J11.1", "J10.1"),
            Is.EqualTo(DiagnosticEpisodeOutcome.Refined));
        Assert.That(DiagnosisCodeRelation.ProposeReason("J11.1", "J10.1"),
            Is.EqualTo(RevisionReason.Refinement));
    }

    [Test]
    public void CodeRelation_NamingTheOrganism_IsRefinementAcrossFamilies()
    {
        // Sepsis growing an organism, and pneumonia getting a causative agent.
        Assert.That(DiagnosisCodeRelation.Propose("A41.9", "A40.1"),
            Is.EqualTo(DiagnosticEpisodeOutcome.Refined));
        Assert.That(DiagnosisCodeRelation.Propose("J18.9", "J13"),
            Is.EqualTo(DiagnosticEpisodeOutcome.Refined));
    }

    [Test]
    public void CodeRelation_DifferentDiseasesStayRevised_EvenNearAFamily()
    {
        // Type 1 vs type 2 diabetes must NEVER be grouped — different diseases, and confusing
        // them is a real error the blunt rule already catches.
        Assert.That(DiagnosisCodeRelation.Propose("E11.9", "E10.9"),
            Is.EqualTo(DiagnosticEpisodeOutcome.Revised));
        // Influenza → viral pneumonia crosses families and stays a correction. This is the
        // early-COVID signal path; grouping it away would have silenced it.
        Assert.That(DiagnosisCodeRelation.Propose("J11.1", "J12.9"),
            Is.EqualTo(DiagnosticEpisodeOutcome.Revised));
    }

    [Test]
    public void CodeRelation_RotatorCuffToAmputation_IsACorrection()
        // The clinician's second worked example: a completely different cause of the pain.
        => Assert.That(DiagnosisCodeRelation.ProposeReason("M75.100", "S98.011A"),
            Is.EqualTo(RevisionReason.Correction));

    [Test]
    public void CodeRelation_LessSpecific_IsBroadened()
        => Assert.That(DiagnosisCodeRelation.Propose("E11.9", "E11"),
            Is.EqualTo(DiagnosticEpisodeOutcome.Broadened));

    [Test]
    public void CodeRelation_MissingCode_StaysOpenRatherThanGuessing()
    {
        Assert.That(DiagnosisCodeRelation.Propose(null, "A41.9"),
            Is.EqualTo(DiagnosticEpisodeOutcome.Open));
        Assert.That(DiagnosisCodeRelation.Propose("N39.0", ""),
            Is.EqualTo(DiagnosticEpisodeOutcome.Open));
    }

    [Test]
    public void CodeRelation_ProposedReasonMirrorsProposedOutcome()
    {
        Assert.That(DiagnosisCodeRelation.ProposeReason("E11", "E11.9"),
            Is.EqualTo(RevisionReason.Refinement));
        Assert.That(DiagnosisCodeRelation.ProposeReason("N39.0", "A41.9"),
            Is.EqualTo(RevisionReason.Correction));
    }

    [Test]
    public void Normalize_StripsPunctuationSoShardKeysStayParseable()
    {
        // Shard keys are colon-delimited and split with a plain Split(':'). Any punctuation
        // surviving into the key would make it ambiguous.
        Assert.That(DiagnosisCodeRelation.Normalize("e11.9"), Is.EqualTo("E119"));
        Assert.That(DiagnosisCodeRelation.Normalize("  M54.5 "), Is.EqualTo("M545"));
        Assert.That(DiagnosisCodeRelation.Normalize(null), Is.EqualTo(string.Empty));
    }

    [Test]
    public void Category3_TakesFirstThreeCharacters()
    {
        Assert.That(DiagnosisCodeRelation.Category3("E11.65"), Is.EqualTo("E11"));
        Assert.That(DiagnosisCodeRelation.Category3("A4"), Is.EqualTo("A4"));
    }

    [Test]
    public void IsUnspecified_DetectsNosCodesOnly()
    {
        // A rising rate of revisions terminating in NOS codes is the strongest available signal
        // that clinicians are systematically failing to reach a diagnosis at all.
        Assert.That(DiagnosisCodeRelation.IsUnspecified("J18.9"), Is.True);
        Assert.That(DiagnosisCodeRelation.IsUnspecified("R50.9"), Is.True);
        Assert.That(DiagnosisCodeRelation.IsUnspecified("E11.65"), Is.False);
        Assert.That(DiagnosisCodeRelation.IsUnspecified("A41"), Is.False, "3-char codes are categories, not NOS forms");
    }

    // ── RevisionSemantics ───────────────────────────────────────────────────

    [Test]
    public void Semantics_RefinementLeavesThePriorAssertionTrue()
        => Assert.That(RevisionSemantics.PriorAssertionRemainsTrue(RevisionReason.Refinement), Is.True);

    [Test]
    public void Semantics_CorrectionRevokesThePriorAssertion()
        => Assert.That(RevisionSemantics.PriorAssertionRemainsTrue(RevisionReason.Correction), Is.False);

    [Test]
    public void Semantics_UnspecifiedIsNullSoTheSystemNeverGuesses()
        // The unknown bucket must stay reportable. Folding it into either answer would invent a
        // clinical judgement nobody made.
        => Assert.That(RevisionSemantics.PriorAssertionRemainsTrue(RevisionReason.Unspecified), Is.Null);

    [Test]
    public void Semantics_OnlyCorrectionCountsAsDiagnosticError()
    {
        Assert.That(RevisionSemantics.CountsAsDiagnosticError(RevisionReason.Correction), Is.True);
        foreach (RevisionReason r in new[]
        {
            RevisionReason.Refinement, RevisionReason.Progression, RevisionReason.Resolution,
            RevisionReason.Recode, RevisionReason.Amendment, RevisionReason.Reclassification,
            RevisionReason.Duplicate, RevisionReason.EnteredInError, RevisionReason.Unspecified
        })
        {
            Assert.That(RevisionSemantics.CountsAsDiagnosticError(r), Is.False, $"{r} must not count");
        }
    }

    [Test]
    public void Semantics_RecodeIsNotAnError()
    {
        // When U07.1 shipped, every prior B34.2 row needed remapping. If bulk recode counted as
        // correction the shard would learn that B34.2 is wrong 100% of the time.
        Assert.That(RevisionSemantics.CountsAsDiagnosticError(RevisionReason.Recode), Is.False);
        Assert.That(RevisionSemantics.PriorAssertionRemainsTrue(RevisionReason.Recode), Is.True);
    }

    // ── Evidence ────────────────────────────────────────────────────────────

    [Test]
    public void Evidence_DefaultPolarityIsNotAssessed()
        // A default-constructed ref must never claim a finding.
        => Assert.That(new EvidenceRef().Polarity, Is.EqualTo(EvidencePolarity.NotAssessed));

    [Test]
    public void Evidence_NotAssessedIsDistinctFromAbsent()
    {
        // "We looked and found nothing" versus "we never looked" are opposite clinical signals.
        var refuted = new EvidenceRef { Kind = EvidenceKind.LabResult, Code = "5000", Polarity = EvidencePolarity.Refutes };
        var unchecked_ = new EvidenceRef { Kind = EvidenceKind.LabResult, Code = "5000", Polarity = EvidencePolarity.NotAssessed };
        Assert.That(refuted.Canonicalize(), Is.Not.EqualTo(unchecked_.Canonicalize()));
    }

    [Test]
    public void Evidence_CanonicalListSurvivesSeparatorsInFreeText()
    {
        // Refs are hashed before joining precisely so free text containing the separators cannot
        // shift field boundaries and make two different lists canonicalize identically.
        var a = new List<EvidenceRef> { new() { Display = "a|b", Note = "c^d" } };
        var b = new List<EvidenceRef> { new() { Display = "a", Note = "b^c^d" } };
        Assert.That(EvidenceRef.CanonicalizeList(a), Is.Not.EqualTo(EvidenceRef.CanonicalizeList(b)));
    }

    [Test]
    public void Evidence_DedupeKeyIgnoresDisplayAndValue()
    {
        var first = new EvidenceRef { Kind = EvidenceKind.LabResult, SourceId = "LAB-1", Code = "33959-8", Display = "CTX" };
        var second = new EvidenceRef { Kind = EvidenceKind.LabResult, SourceId = "LAB-1", Code = "33959-8", Display = "C-telopeptide", ObservedValue = "346" };
        Assert.That(first.DedupeKey(), Is.EqualTo(second.DedupeKey()));
    }

    // ── Curated baseline ────────────────────────────────────────────────────

    [Test]
    public void Baseline_CriticalPairingsExistForTheBigThree()
    {
        (List<DiagnosisAlternative> alts, List<DiagnosticTestSuggestion> tests) =
            DiagnosticRevisionCatalog.GetBaseline("R42.0");

        Assert.That(alts, Is.Not.Empty, "dizziness must carry a stroke pairing");
        Assert.That(alts.Any(a => a.Harm == DiagnosticHarmIfMissed.Critical), Is.True);
        Assert.That(tests.Any(t => t.TestKey == "E:HINTS"), Is.True);
    }

    [Test]
    public void Baseline_EachSuggestedTestAppearsOnce()
    {
        // Two keys sharing one label rendered as two identical rows in the UI. Attention is the
        // scarce resource on this surface, so a duplicated row is a real defect.
        foreach (string code in new[] { "R42.0", "R07.9", "N39.0", "M25.561", "K21.9" })
        {
            (_, List<DiagnosticTestSuggestion> tests) = DiagnosticRevisionCatalog.GetBaseline(code);
            Assert.That(tests.Select(t => t.TestKey).Distinct().Count(), Is.EqualTo(tests.Count),
                $"{code} produced duplicate test keys");
            Assert.That(tests.Select(t => t.Display).Distinct().Count(), Is.EqualTo(tests.Count),
                $"{code} produced duplicate test labels");
        }
    }

    [Test]
    public void Baseline_CarriesNoFabricatedFrequencies()
    {
        // The baseline states that a pairing exists and is dangerous — never how often it
        // happens here. A hand-authored rate would be fabricated clinical evidence.
        (List<DiagnosisAlternative> alts, _) = DiagnosticRevisionCatalog.GetBaseline("N39.0");
        Assert.That(alts, Is.Not.Empty);
        Assert.That(alts.All(a => a.Count == 0 && a.OutOf == 0), Is.True);
        Assert.That(alts.All(a => !string.IsNullOrWhiteSpace(a.Citation)), Is.True);
    }

    [Test]
    public void Baseline_UncuratedCodeReturnsEmpty()
    {
        (List<DiagnosisAlternative> alts, List<DiagnosticTestSuggestion> tests) =
            DiagnosticRevisionCatalog.GetBaseline("Z00.00");
        Assert.That(alts, Is.Empty);
        Assert.That(tests, Is.Empty);
    }

    [Test]
    public void Merge_CriticalBaselineSurvivesWithNoLocalData()
    {
        // The min-N floors gate the learned percentage, never the curated arrow. Dizziness →
        // posterior stroke will never reach n = 20 at one clinic, and that is exactly where
        // silence would be most harmful.
        var advisory = new DiagnosisRevisionAdvisory
        {
            WorkingCode = "R420",
            Band = RevisionRateBand.Insufficient,
            IsColdStart = true
        };

        DiagnosisRevisionAdvisory merged =
            DiagnosticRevisionCatalog.Merge(advisory, "R42.0", DateTime.UtcNow);

        Assert.That(merged.Alternatives.Any(a => a.Harm == DiagnosticHarmIfMissed.Critical), Is.True);
        Assert.That(merged.RevisionRate, Is.Null, "no rate may appear when the band is Insufficient");
    }

    [Test]
    public void Merge_SortsLethalityAboveFrequency()
    {
        var advisory = new DiagnosisRevisionAdvisory
        {
            WorkingCode = "N390",
            Alternatives = new List<DiagnosisAlternative>
            {
                new() { Code = "Z000", Display = "Something common and harmless", Count = 99, OutOf = 100 }
            }
        };

        DiagnosisRevisionAdvisory merged =
            DiagnosticRevisionCatalog.Merge(advisory, "N39.0", DateTime.UtcNow);

        // Sepsis (Critical, count 0) must outrank the common benign alternative. Rarity must
        // never bury lethality.
        Assert.That(merged.Alternatives.First().Harm, Is.EqualTo(DiagnosticHarmIfMissed.Critical));
    }

    // ── Thresholds ──────────────────────────────────────────────────────────

    [Test]
    public void Thresholds_ShareTheProtoAnalyticsLiftDefinitions()
    {
        // Reused, not copied — one meaning of "signal" site-wide.
        Assert.That(DiagnosticStewardshipThresholds.SignalLift,
            Is.EqualTo(ProtoConditionAnalytics.SignalLift));
        Assert.That(DiagnosticStewardshipThresholds.NoiseLiftCeiling,
            Is.EqualTo(ProtoConditionAnalytics.NoiseLiftCeiling));
    }

    [Test]
    public void Thresholds_RateFloorIsStricterThanTheClusterFloor()
        // A noisy cluster signal makes an epidemiologist look twice; a noisy revision rate makes
        // a clinician doubt a correct diagnosis. Higher stakes, higher floor.
        => Assert.That(DiagnosticStewardshipThresholds.MinAdjudicatedForRate, Is.GreaterThan(10));
}
