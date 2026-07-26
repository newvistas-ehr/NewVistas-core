// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainStates;
using NUnit.Framework;

namespace NewVistas.UnitTests;

/// <summary>
/// Pure tests for the prior-auth requirements catalog + merge — no cluster needed. Covers the curated
/// baseline scope tiers, payer-type classification, normalization, and the curated-∪-learned merge/rank
/// that produces the "fill these boxes" checklist.
/// </summary>
[TestFixture]
public class PriorAuthRequirementCatalogTests
{
    private static PayerProcedureRequirementProfile EmptyProfile(string payerId = "PAYER-BCBS-FL", string cpt = "27447")
        => new() { PayerId = payerId, CptCode = cpt };

    [Test]
    public void ClassifyPayerType_MapsByName()
    {
        Assert.Multiple(() =>
        {
            Assert.That(PriorAuthRequirementCatalog.ClassifyPayerType("PAYER-MEDICARE"), Is.EqualTo("MEDICARE"));
            Assert.That(PriorAuthRequirementCatalog.ClassifyPayerType("PAYER-MEDICAID-FL"), Is.EqualTo("MEDICAID"));
            Assert.That(PriorAuthRequirementCatalog.ClassifyPayerType("PAYER-BCBS-FL"), Is.EqualTo("COMMERCIAL"));
        });
    }

    [Test]
    public void GetBaseline_Tka_ReturnsCoreRequirements_AndCommercialOverlay()
    {
        IReadOnlyList<RequirementItem> commercial = PriorAuthRequirementCatalog.GetBaseline("27447", "PAYER-BCBS-FL", "COMMERCIAL");
        var cats = commercial.Select(r => r.Category).ToList();
        Assert.That(cats, Does.Contain(PriorAuthRequirementCategory.ConservativeTherapyTrial));
        Assert.That(cats, Does.Contain(PriorAuthRequirementCategory.ImagingEvidence));
        Assert.That(cats, Does.Contain(PriorAuthRequirementCategory.MedicalNecessityNarrative));
        // Commercial payer picks up the site-of-service overlay.
        Assert.That(cats, Does.Contain(PriorAuthRequirementCategory.SiteOfServiceJustification));

        // Medicare does NOT get the commercial overlay.
        IReadOnlyList<RequirementItem> medicare = PriorAuthRequirementCatalog.GetBaseline("27447", "PAYER-MEDICARE", "MEDICARE");
        Assert.That(medicare.Select(r => r.Category), Does.Not.Contain(PriorAuthRequirementCategory.SiteOfServiceJustification));
    }

    [Test]
    public void GetBaseline_NormalizesCase_AndUnknownCptIsEmpty()
    {
        Assert.That(PriorAuthRequirementCatalog.GetBaseline("27447", "payer-bcbs-fl", "commercial"), Is.Not.Empty);
        Assert.That(PriorAuthRequirementCatalog.GetBaseline("00000", "PAYER-BCBS-FL", "COMMERCIAL"), Is.Empty);
    }

    [Test]
    public void Merge_ColdStart_IsBaselineOnly_RankedByRequired()
    {
        IReadOnlyList<RequirementItem> baseline = PriorAuthRequirementCatalog.GetBaseline("27447", "PAYER-BCBS-FL", "COMMERCIAL");
        PriorAuthRequirementChecklist result = PriorAuthRequirementCatalog.Merge(
            baseline, EmptyProfile(), "27447", "TKA", "PAYER-BCBS-FL", "BCBS FL", "COMMERCIAL", new DateTime(2026, 7, 20));

        Assert.That(result.IsColdStart, Is.True);
        Assert.That(result.ObservedDenialTotal, Is.EqualTo(0));
        Assert.That(result.Items, Is.Not.Empty);
        Assert.That(result.Items.All(i => i.Source == RequirementSource.Baseline), Is.True);
        Assert.That(result.Items.All(i => i.DenialCount == 0), Is.True);
        // Required items sort above not-required (site-of-service overlay is not required).
        Assert.That(result.Items.First().TypicallyRequired, Is.True);
        Assert.That(result.Items.Last().Category, Is.EqualTo(PriorAuthRequirementCategory.SiteOfServiceJustification));
    }

    [Test]
    public void Merge_DenialsRankToTop_AndMarkBoth()
    {
        var profile = new PayerProcedureRequirementProfile
        {
            PayerId = "PAYER-BCBS-FL", CptCode = "27447", TotalDenials = 3, TotalApprovals = 1,
            CategoryStats = new List<CategoryStat>
            {
                // ImagingEvidence denied more than ConservativeTherapyTrial → should rank first.
                new() { Category = PriorAuthRequirementCategory.ImagingEvidence, DenialCount = 3, LastDeniedOn = new DateTime(2026, 5, 10), LastSampleReason = "no films" },
                new() { Category = PriorAuthRequirementCategory.ConservativeTherapyTrial, DenialCount = 1, ApprovalSatisfiedCount = 1 },
            }
        };
        IReadOnlyList<RequirementItem> baseline = PriorAuthRequirementCatalog.GetBaseline("27447", "PAYER-BCBS-FL", "COMMERCIAL");
        PriorAuthRequirementChecklist result = PriorAuthRequirementCatalog.Merge(
            baseline, profile, "27447", "TKA", "PAYER-BCBS-FL", "BCBS FL", "COMMERCIAL", new DateTime(2026, 7, 20));

        Assert.That(result.IsColdStart, Is.False);
        Assert.That(result.Items.First().Category, Is.EqualTo(PriorAuthRequirementCategory.ImagingEvidence));
        Assert.That(result.Items.First().DenialCount, Is.EqualTo(3));
        Assert.That(result.Items.First().Source, Is.EqualTo(RequirementSource.Both));
        Assert.That(result.Items.First().WhyLabel, Does.Contain("Denied 3"));
        // A baseline-only category (MedicalNecessityNarrative) has no denials but is still present.
        Assert.That(result.Items.Select(i => i.Category), Does.Contain(PriorAuthRequirementCategory.MedicalNecessityNarrative));
    }

    [Test]
    public void Merge_LearnedOnlyCategory_AndUnmappedReasons_AreSurfaced()
    {
        var profile = new PayerProcedureRequirementProfile
        {
            PayerId = "PAYER-BCBS-FL", CptCode = "27447", TotalDenials = 2,
            CategoryStats = new List<CategoryStat>
            {
                // Not in the TKA baseline → learned-only line.
                new() { Category = PriorAuthRequirementCategory.SpecialistEvaluation, DenialCount = 1 },
            },
            UnmappedDenials = new List<UnmappedDenial> { new() { ReasonText = "coordination of benefits", Count = 1, LastSeen = new DateTime(2026, 6, 1) } }
        };
        PriorAuthRequirementChecklist result = PriorAuthRequirementCatalog.Merge(
            PriorAuthRequirementCatalog.GetBaseline("27447", "PAYER-BCBS-FL", "COMMERCIAL"),
            profile, "27447", "TKA", "PAYER-BCBS-FL", "BCBS FL", "COMMERCIAL", new DateTime(2026, 7, 20));

        PriorAuthRequirementLine? learned = result.Items.FirstOrDefault(i => i.Category == PriorAuthRequirementCategory.SpecialistEvaluation);
        Assert.That(learned, Is.Not.Null);
        Assert.That(learned!.Source, Is.EqualTo(RequirementSource.Learned));
        Assert.That(result.OtherObservedReasons, Has.Count.EqualTo(1));
        Assert.That(result.OtherObservedReasons[0].ReasonText, Is.EqualTo("coordination of benefits"));
    }
}
