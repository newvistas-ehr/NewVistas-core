// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.Abstractions.Clinical;

/// <summary>A hereditary syndrome implied by a pathogenic/likely-pathogenic germline variant.</summary>
[GenerateSerializer]
public record HereditaryFinding
{
    [Id(0)] public string Gene { get; set; } = string.Empty;
    /// <summary>The variant (HGVS) that triggered the finding.</summary>
    [Id(1)] public string Variant { get; set; } = string.Empty;
    [Id(2)] public VariantClassification Classification { get; set; }
    /// <summary>Hereditary syndrome, e.g. "Hereditary Breast and Ovarian Cancer (HBOC)".</summary>
    [Id(3)] public string Syndrome { get; set; } = string.Empty;
    [Id(4)] public string Inheritance { get; set; } = string.Empty;
    /// <summary>Management / surveillance summary.</summary>
    [Id(5)] public string Management { get; set; } = string.Empty;
    [Id(6)] public string Source { get; set; } = string.Empty;
}

/// <summary>
/// A cascade-testing opportunity (ADR-002 Phase 5): a relative on this patient's chart is LINKED to a
/// Person who is also a patient here with a confirmed pathogenic germline finding — so targeted
/// (cascade) testing can be offered to this patient. Made possible by the Person anchor connecting the
/// relative-appearance to a real chart.
/// </summary>
[GenerateSerializer]
public record CascadeOpportunity
{
    [Id(0)] public string RelativeName { get; set; } = string.Empty;
    [Id(1)] public string Relationship { get; set; } = string.Empty;
    /// <summary>The relative's own chart — the source of the confirmed finding.</summary>
    [Id(2)] public string RelativePatientId { get; set; } = string.Empty;
    [Id(3)] public string Gene { get; set; } = string.Empty;
    [Id(4)] public string Variant { get; set; } = string.Empty;
    [Id(5)] public string Syndrome { get; set; } = string.Empty;
    [Id(6)] public string Recommendation { get; set; } = string.Empty;
}

/// <summary>A family-history pattern suggesting hereditary risk and a referral recommendation.</summary>
[GenerateSerializer]
public record FamilyRiskFlag
{
    /// <summary>The pattern detected, e.g. "Breast cancer diagnosed &lt; 50".</summary>
    [Id(0)] public string Pattern { get; set; } = string.Empty;
    /// <summary>Which relatives / specifics.</summary>
    [Id(1)] public string Detail { get; set; } = string.Empty;
    [Id(2)] public string Recommendation { get; set; } = string.Empty;
}

/// <summary>
/// Curated hereditary-risk knowledge base — maps a pathogenic germline variant to its hereditary
/// syndrome + management, and scans structured family history for red-flag patterns that warrant a
/// genetics referral. Deterministic, rule-based, illustrative (NCCN/ACMG-aligned — same "model the
/// data, curate the rules" pattern as <see cref="PrecisionOncology"/> and <see cref="Pharmacogenomics"/>);
/// NOT a substitute for a genetic counselor or current guideline criteria (e.g. full NCCN/Amsterdam II).
/// Read-only decision support — never auto-orders.
/// </summary>
public static class HereditaryRisk
{
    private sealed record GeneSyndrome(string Gene, string Syndrome, string Inheritance, string Management);

