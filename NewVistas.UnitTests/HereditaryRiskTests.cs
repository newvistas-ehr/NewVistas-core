// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for the curated hereditary-risk knowledge base (HereditaryRisk): germline pathogenic
/// variant → hereditary syndrome, and structured family history → referral red-flag patterns.
/// Pure logic — no Orleans cluster needed (the class under test is static).
/// </summary>
[TestFixture]
public class HereditaryRiskTests
{
    private static GeneticVariant Variant(
        string gene,
        VariantClassification classification,
        VariantOrigin origin = VariantOrigin.Germline,
        string hgvsCoding = "c.0A>T") =>
        new()
        {
            Gene = gene,
            Classification = classification,
            Origin = origin,
            HgvsCoding = hgvsCoding
        };

    private static FamilyMemberHistoryEntry Member(
        FamilyRelationship relationship,
        string sex,
        params (string Condition, int? AgeAtDiagnosis)[] conditions) =>
        new()
        {
            MemberId = $"FM-{Guid.NewGuid()}",
            Relationship = relationship,
            Sex = sex,
            Conditions = conditions
                .Select(c => new FamilyConditionEntry { Condition = c.Condition, AgeAtDiagnosis = c.AgeAtDiagnosis })
                .ToList()
        };

    // ── AssessVariants ───────────────────────────────────────────────────────────────

    [Test]
    public void AssessVariants_GermlinePathogenicBrca1_ProducesHbocFinding()
    {
        List<HereditaryFinding> findings = HereditaryRisk.AssessVariants(
            [Variant("BRCA1", VariantClassification.Pathogenic)]);

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.That(findings[0].Gene, Is.EqualTo("BRCA1"));
        Assert.That(findings[0].Syndrome, Does.Contain("Breast").Or.Contain("HBOC"));
    }

    [Test]
    public void AssessVariants_GermlinePathogenicMlh1_ProducesLynchFinding()
    {
        List<HereditaryFinding> findings = HereditaryRisk.AssessVariants(
            [Variant("MLH1", VariantClassification.Pathogenic)]);

        Assert.That(findings, Has.Count.EqualTo(1));
        Assert.That(findings[0].Gene, Is.EqualTo("MLH1"));
        Assert.That(findings[0].Syndrome, Does.Contain("Lynch"));
    }

    [Test]
    public void AssessVariants_VusBrca1_ProducesNoFinding()
    {
        List<HereditaryFinding> findings = HereditaryRisk.AssessVariants(
            [Variant("BRCA1", VariantClassification.UncertainSignificance)]);

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void AssessVariants_BenignBrca1_ProducesNoFinding()
    {
        List<HereditaryFinding> findings = HereditaryRisk.AssessVariants(
            [Variant("BRCA1", VariantClassification.Benign)]);

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void AssessVariants_SomaticPathogenicBrca1_ProducesNoFinding()
    {
        // Germline-only: a somatic (tumor) pathogenic variant is not a hereditary finding.
        List<HereditaryFinding> findings = HereditaryRisk.AssessVariants(
            [Variant("BRCA1", VariantClassification.Pathogenic, VariantOrigin.Somatic)]);

        Assert.That(findings, Is.Empty);
    }

    [Test]
    public void AssessVariants_PathogenicGeneNotInKnowledgeBase_ProducesNoFinding()
    {
        List<HereditaryFinding> findings = HereditaryRisk.AssessVariants(
            [Variant("FOO1", VariantClassification.Pathogenic)]);

        Assert.That(findings, Is.Empty);
    }

    // ── AssessFamilyHistory ──────────────────────────────────────────────────────────

    [Test]
    public void AssessFamilyHistory_MotherEarlyBreastCancer_FlagsEarlyOnsetBreast()
    {
        List<FamilyRiskFlag> flags = HereditaryRisk.AssessFamilyHistory(
            [Member(FamilyRelationship.Mother, "F", ("Breast cancer", 44))]);

        Assert.That(flags, Has.Some.Matches<FamilyRiskFlag>(
            f => f.Pattern.Contains("Breast") && f.Pattern.Contains("50")));
    }

    [Test]
    public void AssessFamilyHistory_MaternalAuntOvarianCancer_FlagsOvarian()
    {
        List<FamilyRiskFlag> flags = HereditaryRisk.AssessFamilyHistory(
            [Member(FamilyRelationship.MaternalAunt, "F", ("Ovarian cancer", null))]);

        Assert.That(flags, Has.Some.Matches<FamilyRiskFlag>(f => f.Pattern.Contains("Ovarian")));
    }

    [Test]
    public void AssessFamilyHistory_TwoRelativesWithBreastCancer_FlagsTwoOrMore()
    {
        List<FamilyRiskFlag> flags = HereditaryRisk.AssessFamilyHistory(
        [
            Member(FamilyRelationship.Mother, "F", ("Breast cancer", 62)),
            Member(FamilyRelationship.MaternalAunt, "F", ("Breast cancer", 58))
        ]);

        Assert.That(flags, Has.Some.Matches<FamilyRiskFlag>(f => f.Pattern.Contains("Two or more")));
    }

    [Test]
    public void AssessFamilyHistory_EmptyFamilyHistory_ProducesNoFlags()
    {
        List<FamilyRiskFlag> flags = HereditaryRisk.AssessFamilyHistory([]);

        Assert.That(flags, Is.Empty);
    }
}
