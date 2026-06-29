// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.Abstractions.Clinical;

/// <summary>
/// A matched precision-oncology therapy suggestion for an actionable tumor biomarker.
/// </summary>
[GenerateSerializer]
public record PrecisionTherapyMatch
{
    /// <summary>The biomarker gene/marker that triggered the match.</summary>
    [Id(0)] public string Gene { get; set; } = string.Empty;

    /// <summary>The tumor's actual finding for that marker (e.g. "exon 19 deletion", "TPS 60%").</summary>
    [Id(1)] public string Finding { get; set; } = string.Empty;

    /// <summary>Recommended therapy class (e.g. "EGFR tyrosine kinase inhibitor").</summary>
    [Id(2)] public string TherapyClass { get; set; } = string.Empty;

    /// <summary>Example FDA-approved agents in that class.</summary>
    [Id(3)] public string ExampleAgents { get; set; } = string.Empty;

    /// <summary>Cancer context / tumor types the match applies to.</summary>
    [Id(4)] public string Context { get; set; } = string.Empty;

    /// <summary>Evidence note (e.g. "FDA-approved", "FDA tumor-agnostic").</summary>
    [Id(5)] public string Evidence { get; set; } = string.Empty;
}

/// <summary>
/// Curated precision-oncology knowledge base: maps an actionable molecular biomarker to the
/// targeted / immunotherapy it indicates. Deterministic, rule-based (NCCN/FDA-aligned and
/// illustrative — NOT a substitute for current guidelines or a molecular tumor board). This is
/// the same "model the data, curate the rules" pattern used elsewhere (DUR, interaction checks):
/// the LOINC/genomic terminology service isn't loaded in the demo, so the marker set is curated.
///
/// Part of the PRECISION_ONCOLOGY feature — the precision-medicine layer beyond the classic
/// tumor registry. Matching is read-only decision support; it never auto-orders anything.
/// </summary>
public static class PrecisionOncology
{
    private sealed record Rule(string Gene, string TherapyClass, string ExampleAgents, string Context, string Evidence);

    // Each rule fires when the tumor carries a POSITIVE biomarker for that gene/marker.
    private static readonly Rule[] Rules =
    {
        new("EGFR",  "EGFR tyrosine kinase inhibitor", "osimertinib",            "NSCLC — activating mutation (e.g. exon 19 del, L858R)", "FDA-approved"),
        new("ALK",   "ALK inhibitor",                  "alectinib, brigatinib",  "NSCLC — ALK rearrangement",                            "FDA-approved"),
        new("ROS1",  "ROS1 inhibitor",                 "crizotinib, entrectinib","NSCLC — ROS1 rearrangement",                           "FDA-approved"),
        new("BRAF",  "BRAF + MEK inhibitor",           "dabrafenib + trametinib","Melanoma, NSCLC, others — V600E",                      "FDA-approved"),
        new("KRAS",  "KRAS G12C inhibitor",            "sotorasib, adagrasib",   "NSCLC, colorectal — G12C",                             "FDA-approved"),
        new("HER2",  "Anti-HER2 therapy",              "trastuzumab, T-DXd",     "Breast, gastric — amplified / IHC 3+",                 "FDA-approved"),
        new("PD-L1", "PD-1/PD-L1 checkpoint inhibitor","pembrolizumab",          "NSCLC & others — high expression (e.g. TPS ≥50%)", "FDA-approved"),
        new("MSI",   "PD-1 checkpoint inhibitor",      "pembrolizumab, dostarlimab","Any solid tumor — MSI-High / dMMR",                 "FDA tumor-agnostic"),
        new("TMB",   "PD-1 checkpoint inhibitor",      "pembrolizumab",          "Any solid tumor — TMB-High (≥10 mut/Mb)",         "FDA tumor-agnostic"),
        new("NTRK",  "TRK inhibitor",                  "larotrectinib, entrectinib","Any solid tumor — NTRK gene fusion",                "FDA tumor-agnostic"),
        new("BRCA1", "PARP inhibitor",                 "olaparib, niraparib",    "Breast, ovarian, prostate, pancreatic",                "FDA-approved"),
        new("BRCA2", "PARP inhibitor",                 "olaparib, niraparib",    "Breast, ovarian, prostate, pancreatic",                "FDA-approved"),
        new("RET",   "RET inhibitor",                  "selpercatinib, pralsetinib","NSCLC, thyroid — RET fusion/mutation",              "FDA-approved"),
    };

    /// <summary>Canonical marker symbols the knowledge base recognizes (for UI pickers).</summary>
    public static IReadOnlyList<string> KnownMarkers { get; } = Rules.Select(r => r.Gene).ToList();

    /// <summary>
    /// Match a tumor's biomarker panel to indicated targeted/immunotherapies. Only POSITIVE
    /// (actionable) biomarkers produce matches; negatives/equivocals/pending are ignored.
    /// </summary>
    public static List<PrecisionTherapyMatch> Match(IEnumerable<TumorBiomarker>? biomarkers)
    {
        var matches = new List<PrecisionTherapyMatch>();
        if (biomarkers is null) return matches;

        foreach (TumorBiomarker b in biomarkers)
        {
            if (b.Status != BiomarkerStatus.Positive || string.IsNullOrWhiteSpace(b.Gene)) continue;

            foreach (Rule r in Rules)
            {
                if (Normalize(r.Gene) == Normalize(b.Gene))
                {
                    matches.Add(new PrecisionTherapyMatch
                    {
                        Gene = b.Gene,
                        Finding = b.Result,
                        TherapyClass = r.TherapyClass,
                        ExampleAgents = r.ExampleAgents,
                        Context = r.Context,
                        Evidence = r.Evidence
                    });
                }
            }
        }
        return matches;
    }

    // "PD-L1" == "PDL1" == "pd l1"; "ERBB2" treated as HER2 alias.
    private static string Normalize(string gene)
    {
        string g = gene.Trim().ToUpperInvariant().Replace("-", "").Replace(" ", "").Replace("_", "");
        return g switch
        {
            "ERBB2" => "HER2",
            "MMR" or "DMMR" => "MSI",
            _ => g
        };
    }
}