    private static readonly GeneSyndrome[] Genes =
    {
        new("BRCA1", "Hereditary Breast and Ovarian Cancer (HBOC)", "Autosomal dominant",
            "Enhanced breast surveillance (annual MRI + mammography), discuss risk-reducing mastectomy/salpingo-oophorectomy and chemoprophylaxis; PARP-inhibitor eligibility in associated cancers; cascade testing of at-risk relatives."),
        new("BRCA2", "Hereditary Breast and Ovarian Cancer (HBOC)", "Autosomal dominant",
            "Enhanced breast surveillance, risk-reducing options, also elevated pancreatic/prostate/melanoma risk; PARP-inhibitor eligibility; cascade testing."),
        new("PALB2", "Hereditary breast cancer", "Autosomal dominant",
            "Enhanced breast surveillance (MRI); discuss risk-reducing options; cascade testing."),
        new("TP53", "Li-Fraumeni syndrome", "Autosomal dominant",
            "Toronto-protocol surveillance (whole-body + breast MRI); avoid radiation where feasible; cascade testing."),
        new("PTEN", "Cowden syndrome / PTEN hamartoma tumor syndrome", "Autosomal dominant",
            "Breast/thyroid/endometrial/renal surveillance; dermatologic exam; cascade testing."),
        new("STK11", "Peutz-Jeghers syndrome", "Autosomal dominant",
            "GI, breast, and pancreatic surveillance; cascade testing."),
        new("CDH1", "Hereditary diffuse gastric cancer", "Autosomal dominant",
            "Consider prophylactic total gastrectomy; lobular breast surveillance; cascade testing."),
        new("MLH1", "Lynch syndrome", "Autosomal dominant",
            "Colonoscopy every 1-2 yrs from age 20-25; endometrial/ovarian surveillance and risk-reducing hysterectomy/BSO; aspirin chemoprophylaxis; cascade testing."),
        new("MSH2", "Lynch syndrome", "Autosomal dominant",
            "Colonoscopy every 1-2 yrs; endometrial/ovarian risk management; cascade testing."),
        new("MSH6", "Lynch syndrome", "Autosomal dominant",
            "Colonoscopy surveillance; endometrial risk management; cascade testing."),
        new("PMS2", "Lynch syndrome", "Autosomal dominant",
            "Colonoscopy surveillance (lower penetrance); cascade testing."),
        new("EPCAM", "Lynch syndrome (MSH2 epimutation)", "Autosomal dominant",
            "Colonoscopy surveillance; cascade testing."),
        new("APC", "Familial adenomatous polyposis (FAP)", "Autosomal dominant",
            "Colorectal surveillance / colectomy planning; upper-GI surveillance; cascade testing."),
        new("MUTYH", "MUTYH-associated polyposis (MAP)", "Autosomal recessive",
            "Significant when BIALLELIC — colorectal surveillance similar to attenuated FAP; test relatives/partner; monoallelic carriers have modest risk."),
        new("CDKN2A", "Hereditary melanoma / pancreatic cancer", "Autosomal dominant",
            "Dermatologic surveillance and sun protection; consider pancreatic surveillance; cascade testing."),
        new("RET", "Multiple endocrine neoplasia type 2 (MEN2)", "Autosomal dominant",
            "Prophylactic thyroidectomy timed by codon; pheochromocytoma/parathyroid surveillance; cascade testing."),
        new("VHL", "von Hippel-Lindau disease", "Autosomal dominant",
            "Multi-organ surveillance (retina, CNS, renal, adrenal, pancreas); cascade testing."),
    };

    /// <summary>Canonical hereditary genes the knowledge base recognizes (for UI pickers).</summary>
    public static IReadOnlyList<string> KnownGenes { get; } = Genes.Select(g => g.Gene).ToList();

    /// <summary>
    /// Hereditary syndrome findings from a patient's reported variants. Only PATHOGENIC /
    /// LIKELY-PATHOGENIC GERMLINE variants in a known hereditary gene produce a finding (VUS,
    /// benign, and somatic variants are ignored).
    /// </summary>
    public static List<HereditaryFinding> AssessVariants(IEnumerable<GeneticVariant>? variants)
    {
        var findings = new List<HereditaryFinding>();
        if (variants is null) return findings;

        foreach (GeneticVariant v in variants)
        {
            if (v.Origin == VariantOrigin.Somatic) continue;
            if (v.Classification is not (VariantClassification.Pathogenic or VariantClassification.LikelyPathogenic)) continue;
            if (string.IsNullOrWhiteSpace(v.Gene)) continue;

            GeneSyndrome? gs = Genes.FirstOrDefault(g =>
                string.Equals(g.Gene, v.Gene.Trim(), StringComparison.OrdinalIgnoreCase));
            if (gs is null) continue;

            findings.Add(new HereditaryFinding
            {
                Gene = gs.Gene,
                Variant = FormatVariant(v),
                Classification = v.Classification,
                Syndrome = gs.Syndrome,
                Inheritance = gs.Inheritance,
                Management = gs.Management,
                Source = "NCCN/ACMG (illustrative)"
            });
        }
        return findings;
    }

