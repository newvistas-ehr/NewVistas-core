// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for the curated pharmacogenomics (PGx) drug-gene knowledge base (PHARMACOGENOMICS).
/// Pure logic — no Orleans cluster needed (the class under test is static).
/// </summary>
[TestFixture]
public class PharmacogenomicsTests
{
    private static PgxResultEntry Result(string gene, PgxPhenotype phenotype, string diplotype = "") =>
        new() { Gene = gene, Phenotype = phenotype, Diplotype = diplotype };

    [Test]
    public void MatchDrug_Cyp2c19PoorMetabolizer_Clopidogrel_IsAvoid()
    {
        List<PgxRecommendation> recs = Pharmacogenomics.MatchDrug(
            [Result("CYP2C19", PgxPhenotype.PoorMetabolizer, "*2/*2")], "clopidogrel");

        Assert.That(recs, Has.Count.EqualTo(1));
        Assert.That(recs[0].Action, Is.EqualTo(PgxActionCategory.Avoid));
        Assert.That(recs[0].Drug, Is.EqualTo("clopidogrel"));
    }

    [Test]
    public void MatchDrug_Cyp2c19IntermediateMetabolizer_Clopidogrel_IsConsiderAlternative()
    {
        List<PgxRecommendation> recs = Pharmacogenomics.MatchDrug(
            [Result("CYP2C19", PgxPhenotype.IntermediateMetabolizer, "*1/*2")], "clopidogrel");

        Assert.That(recs, Has.Count.EqualTo(1));
        Assert.That(recs[0].Action, Is.EqualTo(PgxActionCategory.ConsiderAlternative));
    }

    [Test]
    public void MatchDrug_HlaB5701Positive_Abacavir_IsContraindicated()
    {
        List<PgxRecommendation> recs = Pharmacogenomics.MatchDrug(
            [Result("HLA-B*57:01", PgxPhenotype.Positive, "positive")], "abacavir");

        Assert.That(recs, Has.Count.EqualTo(1));
        Assert.That(recs[0].Action, Is.EqualTo(PgxActionCategory.Contraindicated));
    }

    [Test]
    public void MatchDrug_DpydIntermediateMetabolizer_Fluorouracil_IsAdjustDose()
    {
        List<PgxRecommendation> recs = Pharmacogenomics.MatchDrug(
            [Result("DPYD", PgxPhenotype.IntermediateMetabolizer)], "fluorouracil");

        Assert.That(recs, Has.Count.EqualTo(1));
        Assert.That(recs[0].Action, Is.EqualTo(PgxActionCategory.AdjustDose));
    }

    [Test]
    public void MatchDrug_DpydPoorMetabolizer_Capecitabine_IsAvoid()
    {
        List<PgxRecommendation> recs = Pharmacogenomics.MatchDrug(
            [Result("DPYD", PgxPhenotype.PoorMetabolizer)], "capecitabine");

        Assert.That(recs, Has.Count.EqualTo(1));
        Assert.That(recs[0].Action, Is.EqualTo(PgxActionCategory.Avoid));
    }

    [Test]
    public void MatchDrug_G6pdDeficient_Rasburicase_IsContraindicated()
    {
        List<PgxRecommendation> recs = Pharmacogenomics.MatchDrug(
            [Result("G6PD", PgxPhenotype.Deficient)], "rasburicase");

        Assert.That(recs, Has.Count.EqualTo(1));
        Assert.That(recs[0].Action, Is.EqualTo(PgxActionCategory.Contraindicated));
    }

    [Test]
    public void MatchDrug_BrandAlias_PlavixResolvesToClopidogrelAvoid()
    {
        // Case-insensitive substring + brand alias: "Plavix 75mg tab" still hits the clopidogrel rule.
        List<PgxRecommendation> recs = Pharmacogenomics.MatchDrug(
            [Result("CYP2C19", PgxPhenotype.PoorMetabolizer, "*2/*2")], "Plavix 75mg tab");

        Assert.That(recs, Is.Not.Empty);
        Assert.That(recs[0].Drug, Is.EqualTo("clopidogrel"));
        Assert.That(recs[0].Action, Is.EqualTo(PgxActionCategory.Avoid));
    }

    [Test]
    public void MatchDrug_NormalMetabolizerOnly_Clopidogrel_IsEmpty()
    {
        List<PgxRecommendation> recs = Pharmacogenomics.MatchDrug(
            [Result("CYP2C19", PgxPhenotype.NormalMetabolizer, "*1/*1")], "clopidogrel");

        Assert.That(recs, Is.Empty);
    }

    [Test]
    public void MatchDrug_DrugNotInKnowledgeBase_IsEmpty()
    {
        List<PgxRecommendation> recs = Pharmacogenomics.MatchDrug(
            [Result("CYP2C19", PgxPhenotype.PoorMetabolizer, "*2/*2")], "amoxicillin");

        Assert.That(recs, Is.Empty);
    }

    [Test]
    public void Match_MultiGeneProfile_OrdersWorstActionFirst()
    {
        // CYP2C19 PM (clopidogrel Avoid) + HLA-B*57:01 Positive (abacavir Contraindicated).
        // Contraindicated (5) must sort ahead of Avoid (4).
        List<PgxRecommendation> recs = Pharmacogenomics.Match(
        [
            Result("CYP2C19", PgxPhenotype.PoorMetabolizer, "*2/*2"),
            Result("HLA-B*57:01", PgxPhenotype.Positive, "positive")
        ]);

        Assert.That(recs, Is.Not.Empty);
        Assert.That(recs[0].Action, Is.EqualTo(PgxActionCategory.Contraindicated));
        Assert.That(recs.Any(r => r.Action == PgxActionCategory.Avoid), Is.True);
    }

    [Test]
    public void PhenotypeLabel_PoorMetabolizer_IsFriendlyText()
    {
        Assert.That(Pharmacogenomics.PhenotypeLabel(PgxPhenotype.PoorMetabolizer),
            Is.EqualTo("Poor metabolizer"));
    }
}
