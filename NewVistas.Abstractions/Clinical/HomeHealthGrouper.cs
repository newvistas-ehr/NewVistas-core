// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Clinical;

/// <summary>
/// Inputs to the PDGM grouper for one 30-day payment period.
/// </summary>
public sealed class HomeHealthGroupingInput
{
    public HomeCareAdmissionSource AdmissionSource { get; set; }
    /// <summary>True for the first 30-day period of a sequence; false for "late" periods.</summary>
    public bool IsEarlyPeriod { get; set; }
    public string PrimaryDiagnosisCode { get; set; } = string.Empty;
    public List<string> SecondaryDiagnoses { get; set; } = new();
    /// <summary>OASIS item code → value (the M18xx functional items drive the functional level).</summary>
    public Dictionary<string, string> OasisItems { get; set; } = new();
    /// <summary>Number of completed visits in the period (drives the LUPA determination).</summary>
    public int VisitCount { get; set; }
}

/// <summary>
/// Deterministic Patient-Driven Groupings Model (PDGM) grouper — the Medicare home-health
/// payment classifier (HH PPS, effective CY2020+). Classifies a 30-day payment period from five
/// inputs: admission source, timing, clinical grouping (principal diagnosis), functional
/// impairment level (OASIS), and comorbidity adjustment; and flags LUPA (Low Utilization Payment
/// Adjustment) from the visit count.
///
/// <para><b>Scope (curated/representative — like <see cref="PrecisionOncology"/>):</b> this is a
/// faithful reproduction of the PDGM <i>structure</i> and the case-mix HIPPS encoding, with a
/// curated diagnosis→clinical-group map, a simplified OASIS functional-points scale, and a
/// representative comorbidity rule and LUPA threshold. It is NOT the full CMS grouper: it does not
/// carry the complete ICD-10 clinical-group assignment tables, the official comorbidity
/// interaction subgroups, the per-group case-mix weights/dollar amounts, or the exact per-group
/// LUPA thresholds. It produces decision-support / what-if classification, not a billable rate.</para>
/// </summary>
public static class HomeHealthGrouper
{
    /// <summary>Classifies one 30-day payment period.</summary>
    public static PdgmGroupingResult Group(HomeHealthGroupingInput input)
    {
        string admission = IsInstitutional(input.AdmissionSource) ? "Institutional" : "Community";
        string timing = input.IsEarlyPeriod ? "Early" : "Late";
        (string clinicalGroup, char clinicalLetter) = ClinicalGroupFor(input.PrimaryDiagnosisCode);
        (string functionalLevel, char functionalChar) = FunctionalLevelFor(input.OasisItems);
        (string comorbidity, char comorbidityChar) = ComorbidityFor(input.SecondaryDiagnoses);

        // HIPPS-style 5-character case-mix code:
        //  [1] timing+admission   [2] clinical group   [3] functional level   [4] comorbidity   [5] reserved
        char timingAdmissionChar = (admission == "Community")
            ? (input.IsEarlyPeriod ? '1' : '2')
            : (input.IsEarlyPeriod ? '3' : '4');
        string hipps = $"{timingAdmissionChar}{clinicalLetter}{functionalChar}{comorbidityChar}1";

        bool isLupa = input.VisitCount < LupaThresholdFor(functionalLevel);

        return new PdgmGroupingResult
        {
            CaseMixGroup = hipps,
            AdmissionSource = admission,
            Timing = timing,
            ClinicalGrouping = clinicalGroup,
            FunctionalLevel = functionalLevel,
            ComorbidityAdjustment = comorbidity,
            IsLupa = isLupa
        };
    }

    private static bool IsInstitutional(HomeCareAdmissionSource source) => source switch
    {
        HomeCareAdmissionSource.AcuteHospital => true,
        HomeCareAdmissionSource.SkilledNursingFacility => true,
        HomeCareAdmissionSource.InpatientRehab => true,
        _ => false
    };

