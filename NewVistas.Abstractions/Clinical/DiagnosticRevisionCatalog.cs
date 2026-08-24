// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Clinical;

/// <summary>
/// Curated, literature-backed pairings of a working diagnosis with the dangerous alternative it
/// is known to be mistaken for, and the test that distinguishes them (ADR-006).
///
/// <b>The baseline carries the arrow and the citation. It never carries a percentage.</b> A
/// payer's prior-auth policy is a policy fact and can be hand-authored; a misdiagnosis <i>rate</i>
/// is site-specific epidemiology, and hand-authoring one would be fabricating clinical evidence.
///
/// This is also what solves the rare-disease problem. The min-N floors gate only the learned
/// percentage, so a <see cref="DiagnosticHarmIfMissed.Critical"/> line renders at n = 0 and sorts
/// to the top. Dizziness → posterior circulation stroke will never reach n = 20 at a single
/// clinic, and that is precisely where silence would do the most harm.
///
/// Sources: SIDM/AHRQ "Big Three" (vascular events, infections, cancers — roughly 75% of serious
/// misdiagnosis-related harm), the AHRQ 2022 ED diagnostic-error evidence review, and established
/// decision rules (HINTS, Wells/PERC, HEART, Centor, Ottawa).
/// </summary>
public static class DiagnosticRevisionCatalog
{
    /// <summary>
    /// One curated pairing. Discriminators are (key, display) pairs rather than a shared display
    /// string — two keys sharing one label rendered as two identical rows in the UI, which reads
    /// as a bug and wastes the clinician's attention on the one surface where attention is the
    /// scarce resource.
    /// </summary>
    private sealed record Rule(
        string WorkingCategory,
        string AlternativeCode,
        string AlternativeDisplay,
        (string Key, string Display)[] Discriminators,
        DiagnosticHarmIfMissed Harm,
        string Citation);

