// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.Abstractions.Clinical;

/// <summary>
/// A matched pharmacogenomic prescribing recommendation: the patient's gene result implies a
/// drug-gene action (avoid / adjust / contraindicated / …).
/// </summary>
[GenerateSerializer]
public record PgxRecommendation
{
    /// <summary>Pharmacogene that triggered the match (e.g. "CYP2C19").</summary>
    [Id(0)] public string Gene { get; set; } = string.Empty;
    /// <summary>Patient's diplotype for that gene (e.g. "*2/*2").</summary>
    [Id(1)] public string Diplotype { get; set; } = string.Empty;
    /// <summary>Patient's phenotype for that gene.</summary>
    [Id(2)] public PgxPhenotype Phenotype { get; set; }
    /// <summary>Friendly phenotype label (e.g. "Poor metabolizer").</summary>
    [Id(3)] public string PhenotypeLabel { get; set; } = string.Empty;
    /// <summary>Affected drug (generic).</summary>
    [Id(4)] public string Drug { get; set; } = string.Empty;
    /// <summary>Drug class / context.</summary>
    [Id(5)] public string DrugClass { get; set; } = string.Empty;
    /// <summary>Indicated prescribing action.</summary>
    [Id(6)] public PgxActionCategory Action { get; set; }
    /// <summary>CPIC recommendation strength.</summary>
    [Id(7)] public PgxRecommendationStrength Strength { get; set; }
    /// <summary>Human-readable guidance.</summary>
    [Id(8)] public string Recommendation { get; set; } = string.Empty;
    /// <summary>Evidence source (e.g. "CPIC", "FDA").</summary>
    [Id(9)] public string Source { get; set; } = string.Empty;
}

/// <summary>
/// Curated pharmacogenomics knowledge base: maps a patient's gene phenotype to drug-gene prescribing
/// guidance. Deterministic, rule-based (CPIC/FDA-aligned and illustrative — NOT a substitute for the
/// current CPIC guidelines or a clinical pharmacist). This is the same "model the data, curate the
/// rules" pattern as <see cref="PrecisionOncology"/>: the representative rule set covers high-impact
/// CPIC Level-A pairs, not the full guideline corpus.
///
/// Part of the PHARMACOGENOMICS feature; read by the DUR engine so a drug-gene contraindication
/// surfaces at prescribing time alongside drug-drug / drug-allergy checks. Read-only decision
/// support — it never auto-orders or auto-cancels anything.
/// </summary>
public static class Pharmacogenomics
{
    private sealed record Rule(
        string Gene,
        PgxPhenotype[] Triggers,
        string Drug,
        string DrugClass,
        PgxActionCategory Action,
        PgxRecommendationStrength Strength,
        string Recommendation,
        string Source = "CPIC");