    private static string FormatVariant(GeneticVariant v)
    {
        string change = !string.IsNullOrWhiteSpace(v.HgvsCoding)
            ? (string.IsNullOrWhiteSpace(v.HgvsProtein) ? v.HgvsCoding : $"{v.HgvsCoding} ({v.HgvsProtein})")
            : v.HgvsProtein;
        return string.IsNullOrWhiteSpace(change) ? v.Gene : $"{v.Gene} {change}";
    }

    private const string ReferRecommendation =
        "Consider referral to genetics / genetic counseling for hereditary cancer risk assessment.";

    /// <summary>
    /// Scans structured family history for red-flag patterns that warrant a hereditary-cancer
    /// genetics referral (representative criteria — not the full NCCN/Amsterdam II rule set).
    /// </summary>
    public static List<FamilyRiskFlag> AssessFamilyHistory(IEnumerable<FamilyMemberHistoryEntry>? members)
    {
        var flags = new List<FamilyRiskFlag>();
        if (members is null) return flags;
        List<FamilyMemberHistoryEntry> fam = members.ToList();

        // Index conditions by category across the family.
        var breast = new List<(FamilyMemberHistoryEntry M, FamilyConditionEntry C)>();
        var ovarian = new List<(FamilyMemberHistoryEntry M, FamilyConditionEntry C)>();
        var colorectal = new List<(FamilyMemberHistoryEntry M, FamilyConditionEntry C)>();
        var endometrial = new List<(FamilyMemberHistoryEntry M, FamilyConditionEntry C)>();
        var pancreatic = new List<(FamilyMemberHistoryEntry M, FamilyConditionEntry C)>();
        var knownSyndrome = new List<(FamilyMemberHistoryEntry M, FamilyConditionEntry C)>();

        foreach (FamilyMemberHistoryEntry m in fam)
            foreach (FamilyConditionEntry c in m.Conditions)
            {
                string t = (c.Condition ?? string.Empty).ToLowerInvariant();
                if (t.Contains("breast")) breast.Add((m, c));
                if (t.Contains("ovar")) ovarian.Add((m, c));
                if (t.Contains("colon") || t.Contains("colorectal") || t.Contains("bowel")) colorectal.Add((m, c));
                if (t.Contains("endometr") || t.Contains("uterine")) endometrial.Add((m, c));
                if (t.Contains("pancrea")) pancreatic.Add((m, c));
                if (t.Contains("brca") || t.Contains("lynch") || t.Contains("pathogenic") || t.Contains("hereditary"))
                    knownSyndrome.Add((m, c));
            }

        // 1. Ovarian cancer at any age.
        if (ovarian.Count > 0)
            flags.Add(new FamilyRiskFlag
            {
                Pattern = "Ovarian cancer in a blood relative",
                Detail = Describe(ovarian),
                Recommendation = ReferRecommendation
            });

        // 2. Breast cancer diagnosed < 50.
        var earlyBreast = breast.Where(x => x.C.AgeAtDiagnosis is > 0 and < 50).ToList();
        if (earlyBreast.Count > 0)
            flags.Add(new FamilyRiskFlag
            {
                Pattern = "Breast cancer diagnosed before age 50",
                Detail = Describe(earlyBreast),
                Recommendation = ReferRecommendation
            });

        // 3. Male breast cancer.
        var maleBreast = breast.Where(x => IsMale(x.M)).ToList();
        if (maleBreast.Count > 0)
            flags.Add(new FamilyRiskFlag
            {
                Pattern = "Male breast cancer",
                Detail = Describe(maleBreast),
                Recommendation = ReferRecommendation
            });

        // 4. Two or more relatives with breast cancer.
        if (breast.Select(x => x.M.MemberId).Distinct().Count() >= 2)
            flags.Add(new FamilyRiskFlag
            {
                Pattern = "Two or more relatives with breast cancer",
                Detail = Describe(breast),
                Recommendation = ReferRecommendation
            });

        // 5. Colorectal or endometrial cancer < 50 (Lynch).
        var earlyLynch = colorectal.Concat(endometrial).Where(x => x.C.AgeAtDiagnosis is > 0 and < 50).ToList();
        if (earlyLynch.Count > 0)
            flags.Add(new FamilyRiskFlag
            {
                Pattern = "Colorectal or endometrial cancer before age 50 (Lynch spectrum)",
                Detail = Describe(earlyLynch),
                Recommendation = ReferRecommendation
            });

        // 6. Three or more relatives with Lynch-spectrum cancers.
        var lynchSpectrum = colorectal.Concat(endometrial).Concat(ovarian)
            .Select(x => x.M.MemberId).Distinct().Count();
        if (lynchSpectrum >= 3)
            flags.Add(new FamilyRiskFlag
            {
                Pattern = "Three or more relatives with Lynch-spectrum cancers",
                Detail = $"{lynchSpectrum} relatives with colorectal / endometrial / ovarian cancer.",
                Recommendation = ReferRecommendation
            });

        // 7. Pancreatic cancer with breast/ovarian in the family.
        if (pancreatic.Count > 0 && (breast.Count > 0 || ovarian.Count > 0))
            flags.Add(new FamilyRiskFlag
            {
                Pattern = "Pancreatic cancer with breast/ovarian cancer in the family",
                Detail = Describe(pancreatic),
                Recommendation = ReferRecommendation
            });

        // 8. A known hereditary syndrome / pathogenic variant documented in a relative.
        if (knownSyndrome.Count > 0)
            flags.Add(new FamilyRiskFlag
            {
                Pattern = "Known hereditary syndrome / pathogenic variant in a relative",
                Detail = Describe(knownSyndrome),
                Recommendation = "Offer cascade (targeted) genetic testing; refer to genetic counseling."
            });

        return flags;
    }

