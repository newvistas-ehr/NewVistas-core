// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.Clinical;

/// <summary>Body-system grouping for a catalog symptom (drives the survey's section layout).</summary>
[GenerateSerializer]
public enum SymptomCategory
{
    Constitutional = 0,
    Respiratory = 1,
    Cardiovascular = 2,
    Gastrointestinal = 3,
    Neurological = 4,
    Sensory = 5,
    Dermatologic = 6,
    Musculoskeletal = 7,
    Psychiatric = 8
}

/// <summary>
/// One coded symptom in the closed surveillance vocabulary. The catalog is the ONLY source of
/// symptom codes — prevalence math is meaningless over free-text, so observations and proto
/// features must reference a catalog <see cref="Code"/>.
/// </summary>
[GenerateSerializer]
public record SymptomCatalogEntry
{
    /// <summary>SNOMED CT concept id — the primary key of the symptom.</summary>
    [Id(0)] public string Code { get; init; } = string.Empty;
    /// <summary>ICD-10-CM cross-reference (mostly the R-chapter "signs &amp; symptoms").</summary>
    [Id(1)] public string Icd10 { get; init; } = string.Empty;
    /// <summary>Human-readable display name (question text on the survey).</summary>
    [Id(2)] public string Display { get; init; } = string.Empty;
    /// <summary>Body-system grouping.</summary>
    [Id(3)] public SymptomCategory Category { get; init; }
    /// <summary>True if this symptom is part of the default "wide net" asked of every screened patient.</summary>
    [Id(4)] public bool IsCoreScreen { get; init; }
    /// <summary>
    /// Curated background prevalence (0..1) — the analytics FALLBACK denominator when the live
    /// assessed population is too thin to estimate a rate. Illustrative, adult general population.
    /// </summary>
    [Id(5)] public double BackgroundPrevalence { get; init; }
}

/// <summary>
/// Curated catalog of coded symptoms for emerging-condition surveillance — the "wide net" of
/// review-of-systems questions the front door asks when a patient's illness has no name yet.
///
/// Deterministic and static, in the same "model the data, curate the rules" house style as
/// <see cref="Pharmacogenomics"/> / <see cref="PrecisionOncology"/>. This is a representative
/// review-of-systems set (SNOMED-keyed, ICD-10-R cross-referenced), NOT the full SNOMED
/// findings hierarchy. It exists because NO coded symptom/ROS capture surface existed anywhere
/// in the system — chief complaint / HPI are free text, so a symptom a clinician never charts
/// (early anosmia) is invisible rather than "not asked", and the matcher would starve.
///
/// The <see cref="BackgroundPrevalence"/> values are illustrative fallbacks only; the analytics
/// engine prefers a rate computed from the live assessed population when one is available.
/// </summary>
public static class SymptomCatalog
{
    // SNOMED code, ICD-10 cross-ref, display, category, core-screen, background prevalence.
    private static readonly SymptomCatalogEntry[] Entries =
    {
        // ── Constitutional ────────────────────────────────────────────────────
        Entry("386661006", "R50.9",  "Fever",              SymptomCategory.Constitutional, true,  0.030),
        Entry("43724002",  "R68.83", "Chills",             SymptomCategory.Constitutional, true,  0.020),
        Entry("84229001",  "R53.83", "Fatigue",            SymptomCategory.Constitutional, true,  0.100),
        Entry("42984000",  "R61",    "Night sweats",       SymptomCategory.Constitutional, false, 0.015),
        Entry("79890006",  "R63.0",  "Loss of appetite",   SymptomCategory.Constitutional, false, 0.040),
        Entry("89362005",  "R63.4",  "Unintended weight loss", SymptomCategory.Constitutional, false, 0.020),

        // ── Respiratory ───────────────────────────────────────────────────────
        Entry("49727002",  "R05.9",  "Cough",              SymptomCategory.Respiratory, true,  0.060),
        Entry("267036007", "R06.00", "Shortness of breath", SymptomCategory.Respiratory, true,  0.040),
        Entry("56018004",  "R06.2",  "Wheezing",           SymptomCategory.Respiratory, false, 0.020),
        Entry("267101005", "R09.81", "Runny nose",         SymptomCategory.Respiratory, true,  0.070),
        Entry("68235000",  "J34.89", "Nasal congestion",   SymptomCategory.Respiratory, true,  0.070),
        Entry("267102003", "R07.0",  "Sore throat",        SymptomCategory.Respiratory, true,  0.050),
        Entry("66857006",  "R04.2",  "Coughing up blood",  SymptomCategory.Respiratory, false, 0.003),

        // ── Cardiovascular ────────────────────────────────────────────────────
        Entry("29857009",  "R07.9",  "Chest pain",         SymptomCategory.Cardiovascular, true,  0.030),
        Entry("80313002",  "R00.2",  "Palpitations",       SymptomCategory.Cardiovascular, false, 0.020),
        Entry("267038008", "R60.9",  "Swelling (edema)",   SymptomCategory.Cardiovascular, false, 0.025),
        Entry("271594007", "R55",    "Fainting (syncope)", SymptomCategory.Cardiovascular, false, 0.008),

        // ── Gastrointestinal ──────────────────────────────────────────────────
        Entry("422587007", "R11.0",  "Nausea",             SymptomCategory.Gastrointestinal, true,  0.040),
        Entry("422400008", "R11.10", "Vomiting",           SymptomCategory.Gastrointestinal, false, 0.020),
        Entry("62315008",  "R19.7",  "Diarrhea",           SymptomCategory.Gastrointestinal, true,  0.030),
        Entry("21522001",  "R10.9",  "Abdominal pain",     SymptomCategory.Gastrointestinal, false, 0.035),

        // ── Neurological ──────────────────────────────────────────────────────
        Entry("25064002",  "R51.9",  "Headache",           SymptomCategory.Neurological, true,  0.080),
        Entry("404640003", "R42",    "Dizziness",          SymptomCategory.Neurological, false, 0.030),
        Entry("40917007",  "R41.0",  "Confusion",          SymptomCategory.Neurological, true,  0.010),
        Entry("91019004",  "R20.2",  "Numbness or tingling", SymptomCategory.Neurological, false, 0.020),
        Entry("26544005",  "M62.81", "Muscle weakness",    SymptomCategory.Neurological, false, 0.020),

        // ── Sensory (the wide-net "did you ask?" axis) ────────────────────────
        Entry("44169009",  "R43.0",  "Loss of smell",      SymptomCategory.Sensory, true,  0.020),
        Entry("36955009",  "R43.2",  "Loss of taste",      SymptomCategory.Sensory, true,  0.020),
        Entry("15188001",  "H91.90", "Hearing change",     SymptomCategory.Sensory, true,  0.100),
        Entry("60862001",  "H93.19", "Ringing in the ears (tinnitus)", SymptomCategory.Sensory, false, 0.080),
        Entry("246636008", "H53.8",  "Vision change",      SymptomCategory.Sensory, false, 0.060),
        Entry("9826008",   "H10.9",  "Red or irritated eyes", SymptomCategory.Sensory, false, 0.025),

        // ── Dermatologic ──────────────────────────────────────────────────────
        Entry("271807003", "R21",    "Rash",               SymptomCategory.Dermatologic, true,  0.030),
        Entry("162290004", "R23.0",  "Bluish skin (cyanosis)", SymptomCategory.Dermatologic, false, 0.004),

        // ── Musculoskeletal ───────────────────────────────────────────────────
        Entry("68962001",  "M79.10", "Muscle aches",       SymptomCategory.Musculoskeletal, true,  0.050),
        Entry("57676002",  "M25.50", "Joint pain",         SymptomCategory.Musculoskeletal, false, 0.045),

        // ── Psychiatric ───────────────────────────────────────────────────────
        Entry("48694002",  "R45.0",  "Anxiety",            SymptomCategory.Psychiatric, false, 0.060),
        Entry("193462001", "G47.00", "Difficulty sleeping", SymptomCategory.Psychiatric, false, 0.070),
    };

