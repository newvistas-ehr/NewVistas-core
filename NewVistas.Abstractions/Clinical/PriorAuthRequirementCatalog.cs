// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Clinical;

/// <summary>How broadly a baseline requirement rule applies.</summary>
public enum RuleScope
{
    /// <summary>Applies to any payer for this procedure (the clinical/utilization-management baseline).</summary>
    Any = 0,
    /// <summary>Applies to a class of payers (Commercial / Medicare / Medicaid).</summary>
    PayerType = 1,
    /// <summary>Applies to one specific payer (its <c>PayerId</c>).</summary>
    SpecificPayer = 2
}

/// <summary>One resolved baseline requirement for a (payer, procedure) — a "box to fill."</summary>
public sealed record RequirementItem(
    PriorAuthRequirementCategory Category,
    string Description,
    bool TypicallyRequired);

/// <summary>One line of the merged "fill these boxes" checklist (curated baseline ∪ learned denials).</summary>
[GenerateSerializer]
public record PriorAuthRequirementLine
{
    [Id(0)] public PriorAuthRequirementCategory Category { get; set; }
    [Id(1)] public string CategoryDisplay { get; set; } = string.Empty;
    [Id(2)] public string Description { get; set; } = string.Empty;
    [Id(3)] public bool TypicallyRequired { get; set; }
    [Id(4)] public RequirementSource Source { get; set; }
    [Id(5)] public int DenialCount { get; set; }
    [Id(6)] public DateTime? LastDeniedOn { get; set; }
    [Id(7)] public string? SampleDenialReason { get; set; }
    [Id(8)] public int ApprovalSatisfiedCount { get; set; }
    [Id(9)] public int RankScore { get; set; }
    [Id(10)] public string WhyLabel { get; set; } = string.Empty;
}

/// <summary>
/// The full ranked "for a TKA (27447) for Blue Cross of Florida, fill in these boxes" answer.
/// </summary>
[GenerateSerializer]
public record PriorAuthRequirementChecklist
{
    [Id(0)] public string CptCode { get; set; } = string.Empty;
    [Id(1)] public string CptDescription { get; set; } = string.Empty;
    [Id(2)] public string PayerId { get; set; } = string.Empty;
    [Id(3)] public string PayerName { get; set; } = string.Empty;
    [Id(4)] public string PayerType { get; set; } = string.Empty;
    [Id(5)] public DateTime GeneratedAt { get; set; }
    [Id(6)] public IReadOnlyList<PriorAuthRequirementLine> Items { get; set; } = new List<PriorAuthRequirementLine>();
    [Id(7)] public int ObservedDenialTotal { get; set; }
    /// <summary>True when there is no learned history yet — the checklist is baseline-only.</summary>
    [Id(8)] public bool IsColdStart { get; set; }
    /// <summary>Observed denial reasons that didn't map to a real category.</summary>
    [Id(9)] public IReadOnlyList<UnmappedDenial> OtherObservedReasons { get; set; } = new List<UnmappedDenial>();
}

/// <summary>
/// Curated baseline knowledge base of prior-authorization documentation requirements, keyed by
/// (scope, CPT code). Deterministic, rule-based, and <b>illustrative</b> — payer policies change and
/// vary; this is a starting checklist, not a substitute for the payer's current medical-policy bulletin.
/// Same "model the data, curate the rules" house style as <see cref="PrecisionOncology"/> /
/// <c>SdohScreeningCatalog</c>. The learned half (<c>PAYER-PROC:{payerId}:{cpt}</c> shard) refines and
/// reranks this from real denial outcomes; <see cref="Merge"/> unions the two.
/// </summary>
public static class PriorAuthRequirementCatalog
{
    private sealed record Rule(
        RuleScope Scope,
        string ScopeKey,   // payerId | payer-type ("COMMERCIAL"/"MEDICARE"/"MEDICAID") | "" for Any
        string CptCode,
        PriorAuthRequirementCategory Category,
        string Description,
        bool TypicallyRequired);