    private static readonly Rule[] Rules =
    {
        new("R42", "I63", "Cerebral infarction (posterior circulation)",
            new[] { ("E:HINTS", "HINTS exam"), ("R:70553", "MRI brain with DWI") },
            DiagnosticHarmIfMissed.Critical,
            "AHRQ 2022 ED diagnostic error review — stroke is the most frequently missed " +
            "dangerous cause of dizziness; HINTS outperforms early MRI in acute vestibular syndrome."),

        new("R07", "I21", "Acute myocardial infarction",
            new[] { ("L:89579-7", "High-sensitivity troponin"), ("E:ECG12", "12-lead ECG") },
            DiagnosticHarmIfMissed.Critical,
            "SIDM 'Big Three' — vascular events. Missed MI is a leading source of serious " +
            "diagnostic harm in undifferentiated chest pain."),

        new("N39", "A41", "Sepsis, unspecified organism",
            new[] { ("L:32693-4", "Lactate"), ("L:600-7", "Blood culture") },
            DiagnosticHarmIfMissed.Critical,
            "SIDM 'Big Three' — infections. Urinary source with systemic features is a common " +
            "path to under-recognised sepsis, particularly in older adults."),

        new("M54", "C79", "Metastatic disease of spine",
            new[] { ("L:30341-2", "ESR"), ("R:72148", "MRI lumbar spine") },
            DiagnosticHarmIfMissed.Critical,
            "AHRQ — 'red flag' back pain. Cancer and epidural abscess are the two time-critical " +
            "causes hidden inside a very high-volume benign presentation."),

        new("G43", "I60", "Subarachnoid haemorrhage",
            new[] { ("R:70450", "Non-contrast head CT"), ("E:OTTAWA-SAH", "Ottawa SAH rule") },
            DiagnosticHarmIfMissed.Critical,
            "AHRQ 2022 — headache. Thunderclap onset and age over 40 are the discriminating " +
            "features most often absent from a migraine workup."),

        new("J18", "I50", "Heart failure",
            new[] { ("L:33762-6", "NT-proBNP") },
            DiagnosticHarmIfMissed.Serious,
            "Dyspnoea with an infiltrate is frequently treated as pneumonia when the driver is " +
            "cardiogenic; natriuretic peptide separates them."),

        new("R10", "K35", "Acute appendicitis",
            new[] { ("R:74177", "CT abdomen/pelvis with contrast") },
            DiagnosticHarmIfMissed.Serious,
            "SIDM — abdominal pain is the highest-volume ED presentation with a time-critical " +
            "surgical alternative."),

        new("J45", "I50", "Heart failure",
            new[] { ("L:33762-6", "NT-proBNP"), ("R:71046", "Chest radiograph") },
            DiagnosticHarmIfMissed.Serious,
            "'Cardiac asthma' — wheeze from pulmonary congestion treated as reactive airway disease."),

        new("K21", "I20", "Angina pectoris",
            new[] { ("L:89579-7", "High-sensitivity troponin"), ("E:ECG12", "12-lead ECG") },
            DiagnosticHarmIfMissed.Critical,
            "Reflux and ischaemic chest pain overlap substantially; symptomatic response to " +
            "antacids does not exclude ischaemia."),

        new("F32", "E03", "Hypothyroidism",
            new[] { ("L:3016-3", "TSH") },
            DiagnosticHarmIfMissed.Routine,
            "Reversible endocrine cause of a depressive presentation; standard first-line screen."),

        new("F41", "E05", "Thyrotoxicosis",
            new[] { ("L:3016-3", "TSH") },
            DiagnosticHarmIfMissed.Routine,
            "Palpitations and anxiety symptoms from thyrotoxicosis are commonly attributed to " +
            "primary anxiety."),

        new("R55", "I49", "Cardiac arrhythmia",
            new[] { ("E:ECG12", "12-lead ECG"), ("E:ORTHOSTATICS", "Orthostatic vital signs") },
            DiagnosticHarmIfMissed.Serious,
            "Cardiac syncope carries materially higher mortality than vasovagal syncope and is " +
            "separated largely by ECG."),

        new("R06", "I26", "Pulmonary embolism",
            new[] { ("L:48065-7", "D-dimer"), ("R:71275", "CT pulmonary angiogram") },
            DiagnosticHarmIfMissed.Critical,
            "SIDM 'Big Three' — vascular. Wells/PERC exist because unexplained dyspnoea is where " +
            "PE hides."),

        new("N20", "I71", "Aortic aneurysm/dissection",
            new[] { ("R:74178", "CT angiogram abdomen/pelvis") },
            DiagnosticHarmIfMissed.Critical,
            "Flank pain attributed to renal colic in an older patient is a classic presentation " +
            "of a leaking abdominal aortic aneurysm."),

        new("K59", "C18", "Malignant neoplasm of colon",
            new[] { ("L:2335-8", "Faecal occult blood"), ("R:45378", "Colonoscopy") },
            DiagnosticHarmIfMissed.Serious,
            "SIDM 'Big Three' — cancers. New constipation with anaemia in an adult over 50 " +
            "warrants exclusion of malignancy."),

        new("M25", "M00", "Septic arthritis",
            new[] { ("L:30341-2", "ESR"), ("L:1988-5", "CRP"), ("E:ARTHROCENTESIS", "Joint aspiration") },
            DiagnosticHarmIfMissed.Critical,
            "A hot joint treated as a flare of osteoarthritis loses the window in which septic " +
            "arthritis is salvageable."),

        new("R51", "G93", "Intracranial mass/raised pressure",
            new[] { ("R:70450", "Non-contrast head CT"), ("E:FUNDOSCOPY", "Fundoscopy") },
            DiagnosticHarmIfMissed.Serious,
            "Progressive or postural headache with a normal neurological exam still warrants " +
            "imaging when red flags are present."),

        new("E86", "E27", "Adrenal insufficiency",
            new[] { ("L:2143-6", "Morning cortisol"), ("L:2951-2", "Serum sodium") },
            DiagnosticHarmIfMissed.Serious,
            "Recurrent dehydration with hyponatraemia is a recognised presentation of " +
            "undiagnosed adrenal insufficiency.")
    };

    static DiagnosticRevisionCatalog()
    {
        // Duplicate (working category → alternative) pairs would double-render the same advice.
        var seen = new HashSet<(string, string)>();
        foreach (Rule r in Rules)
            if (!seen.Add((r.WorkingCategory, r.AlternativeCode)))
                throw new InvalidOperationException(
                    $"Duplicate diagnostic revision rule: {r.WorkingCategory} → {r.AlternativeCode}");
    }