    /// <summary>
    /// Curated principal-diagnosis → PDGM clinical group (12 groups; representative ICD-10
    /// prefixes). Defaults to MMTA - Other.
    /// </summary>
    private static (string group, char letter) ClinicalGroupFor(string dx)
    {
        string code = (dx ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length == 0) return ("MMTA - Other", 'A');

        // Wounds / ulcers / post-op wound care
        if (StartsWithAny(code, "L89", "L97", "L98", "T81.4", "S", "I70.2"))
            return ("Wound", 'W');
        // Neuro / stroke rehab
        if (StartsWithAny(code, "I6", "G", "F03", "R47"))
            return ("Neuro / Stroke Rehabilitation", 'N');
        // Musculoskeletal rehab (joint replacement, fractures, dorsopathies)
        if (StartsWithAny(code, "M", "Z47", "Z96.6"))
            return ("Musculoskeletal Rehabilitation", 'R');
        // Behavioral health
        if (StartsWithAny(code, "F0", "F1", "F2", "F3", "F4"))
            return ("Behavioral Health", 'B');
        // Complex nursing interventions (paralysis, ostomy, IV, vent dependence)
        if (StartsWithAny(code, "Z93", "Z99", "Z43", "G82", "G83"))
            return ("Complex Nursing Interventions", 'X');
        // MMTA subgroups
        if (StartsWithAny(code, "J")) return ("MMTA - Respiratory", 'C');
        if (StartsWithAny(code, "I")) return ("MMTA - Cardiac and Circulatory", 'D');
        if (StartsWithAny(code, "E")) return ("MMTA - Endocrine", 'E');
        if (StartsWithAny(code, "K", "N")) return ("MMTA - Gastrointestinal / Genitourinary", 'G');
        if (StartsWithAny(code, "A", "B", "C", "D")) return ("MMTA - Infectious / Neoplasm / Blood", 'H');
        if (StartsWithAny(code, "Z48", "Z49")) return ("MMTA - Surgical Aftercare", 'S');
        return ("MMTA - Other", 'A');
    }

    /// <summary>
    /// PDGM functional impairment level from the OASIS functional items (M1800 grooming,
    /// M1810/M1820 dressing, M1830 bathing, M1840 toilet transfer, M1850 transferring,
    /// M1860 ambulation, M1033 hospitalization risk). Higher summed response = more impaired.
    /// Simplified single Low/Medium/High scale (CMS varies thresholds by clinical group).
    /// </summary>
    private static (string level, char ch) FunctionalLevelFor(IDictionary<string, string> oasis)
    {
        string[] functionalItems = { "M1800", "M1810", "M1820", "M1830", "M1840", "M1850", "M1860" };
        int score = 0;
        foreach (string item in functionalItems)
            if (oasis.TryGetValue(item, out string? v) && int.TryParse(v, out int n))
                score += n;
        // M1033 risk-for-hospitalization: count of checked risk factors (0-N) adds weight.
        if (oasis.TryGetValue("M1033", out string? risk) && int.TryParse(risk, out int riskCount))
            score += Math.Min(riskCount, 3);

        if (score <= 6) return ("Low", 'A');
        if (score <= 13) return ("Medium", 'B');
        return ("High", 'C');
    }

    /// <summary>
    /// Comorbidity adjustment from secondary diagnoses. Representative rule: count "relevant"
    /// chronic comorbidity chapters; 0 = None, 1 = Low, 2+ (interaction) = High. The real model
    /// uses specific comorbidity subgroups and interaction pairs.
    /// </summary>
    private static (string level, char ch) ComorbidityFor(IEnumerable<string> secondaryDiagnoses)
    {
        int relevant = 0;
        foreach (string dx in secondaryDiagnoses ?? Enumerable.Empty<string>())
        {
            string code = (dx ?? string.Empty).Trim().ToUpperInvariant();
            if (StartsWithAny(code, "E10", "E11", "I10", "I11", "I12", "I13", "I20", "I25", "I48", "I50",
                    "J44", "N18", "F03", "F32", "F33", "Z99", "G20", "M05", "M06", "C"))
                relevant++;
        }
        if (relevant == 0) return ("None", '1');
        if (relevant == 1) return ("Low", '2');
        return ("High", '3');
    }

    /// <summary>
    /// Representative LUPA visit threshold (CMS assigns each case-mix group a 2-6 visit threshold;
    /// below it the period is paid per-visit). Simplified: scales with functional level.
    /// </summary>
    private static int LupaThresholdFor(string functionalLevel) => functionalLevel switch
    {
        "Low" => 3,
        "Medium" => 4,
        _ => 5
    };

    private static bool StartsWithAny(string code, params string[] prefixes)
    {
        foreach (string p in prefixes)
            if (code.StartsWith(p, StringComparison.Ordinal)) return true;
        return false;
    }
}