    private static bool IsMale(FamilyMemberHistoryEntry m)
    {
        if (!string.IsNullOrWhiteSpace(m.Sex) && m.Sex.Trim().StartsWith("M", StringComparison.OrdinalIgnoreCase))
            return true;
        return m.Relationship is FamilyRelationship.Father or FamilyRelationship.Brother or FamilyRelationship.Son
            or FamilyRelationship.MaternalGrandfather or FamilyRelationship.PaternalGrandfather
            or FamilyRelationship.MaternalUncle or FamilyRelationship.PaternalUncle or FamilyRelationship.Nephew;
    }

    private static string Describe(List<(FamilyMemberHistoryEntry M, FamilyConditionEntry C)> hits) =>
        string.Join("; ", hits
            .Select(x => $"{Friendly(x.M.Relationship)}: {x.C.Condition}"
                + (x.C.AgeAtDiagnosis is int a and > 0 ? $" (dx {a})" : string.Empty))
            .Distinct());

    private static string Friendly(FamilyRelationship r) => r switch
    {
        FamilyRelationship.MaternalGrandmother => "Maternal grandmother",
        FamilyRelationship.MaternalGrandfather => "Maternal grandfather",
        FamilyRelationship.PaternalGrandmother => "Paternal grandmother",
        FamilyRelationship.PaternalGrandfather => "Paternal grandfather",
        FamilyRelationship.MaternalAunt => "Maternal aunt",
        FamilyRelationship.MaternalUncle => "Maternal uncle",
        FamilyRelationship.PaternalAunt => "Paternal aunt",
        FamilyRelationship.PaternalUncle => "Paternal uncle",
        FamilyRelationship.HalfSibling => "Half-sibling",
        _ => r.ToString()
    };
}