    private static SymptomCatalogEntry Entry(string code, string icd10, string display,
        SymptomCategory category, bool coreScreen, double backgroundPrevalence) => new()
    {
        Code = code,
        Icd10 = icd10,
        Display = display,
        Category = category,
        IsCoreScreen = coreScreen,
        BackgroundPrevalence = backgroundPrevalence
    };

    private static readonly Dictionary<string, SymptomCatalogEntry> ByCode =
        Entries.ToDictionary(e => e.Code, StringComparer.Ordinal);

    static SymptomCatalog()
    {
        // Uniqueness guard — a duplicate SNOMED code would silently collapse two symptoms.
        if (ByCode.Count != Entries.Length)
            throw new InvalidOperationException("SymptomCatalog contains duplicate symptom codes.");
    }

    /// <summary>All catalog symptoms, ordered by body system then display name.</summary>
    public static IReadOnlyList<SymptomCatalogEntry> All { get; } =
        Entries.OrderBy(e => e.Category).ThenBy(e => e.Display, StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>The default wide-net screen — every core-screen symptom.</summary>
    public static IReadOnlyList<SymptomCatalogEntry> CoreScreen { get; } =
        All.Where(e => e.IsCoreScreen).ToList();

    /// <summary>True if the code is part of the closed surveillance vocabulary.</summary>
    public static bool Contains(string? code) =>
        !string.IsNullOrWhiteSpace(code) && ByCode.ContainsKey(code);

    /// <summary>The catalog entry for a code, or null if the code is not in the vocabulary.</summary>
    public static SymptomCatalogEntry? TryGet(string? code) =>
        code is not null && ByCode.TryGetValue(code, out SymptomCatalogEntry? e) ? e : null;

    /// <summary>Display name for a code, or the raw code if unknown (defensive).</summary>
    public static string DisplayFor(string code) => TryGet(code)?.Display ?? code;

    /// <summary>Curated background prevalence for a code, or 0 if unknown.</summary>
    public static double BackgroundPrevalenceFor(string code) => TryGet(code)?.BackgroundPrevalence ?? 0.0;

    /// <summary>
    /// Resolves the survey question set: the core wide-net screen UNION the symptom features of the
    /// currently active proto-conditions (passed as their catalog codes). Unknown codes are ignored
    /// (closed vocabulary). Ordered by body system then display name so the survey groups cleanly.
    ///
    /// The proto codes are supplied by the caller (workflow/UI) rather than proto objects, keeping the
    /// catalog dependency-free — a proto's symptom-kind feature ids ARE catalog codes.
    /// </summary>
    public static List<SymptomCatalogEntry> BuildSurveyQuestionSet(IEnumerable<string>? extraSymptomCodes = null)
    {
        var codes = new HashSet<string>(CoreScreen.Select(e => e.Code), StringComparer.Ordinal);
        if (extraSymptomCodes is not null)
            foreach (string code in extraSymptomCodes)
                if (Contains(code)) codes.Add(code);

        return All.Where(e => codes.Contains(e.Code)).ToList();
    }
}
