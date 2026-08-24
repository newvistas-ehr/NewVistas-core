// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Clinical;

/// <summary>
/// Pure string reasoning over ICD-10 codes, used to <b>propose</b> whether a diagnosis change is
/// a refinement or a real revision (ADR-006).
///
/// The system proposes; the clinician confirms; what gets counted is the clinician's choice.
/// That ordering is the point. A machine's opinion that a doctor was wrong is an accusation
/// nobody will accept and no statistic should be built on; a doctor's own coded statement that
/// they were wrong is defensible, countable, and socially survivable.
///
/// No ICD/SNOMED hierarchy grain is required. <c>Icd10State</c> carries no parent pointer, and
/// prefix comparison makes one unnecessary.
/// </summary>
public static class DiagnosisCodeRelation
{
    /// <summary>
    /// Propose an outcome for a diagnosis change. Deliberately blunt.
    ///
    /// Same 3-character ICD-10 category ⇒ never proposed as a revision. That single rule handles
    /// laterality and encounter suffixes (S72.001A → S72.001D) and sibling specificity
    /// (E11.9 → E11.65) for free, and correctly flags E11.9 → E10.9 (type 2 → type 1 diabetes)
    /// as a genuine revision because the categories differ.
    ///
    /// Where it errs — G43.909 migraine → G44.1 vascular headache is proposed as a revision even
    /// though the categories differ, while a within-category change that really was an error is
    /// proposed as a refinement — it errs toward <i>not</i> calling a clinician wrong. That is the
    /// safe direction for a default that a human is about to confirm.
    /// </summary>
    public static DiagnosticEpisodeOutcome Propose(string? fromCode, string? toCode)
    {
        string from = Normalize(fromCode);
        string to = Normalize(toCode);

        // No codes to compare — the clinician must say what happened.
        if (from.Length == 0 || to.Length == 0) return DiagnosticEpisodeOutcome.Open;

        if (from == to) return DiagnosticEpisodeOutcome.Confirmed;
        if (to.StartsWith(from, StringComparison.Ordinal)) return DiagnosticEpisodeOutcome.Refined;
        if (from.StartsWith(to, StringComparison.Ordinal)) return DiagnosticEpisodeOutcome.Broadened;
        if (Category3(from) == Category3(to)) return DiagnosticEpisodeOutcome.Refined;
        if (SameRefinementFamily(from, to)) return DiagnosticEpisodeOutcome.Refined;

        return DiagnosticEpisodeOutcome.Revised;
    }

    /// <summary>
    /// Categories that are the same disease differing only by <b>which causative agent was
    /// identified</b>. Naming the organism is the workup succeeding, not a clinician having been
    /// wrong — but ICD-10 puts those variants in different 3-character categories, so the plain
    /// prefix rule would propose "Correction" and quietly inflate the error rate.
    ///
    /// Concretely: unidentified influenza (J11) → identified influenza (J10) is a refinement, and
    /// so is unspecified-organism sepsis (A41) → streptococcal sepsis (A40). Without this table
    /// every flu that gets typed and every sepsis that grows an organism would be filed as a
    /// misdiagnosis.
    ///
    /// Deliberately narrow. Membership requires that the categories describe one disease and
    /// differ only on agent identification. Type 1 vs type 2 diabetes (E10/E11) is NOT here and
    /// must never be — those are different diseases and confusing them is a real error, which is
    /// the case the blunt rule already gets right.
    /// </summary>
    private static readonly Dictionary<string, string> RefinementFamilies = BuildFamilies(
        // Influenza: novel identified / other identified / unidentified virus
        ("FLU", new[] { "J09", "J10", "J11" }),
        // Sepsis: streptococcal / other and unspecified organism
        ("SEPSIS", new[] { "A40", "A41" }),
        // Pneumonia by causative organism, through to unspecified
        ("PNEUMONIA", new[] { "J12", "J13", "J14", "J15", "J16", "J17", "J18" }),
        // Viral hepatitis by agent, through to unspecified
        ("VIRAL_HEPATITIS", new[] { "B15", "B16", "B17", "B18", "B19" }),
        // Meningitis by causative organism, through to unspecified
        ("MENINGITIS", new[] { "G00", "G01", "G02", "G03" }));

    private static Dictionary<string, string> BuildFamilies(params (string Family, string[] Categories)[] groups)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string family, string[] categories) in groups)
            foreach (string c in categories)
                map[c] = family;
        return map;
    }

    /// <summary>
    /// True when two codes sit in the same agent-identification family — the same disease with
    /// the organism named (or un-named) rather than a different disease.
    /// </summary>
    public static bool SameRefinementFamily(string? a, string? b)
    {
        string ca = Category3(a), cb = Category3(b);
        if (ca.Length == 0 || cb.Length == 0 || ca == cb) return false;
        return RefinementFamilies.TryGetValue(ca, out string? fa)
            && RefinementFamilies.TryGetValue(cb, out string? fb)
            && fa == fb;
    }

    /// <summary>
    /// The reason to pre-select alongside <see cref="Propose"/>'s outcome. Mirrors it exactly so
    /// the two can never drift apart in the UI.
    /// </summary>
    public static RevisionReason ProposeReason(string? fromCode, string? toCode)
        => Propose(fromCode, toCode) switch
        {
            DiagnosticEpisodeOutcome.Refined or DiagnosticEpisodeOutcome.Broadened
                => RevisionReason.Refinement,
            DiagnosticEpisodeOutcome.Revised => RevisionReason.Correction,
            // Same code, or nothing to compare — no change to explain.
            _ => RevisionReason.Unspecified
        };

    /// <summary>
    /// Normalized shard key form: dots stripped, upper-cased, non-alphanumerics dropped.
    ///
    /// The stripping matters beyond tidiness — grain keys in this feature are colon-delimited and
    /// parsed with a plain <c>Split(':')</c>, so any punctuation surviving into the key would
    /// make the key ambiguous.
    /// </summary>
    public static string Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;
        Span<char> buf = stackalloc char[code.Length];
        int n = 0;
        foreach (char c in code)
            if (char.IsLetterOrDigit(c))
                buf[n++] = char.ToUpperInvariant(c);
        return new string(buf[..n]);
    }

    /// <summary>
    /// The 3-character ICD-10 category ("E119" → "E11"). Codes shorter than three characters are
    /// returned whole.
    /// </summary>
    public static string Category3(string? code)
    {
        string s = Normalize(code);
        return s.Length <= 3 ? s : s[..3];
    }

    /// <summary>
    /// True when a code is an unspecified / not-otherwise-specified form — the ".9" and ".9xx"
    /// endings ICD-10 uses for "we know the category and not the detail".
    ///
    /// Tracked because a rising rate of revisions <i>terminating</i> in NOS codes is the strongest
    /// signal the system can emit that clinicians are systematically failing to reach a diagnosis
    /// — the shape an unnamed emerging disease makes on its way through a problem list.
    /// </summary>
    public static bool IsUnspecified(string? code)
    {
        string s = Normalize(code);
        if (s.Length < 4) return false;

        // E119 / J189 → trailing 9 after the 3-char category; J9601 → "9" in the 4th position
        // followed by more digits is NOT unspecified, so only accept an all-9 tail.
        for (int i = 3; i < s.Length; i++)
            if (s[i] != '9')
                return false;
        return true;
    }
}