    private static readonly Rule[] Rules =
    {
        // 27447 — Total knee arthroplasty (TKA/TJA)
        new(RuleScope.Any, "", "27447", PriorAuthRequirementCategory.ConservativeTherapyTrial,
            "≥3 months of documented conservative therapy (PT, NSAIDs, intra-articular injections)", true),
        new(RuleScope.Any, "", "27447", PriorAuthRequirementCategory.ImagingEvidence,
            "Weight-bearing radiographs showing joint-space narrowing (Kellgren-Lawrence grade)", true),
        new(RuleScope.Any, "", "27447", PriorAuthRequirementCategory.MedicalNecessityNarrative,
            "Functional-limitation documentation plus BMI / comorbidity note", true),
        // Commercial payers increasingly steer TKA to an outpatient / ASC site of service.
        new(RuleScope.PayerType, "COMMERCIAL", "27447", PriorAuthRequirementCategory.SiteOfServiceJustification,
            "Justification if performed inpatient rather than outpatient/ASC", false),

        // 72148 — MRI lumbar spine without contrast (radiology-benefit-manager PA)
        new(RuleScope.Any, "", "72148", PriorAuthRequirementCategory.ConservativeTherapyTrial,
            "≥6 weeks of conservative care unless red-flag findings are documented", true),
        new(RuleScope.Any, "", "72148", PriorAuthRequirementCategory.MedicalNecessityNarrative,
            "Symptoms, exam findings, and how the result will change management", true),
        new(RuleScope.Any, "", "72148", PriorAuthRequirementCategory.ContraindicationDocs,
            "Note any implanted device / contrast-contraindication considerations", false),

        // Physical therapy — evaluation + therapeutic exercise
        new(RuleScope.Any, "", "97110", PriorAuthRequirementCategory.MedicalNecessityNarrative,
            "Signed plan of care with measurable, time-bound functional goals", true),
        new(RuleScope.Any, "", "97110", PriorAuthRequirementCategory.FailedPriorTreatment,
            "Prior treatment tried and response, if applicable", false),
        new(RuleScope.Any, "", "97161", PriorAuthRequirementCategory.MedicalNecessityNarrative,
            "PT evaluation with plan of care and functional goals", true),
        new(RuleScope.Any, "", "97162", PriorAuthRequirementCategory.MedicalNecessityNarrative,
            "PT evaluation (moderate complexity) with plan of care and functional goals", true),
        new(RuleScope.Any, "", "97163", PriorAuthRequirementCategory.MedicalNecessityNarrative,
            "PT evaluation (high complexity) with plan of care and functional goals", true),
    };

    // Duplicate self-check (same style as PrecisionOncology / SdohScreeningCatalog).
    static PriorAuthRequirementCatalog()
    {
        var seen = new HashSet<string>();
        foreach (Rule r in Rules)
        {
            string key = $"{r.Scope}|{r.ScopeKey.ToUpperInvariant()}|{r.CptCode.ToUpperInvariant()}|{r.Category}";
            if (!seen.Add(key))
                throw new InvalidOperationException($"Duplicate prior-auth requirement rule: {key}");
        }
    }

    /// <summary>CPT codes the baseline catalog knows about (for UI pickers / suggestions).</summary>
    public static IReadOnlyList<string> KnownCptCodes { get; } =
        Rules.Select(r => r.CptCode).Distinct().OrderBy(c => c).ToList();

    /// <summary>
    /// Classifies a payer id into a broad type without touching <c>PayerConfigState</c> — Medicare /
    /// Medicaid by name, else Commercial. Keeps the state schema untouched (no <c>[Id]</c> change).
    /// </summary>
    public static string ClassifyPayerType(string? payerId)
    {
        string p = (payerId ?? string.Empty).ToUpperInvariant();
        if (p.Contains("MEDICARE")) return "MEDICARE";
        if (p.Contains("MEDICAID")) return "MEDICAID";
        return "COMMERCIAL";
    }

    /// <summary>
    /// Resolves the curated baseline requirements for a (payer, procedure), unioning the three scope
    /// tiers in specificity order (SpecificPayer &gt; PayerType &gt; Any), deduped by category keeping the
    /// most specific rule.
    /// </summary>
    public static IReadOnlyList<RequirementItem> GetBaseline(string cptCode, string? payerId, string? payerType)
    {
        string cpt = (cptCode ?? string.Empty).Trim().ToUpperInvariant();
        string payer = (payerId ?? string.Empty).Trim().ToUpperInvariant();
        string type = (payerType ?? ClassifyPayerType(payerId)).Trim().ToUpperInvariant();

        static int Specificity(RuleScope s) => (int)s; // Any=0 < PayerType=1 < SpecificPayer=2

        return Rules
            .Where(r => r.CptCode.ToUpperInvariant() == cpt)
            .Where(r => r.Scope == RuleScope.Any
                        || (r.Scope == RuleScope.PayerType && r.ScopeKey.ToUpperInvariant() == type)
                        || (r.Scope == RuleScope.SpecificPayer && r.ScopeKey.ToUpperInvariant() == payer))
            .GroupBy(r => r.Category)
            .Select(g => g.OrderByDescending(r => Specificity(r.Scope)).First())
            .Select(r => new RequirementItem(r.Category, r.Description, r.TypicallyRequired))
            .ToList();
    }