    // High-impact, representative CPIC Level-A drug-gene pairs. Triggers list the phenotypes that fire.
    private static readonly Rule[] Rules =
    {
        // ── CYP2C19 ───────────────────────────────────────────────────────────
        new("CYP2C19", new[]{ PgxPhenotype.PoorMetabolizer }, "clopidogrel", "P2Y12 antiplatelet",
            PgxActionCategory.Avoid, PgxRecommendationStrength.Strong,
            "Poor metabolizer: markedly reduced clopidogrel activation and antiplatelet effect. Use prasugrel or ticagrelor if not contraindicated."),
        new("CYP2C19", new[]{ PgxPhenotype.IntermediateMetabolizer }, "clopidogrel", "P2Y12 antiplatelet",
            PgxActionCategory.ConsiderAlternative, PgxRecommendationStrength.Moderate,
            "Intermediate metabolizer: reduced clopidogrel activation. Consider prasugrel or ticagrelor."),
        new("CYP2C19", new[]{ PgxPhenotype.PoorMetabolizer }, "voriconazole", "Azole antifungal",
            PgxActionCategory.AdjustDose, PgxRecommendationStrength.Moderate,
            "Higher voriconazole exposure; consider a lower dose with therapeutic drug monitoring, or an alternative agent."),
        new("CYP2C19", new[]{ PgxPhenotype.UltrarapidMetabolizer, PgxPhenotype.RapidMetabolizer }, "voriconazole", "Azole antifungal",
            PgxActionCategory.ConsiderAlternative, PgxRecommendationStrength.Moderate,
            "Subtherapeutic voriconazole likely; consider an alternative antifungal."),

        // ── CYP2D6 ────────────────────────────────────────────────────────────
        new("CYP2D6", new[]{ PgxPhenotype.UltrarapidMetabolizer }, "codeine", "Opioid analgesic",
            PgxActionCategory.Avoid, PgxRecommendationStrength.Strong,
            "Ultrarapid metabolism → high morphine formation; risk of life-threatening respiratory depression. Avoid codeine; use a non-codeine/non-tramadol analgesic."),
        new("CYP2D6", new[]{ PgxPhenotype.PoorMetabolizer }, "codeine", "Opioid analgesic",
            PgxActionCategory.Avoid, PgxRecommendationStrength.Strong,
            "Poor metabolism → little/no morphine formation; inadequate analgesia. Avoid codeine; use an alternative analgesic."),
        new("CYP2D6", new[]{ PgxPhenotype.UltrarapidMetabolizer }, "tramadol", "Opioid analgesic",
            PgxActionCategory.Avoid, PgxRecommendationStrength.Strong,
            "Ultrarapid metabolism → increased active metabolite; toxicity risk. Avoid tramadol."),
        new("CYP2D6", new[]{ PgxPhenotype.PoorMetabolizer }, "tramadol", "Opioid analgesic",
            PgxActionCategory.ConsiderAlternative, PgxRecommendationStrength.Moderate,
            "Poor metabolism → reduced analgesia. Consider a non-CYP2D6 analgesic."),

        // ── CYP2C9 (+ VKORC1 for warfarin) ───────────────────────────────────
        new("CYP2C9", new[]{ PgxPhenotype.PoorMetabolizer, PgxPhenotype.IntermediateMetabolizer }, "warfarin", "Vitamin-K antagonist",
            PgxActionCategory.AdjustDose, PgxRecommendationStrength.Moderate,
            "Reduced warfarin clearance; use genotype-guided (lower) initial dosing (with VKORC1) and monitor INR closely."),
        new("CYP2C9", new[]{ PgxPhenotype.PoorMetabolizer }, "phenytoin", "Anticonvulsant",
            PgxActionCategory.AdjustDose, PgxRecommendationStrength.Moderate,
            "Reduced phenytoin clearance; consider a lower dose and monitor levels."),

        // ── DPYD (fluoropyrimidines) ─────────────────────────────────────────
        new("DPYD", new[]{ PgxPhenotype.PoorMetabolizer, PgxPhenotype.PoorFunction }, "fluorouracil", "Fluoropyrimidine chemotherapy",
            PgxActionCategory.Avoid, PgxRecommendationStrength.Strong,
            "DPYD poor metabolizer: high risk of severe, potentially fatal fluoropyrimidine toxicity. Avoid 5-FU/capecitabine; select an alternative regimen."),
        new("DPYD", new[]{ PgxPhenotype.PoorMetabolizer, PgxPhenotype.PoorFunction }, "capecitabine", "Fluoropyrimidine chemotherapy",
            PgxActionCategory.Avoid, PgxRecommendationStrength.Strong,
            "DPYD poor metabolizer: high risk of severe, potentially fatal toxicity. Avoid capecitabine; select an alternative regimen."),
        new("DPYD", new[]{ PgxPhenotype.IntermediateMetabolizer, PgxPhenotype.DecreasedFunction }, "fluorouracil", "Fluoropyrimidine chemotherapy",
            PgxActionCategory.AdjustDose, PgxRecommendationStrength.Strong,
            "DPYD intermediate metabolizer: reduce starting dose by ~50% and titrate to toxicity/effect."),
        new("DPYD", new[]{ PgxPhenotype.IntermediateMetabolizer, PgxPhenotype.DecreasedFunction }, "capecitabine", "Fluoropyrimidine chemotherapy",
            PgxActionCategory.AdjustDose, PgxRecommendationStrength.Strong,
            "DPYD intermediate metabolizer: reduce starting dose by ~50% and titrate to toxicity/effect."),

        // ── TPMT / NUDT15 (thiopurines) ──────────────────────────────────────
        new("TPMT", new[]{ PgxPhenotype.PoorMetabolizer }, "azathioprine", "Thiopurine immunosuppressant",
            PgxActionCategory.ConsiderAlternative, PgxRecommendationStrength.Strong,
            "TPMT poor metabolizer: markedly increased risk of severe myelosuppression. Use an alternative agent, or drastically reduce dose (start ~10%, reduce frequency) with close CBC monitoring."),
        new("TPMT", new[]{ PgxPhenotype.PoorMetabolizer }, "mercaptopurine", "Thiopurine antimetabolite",
            PgxActionCategory.ConsiderAlternative, PgxRecommendationStrength.Strong,
            "TPMT poor metabolizer: severe myelosuppression risk. Alternative agent or drastic dose reduction with monitoring."),
        new("TPMT", new[]{ PgxPhenotype.IntermediateMetabolizer }, "azathioprine", "Thiopurine immunosuppressant",
            PgxActionCategory.AdjustDose, PgxRecommendationStrength.Strong,
            "TPMT intermediate metabolizer: start at a reduced dose (30-80% of target) and titrate; monitor CBC."),
        new("TPMT", new[]{ PgxPhenotype.IntermediateMetabolizer }, "mercaptopurine", "Thiopurine antimetabolite",
            PgxActionCategory.AdjustDose, PgxRecommendationStrength.Strong,
            "TPMT intermediate metabolizer: reduce starting dose and titrate; monitor CBC."),
        new("NUDT15", new[]{ PgxPhenotype.PoorMetabolizer }, "mercaptopurine", "Thiopurine antimetabolite",
            PgxActionCategory.ConsiderAlternative, PgxRecommendationStrength.Strong,
            "NUDT15 poor metabolizer: increased myelosuppression risk. Alternative agent or drastic dose reduction."),
        new("NUDT15", new[]{ PgxPhenotype.IntermediateMetabolizer }, "mercaptopurine", "Thiopurine antimetabolite",
            PgxActionCategory.AdjustDose, PgxRecommendationStrength.Strong,
            "NUDT15 intermediate metabolizer: reduce starting dose and titrate; monitor CBC."),

        // ── G6PD ──────────────────────────────────────────────────────────────
        new("G6PD", new[]{ PgxPhenotype.Deficient }, "rasburicase", "Urate oxidase",
            PgxActionCategory.Contraindicated, PgxRecommendationStrength.Strong,
            "G6PD deficiency: rasburicase is contraindicated — risk of severe hemolysis and methemoglobinemia.", "FDA"),
        new("G6PD", new[]{ PgxPhenotype.Deficient }, "dapsone", "Sulfone",
            PgxActionCategory.Avoid, PgxRecommendationStrength.Strong,
            "G6PD deficiency: risk of hemolytic anemia. Avoid, or use with extreme caution and hematologic monitoring."),
        new("G6PD", new[]{ PgxPhenotype.Deficient }, "primaquine", "Antimalarial",
            PgxActionCategory.Avoid, PgxRecommendationStrength.Strong,
            "G6PD deficiency: risk of acute hemolysis. Avoid (G6PD testing is required before use)."),

        // ── SLCO1B1 (statin myopathy) ────────────────────────────────────────
        new("SLCO1B1", new[]{ PgxPhenotype.PoorFunction }, "simvastatin", "HMG-CoA reductase inhibitor",
            PgxActionCategory.ConsiderAlternative, PgxRecommendationStrength.Strong,
            "SLCO1B1 poor function: markedly increased simvastatin exposure and myopathy risk. Use an alternative statin (e.g. pravastatin, rosuvastatin)."),
        new("SLCO1B1", new[]{ PgxPhenotype.DecreasedFunction }, "simvastatin", "HMG-CoA reductase inhibitor",
            PgxActionCategory.AdjustDose, PgxRecommendationStrength.Strong,
            "SLCO1B1 decreased function: increased myopathy risk. Limit dose (≤20 mg) or use an alternative statin."),

        // ── UGT1A1 ────────────────────────────────────────────────────────────
        new("UGT1A1", new[]{ PgxPhenotype.PoorMetabolizer }, "irinotecan", "Topoisomerase-I inhibitor",
            PgxActionCategory.AdjustDose, PgxRecommendationStrength.Strong,
            "UGT1A1 poor metabolizer: increased risk of severe neutropenia and diarrhea; consider a reduced starting dose."),

        // ── HLA risk alleles (immune-mediated reactions) ─────────────────────
        new("HLA-B*57:01", new[]{ PgxPhenotype.Positive }, "abacavir", "NRTI antiretroviral",
            PgxActionCategory.Contraindicated, PgxRecommendationStrength.Strong,
            "HLA-B*57:01 positive: high risk of abacavir hypersensitivity reaction. Abacavir is contraindicated.", "FDA"),
        new("HLA-B*15:02", new[]{ PgxPhenotype.Positive }, "carbamazepine", "Anticonvulsant",
            PgxActionCategory.Contraindicated, PgxRecommendationStrength.Strong,
            "HLA-B*15:02 positive: high risk of Stevens-Johnson syndrome / toxic epidermal necrolysis. Avoid carbamazepine.", "FDA"),
        new("HLA-B*15:02", new[]{ PgxPhenotype.Positive }, "oxcarbazepine", "Anticonvulsant",
            PgxActionCategory.Avoid, PgxRecommendationStrength.Strong,
            "HLA-B*15:02 positive: increased SJS/TEN risk. Avoid oxcarbazepine."),
        new("HLA-B*58:01", new[]{ PgxPhenotype.Positive }, "allopurinol", "Xanthine oxidase inhibitor",
            PgxActionCategory.Avoid, PgxRecommendationStrength.Strong,
            "HLA-B*58:01 positive: risk of severe cutaneous adverse reaction (SJS/TEN/DRESS). Avoid allopurinol; use an alternative urate-lowering agent."),
    };