    /// <summary>
    /// Curated alternatives and tests for a working diagnosis code, matched on its 3-character
    /// ICD-10 category. Returns empty when nothing is curated — which is the common case and is
    /// correct: the catalog is deliberately small enough that every line can be defended.
    /// </summary>
    public static (List<DiagnosisAlternative> Alternatives, List<DiagnosticTestSuggestion> Tests)
        GetBaseline(string? workingCode)
    {
        string cat = DiagnosisCodeRelation.Category3(workingCode);
        var alternatives = new List<DiagnosisAlternative>();
        var tests = new List<DiagnosticTestSuggestion>();
        if (cat.Length == 0) return (alternatives, tests);

        foreach (Rule r in Rules)
        {
            if (!string.Equals(r.WorkingCategory, cat, StringComparison.Ordinal)) continue;

            alternatives.Add(new DiagnosisAlternative
            {
                Code = r.AlternativeCode,
                Display = r.AlternativeDisplay,
                // No counts: the baseline states that this pairing exists and is dangerous, not
                // how often it happens here. Inventing a frequency would be fabricating evidence.
                Count = 0,
                OutOf = 0,
                FromBaseline = true,
                Harm = r.Harm,
                Citation = r.Citation
            });

            foreach ((string key, string display) in r.Discriminators)
            {
                // Several rules can cite the same test (12-lead ECG appears under chest pain,
                // reflux and syncope). One row per test, not one per rule.
                if (tests.Any(t => t.TestKey == key)) continue;

                tests.Add(new DiagnosticTestSuggestion
                {
                    TestKey = key,
                    Display = display,
                    Kind = KindOf(key),
                    Verdict = SignalVerdict.Insufficient,
                    FromBaseline = true,
                    Harm = r.Harm,
                    Citation = r.Citation
                });
            }
        }

        return (alternatives, tests);
    }

    /// <summary>
    /// Merge curated baseline with locally learned content. Pure function — no I/O, no clock.
    ///
    /// Merge rules, in order of what they protect:
    ///  1. Every Critical baseline line survives, regardless of local counts. The floors gate
    ///     the learned percentage, never the curated arrow.
    ///  2. Where local data names the same alternative, the learned counts are attached to the
    ///     baseline line rather than duplicating it — so the clinician sees one row carrying
    ///     both the citation and this site's experience.
    ///  3. Ordering is by harm first, then by local count. Rarity must not bury lethality.
    /// </summary>
    public static DiagnosisRevisionAdvisory Merge(
        DiagnosisRevisionAdvisory learned,
        string? workingCode,
        DateTime generatedAt)
    {
        (List<DiagnosisAlternative> baseAlts, List<DiagnosticTestSuggestion> baseTests) =
            GetBaseline(workingCode);

        var mergedAlts = new List<DiagnosisAlternative>();
        var learnedByCode = learned.Alternatives
            .GroupBy(a => DiagnosisCodeRelation.Category3(a.Code))
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        foreach (DiagnosisAlternative b in baseAlts)
        {
            string key = DiagnosisCodeRelation.Category3(b.Code);
            if (learnedByCode.TryGetValue(key, out DiagnosisAlternative? local))
            {
                b.Count = local.Count;
                b.OutOf = local.OutOf;
                learnedByCode.Remove(key);
            }
            mergedAlts.Add(b);
        }
        mergedAlts.AddRange(learnedByCode.Values);

        var mergedTests = new List<DiagnosticTestSuggestion>(learned.SuggestedTests);
        var haveKeys = new HashSet<string>(mergedTests.Select(t => t.TestKey), StringComparer.Ordinal);
        foreach (DiagnosticTestSuggestion t in baseTests)
            if (haveKeys.Add(t.TestKey))
                mergedTests.Add(t);

        learned.Alternatives = mergedAlts
            .OrderByDescending(a => (int)a.Harm)
            .ThenByDescending(a => a.Count)
            .ToList();
        learned.SuggestedTests = mergedTests
            .OrderByDescending(t => (int)t.Harm)
            .ThenByDescending(t => t.Lift ?? 0)
            .ToList();
        learned.GeneratedAt = generatedAt;
        return learned;
    }

    private static DiagnosticTestKind KindOf(string key) => key.Length > 1
        ? key[0] switch
        {
            'L' => DiagnosticTestKind.Lab,
            'R' => DiagnosticTestKind.Imaging,
            'C' => DiagnosticTestKind.Consult,
            'E' => DiagnosticTestKind.Exam,
            _ => DiagnosticTestKind.Unspecified
        }
        : DiagnosticTestKind.Unspecified;
}