    /// <summary>Human label for a requirement category.</summary>
    public static string DisplayName(PriorAuthRequirementCategory c) => c switch
    {
        PriorAuthRequirementCategory.ConservativeTherapyTrial => "Conservative therapy trial",
        PriorAuthRequirementCategory.ImagingEvidence => "Imaging evidence",
        PriorAuthRequirementCategory.FailedPriorTreatment => "Failed prior treatment",
        PriorAuthRequirementCategory.MedicalNecessityNarrative => "Medical-necessity narrative",
        PriorAuthRequirementCategory.SiteOfServiceJustification => "Site-of-service justification",
        PriorAuthRequirementCategory.LabResults => "Lab results",
        PriorAuthRequirementCategory.ContraindicationDocs => "Contraindication documentation",
        PriorAuthRequirementCategory.DeviceOrImplantJustification => "Device / implant justification",
        PriorAuthRequirementCategory.SpecialistEvaluation => "Specialist evaluation",
        PriorAuthRequirementCategory.ClinicalGuidelineCitation => "Clinical-guideline citation",
        PriorAuthRequirementCategory.Other => "Other",
        _ => c.ToString()
    };

    /// <summary>
    /// Merges the curated baseline with the learned (payer, procedure) denial profile into one ranked
    /// checklist. Pure/deterministic so it unit-tests without a cluster. Ranking (desc): (1) denial
    /// count — "how often THIS payer denied for this", (2) baseline-required, (3) typically-required,
    /// (4) approval-satisfied count; final stable tiebreak = category enum order.
    /// </summary>
    public static PriorAuthRequirementChecklist Merge(
        IReadOnlyList<RequirementItem> baseline,
        PayerProcedureRequirementProfile profile,
        string cptCode, string cptDescription, string payerId, string payerName, string payerType, DateTime generatedAt)
    {
        var lines = new Dictionary<PriorAuthRequirementCategory, PriorAuthRequirementLine>();

        foreach (RequirementItem b in baseline)
        {
            lines[b.Category] = new PriorAuthRequirementLine
            {
                Category = b.Category,
                CategoryDisplay = DisplayName(b.Category),
                Description = b.Description,
                TypicallyRequired = b.TypicallyRequired,
                Source = RequirementSource.Baseline
            };
        }

        foreach (CategoryStat s in profile.CategoryStats)
        {
            if (lines.TryGetValue(s.Category, out PriorAuthRequirementLine? line))
            {
                line.Source = RequirementSource.Both;
            }
            else
            {
                line = new PriorAuthRequirementLine
                {
                    Category = s.Category,
                    CategoryDisplay = DisplayName(s.Category),
                    Description = "Observed denial driver for this payer — verify the current payer policy",
                    TypicallyRequired = false,
                    Source = RequirementSource.Learned
                };
                lines[s.Category] = line;
            }
            line.DenialCount = s.DenialCount;
            line.LastDeniedOn = s.LastDeniedOn;
            line.SampleDenialReason = s.LastSampleReason;
            line.ApprovalSatisfiedCount = s.ApprovalSatisfiedCount;
        }

        foreach (PriorAuthRequirementLine line in lines.Values)
        {
            // Deterministic composite weight: denials dominate, then baseline/typically-required, then approvals.
            line.RankScore = line.DenialCount * 1000
                             + (line.Source != RequirementSource.Learned ? 100 : 0)
                             + (line.TypicallyRequired ? 50 : 0)
                             + Math.Min(line.ApprovalSatisfiedCount, 9);
            line.WhyLabel = line.DenialCount > 0
                ? $"Denied {line.DenialCount}× by this payer" + (line.LastDeniedOn is { } d ? $" (last {d:yyyy-MM-dd})" : "")
                : line.Source == RequirementSource.Learned
                    ? "Observed for this payer"
                    : "Standard requirement for this procedure";
        }

        List<PriorAuthRequirementLine> ordered = lines.Values
            .OrderByDescending(l => l.DenialCount)
            .ThenByDescending(l => l.Source != RequirementSource.Learned)  // baseline-backed above learned-only
            .ThenByDescending(l => l.TypicallyRequired)
            .ThenByDescending(l => l.ApprovalSatisfiedCount)
            .ThenBy(l => (int)l.Category)
            .ToList();

        return new PriorAuthRequirementChecklist
        {
            CptCode = cptCode,
            CptDescription = cptDescription,
            PayerId = payerId,
            PayerName = payerName,
            PayerType = payerType,
            GeneratedAt = generatedAt,
            Items = ordered,
            ObservedDenialTotal = profile.TotalDenials,
            IsColdStart = profile.TotalDenials == 0 && profile.TotalApprovals == 0,
            OtherObservedReasons = profile.UnmappedDenials
        };
    }
}