    // Brand → generic, so a prescription written under a brand name still matches.
    private static readonly (string Brand, string Generic)[] BrandAliases =
    {
        ("plavix", "clopidogrel"), ("coumadin", "warfarin"), ("jantoven", "warfarin"),
        ("ziagen", "abacavir"), ("tegretol", "carbamazepine"), ("trileptal", "oxcarbazepine"),
        ("imuran", "azathioprine"), ("purinethol", "mercaptopurine"), ("6-mp", "mercaptopurine"),
        ("6mp", "mercaptopurine"), ("xeloda", "capecitabine"), ("5-fu", "fluorouracil"),
        ("adrucil", "fluorouracil"), ("zyloprim", "allopurinol"), ("elitek", "rasburicase"),
        ("ultram", "tramadol"), ("zocor", "simvastatin"), ("camptosar", "irinotecan"),
        ("vfend", "voriconazole"), ("dilantin", "phenytoin"),
    };

    /// <summary>Canonical gene symbols the knowledge base recognizes (for UI pickers).</summary>
    public static IReadOnlyList<string> KnownGenes { get; } =
        Rules.Select(r => r.Gene).Distinct().ToList();

    /// <summary>Canonical drugs the knowledge base covers (for UI pickers).</summary>
    public static IReadOnlyList<string> KnownDrugs { get; } =
        Rules.Select(r => r.Drug).Distinct().OrderBy(d => d).ToList();

    /// <summary>
    /// All drug-gene recommendations implied by a patient's profile (drug-agnostic "implications"
    /// list) — every drug to watch given the patient's gene phenotypes, worst action first.
    /// </summary>
    public static List<PgxRecommendation> Match(IEnumerable<PgxResultEntry>? results)
    {
        var matches = new List<PgxRecommendation>();
        if (results is null) return matches;
        List<PgxResultEntry> profile = results.ToList();

        foreach (Rule r in Rules)
        {
            PgxResultEntry? hit = FindTriggeringResult(profile, r);
            if (hit is not null) matches.Add(ToRecommendation(r, hit));
        }
        return matches
            .OrderByDescending(m => (int)m.Action)
            .ThenBy(m => m.Drug, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Recommendations for a specific drug given the patient's profile (used by the DUR engine and a
    /// "check a drug" lookup). Empty when no rule for the drug fires for this patient.
    /// </summary>
    public static List<PgxRecommendation> MatchDrug(IEnumerable<PgxResultEntry>? results, string? drugName)
    {
        var matches = new List<PgxRecommendation>();
        if (results is null || string.IsNullOrWhiteSpace(drugName)) return matches;
        List<PgxResultEntry> profile = results.ToList();

        foreach (Rule r in Rules)
        {
            if (!DrugMatches(r.Drug, drugName)) continue;
            PgxResultEntry? hit = FindTriggeringResult(profile, r);
            if (hit is not null) matches.Add(ToRecommendation(r, hit));
        }
        return matches.OrderByDescending(m => (int)m.Action).ToList();
    }

    private static PgxResultEntry? FindTriggeringResult(List<PgxResultEntry> profile, Rule r)
        => profile.FirstOrDefault(p =>
            NormalizeGene(p.Gene) == NormalizeGene(r.Gene) && r.Triggers.Contains(p.Phenotype));

    private static PgxRecommendation ToRecommendation(Rule r, PgxResultEntry hit) => new()
    {
        Gene = r.Gene,
        Diplotype = hit.Diplotype,
        Phenotype = hit.Phenotype,
        PhenotypeLabel = PhenotypeLabel(hit.Phenotype),
        Drug = r.Drug,
        DrugClass = r.DrugClass,
        Action = r.Action,
        Strength = r.Strength,
        Recommendation = r.Recommendation,
        Source = r.Source
    };

    private static bool DrugMatches(string ruleDrug, string inputDrug)
    {
        string d = inputDrug.ToLowerInvariant();
        if (d.Contains(ruleDrug)) return true;
        foreach ((string brand, string generic) in BrandAliases)
            if (generic == ruleDrug && d.Contains(brand)) return true;
        return false;
    }

    // "HLA-B*57:01" == "HLAB5701"; "CYP2C19" unchanged.
    private static string NormalizeGene(string gene)
        => new(gene.Trim().ToUpperInvariant()
            .Where(char.IsLetterOrDigit).ToArray());

    /// <summary>Friendly label for a phenotype.</summary>
    public static string PhenotypeLabel(PgxPhenotype p) => p switch
    {
        PgxPhenotype.UltrarapidMetabolizer => "Ultrarapid metabolizer",
        PgxPhenotype.RapidMetabolizer => "Rapid metabolizer",
        PgxPhenotype.NormalMetabolizer => "Normal metabolizer",
        PgxPhenotype.IntermediateMetabolizer => "Intermediate metabolizer",
        PgxPhenotype.PoorMetabolizer => "Poor metabolizer",
        PgxPhenotype.IncreasedFunction => "Increased function",
        PgxPhenotype.NormalFunction => "Normal function",
        PgxPhenotype.DecreasedFunction => "Decreased function",
        PgxPhenotype.PoorFunction => "Poor function",
        PgxPhenotype.Positive => "Positive",
        PgxPhenotype.Negative => "Negative",
        PgxPhenotype.Deficient => "Deficient",
        PgxPhenotype.Variable => "Variable",
        _ => "Indeterminate"
    };
}
