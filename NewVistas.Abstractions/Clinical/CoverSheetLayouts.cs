// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Clinical;

/// <summary>A named cover-sheet layout — an ordered set of sections a specialty leads with.</summary>
public sealed record CoverSheetLayout(
    string Id, string Name, string Description, IReadOnlyList<CoverSheetSectionSpec> Sections);

/// <summary>
/// PROTOTYPE cover-sheet layout registry — the cover sheet as a composition. Three shipped layouts
/// (General / Oncology / Procedural); each is DATA (an ordered section list), not code. A layout's
/// sections are rendered on top of a non-suppressible safety spine (demographics/CWAD/allergies) that
/// the assembler always adds. Production would move these to site parameters with system→division→
/// service→user precedence. See Docs/Domain/SPECIALTY_COVERSHEET_PROTOTYPE.md.
/// </summary>
public static class CoverSheetLayouts
{
    public const string General = "general";
    public const string Oncology = "oncology";
    public const string Procedural = "procedural";

    private static CoverSheetSectionSpec S(string key, string title, bool prominent = false, int max = 5)
        => new() { SectionKey = key, Title = title, Prominent = prominent, MaxItems = max };

    private static readonly CoverSheetLayout GeneralLayout = new(
        General, "General (Primary Care)",
        "Longitudinal overview — problems, meds, health maintenance, trends. Everything, shallow.",
        new[]
        {
            S(CoverSheetSections.Problems,    "Active problems", prominent: true, max: 8),
            S(CoverSheetSections.Medications, "Active medications", max: 6),
            S(CoverSheetSections.Reminders,   "Health maintenance reminders", max: 6),
            S(CoverSheetSections.Labs,        "Recent labs", max: 5),
            S(CoverSheetSections.Vitals,      "Recent vitals", max: 5),
            S(CoverSheetSections.Visits,      "Recent visits", max: 5),
            S(CoverSheetSections.Orders,      "Active orders", max: 5),
            S(CoverSheetSections.Consults,    "Active consults", max: 5),
        });

    private static readonly CoverSheetLayout OncologyLayout = new(
        Oncology, "Oncology",
        "Cancer-oriented — diagnosis/stage, regimen, molecular profile & matched therapy, chemo labs.",
        new[]
        {
            S(CoverSheetSections.Oncology,    "Oncology", prominent: true, max: 5),
            S(CoverSheetSections.Pgx,         "Pharmacogenomics alerts", prominent: true, max: 6),
            S(CoverSheetSections.Medications, "Active medications", max: 6),
            S(CoverSheetSections.Labs,        "Recent labs (chemo)", max: 6),
            S(CoverSheetSections.Imaging,     "Latest imaging (staging)", max: 3),
            S(CoverSheetSections.Problems,    "Active problems", max: 5),
            S(CoverSheetSections.Vitals,      "Recent vitals", max: 5),
        });

    private static readonly CoverSheetLayout ProceduralLayout = new(
        Procedural, "Procedural (Surgery)",
        "Procedure-anchored — the upcoming case, the relevant image, anticoagulation, pre-op labs.",
        new[]
        {
            S(CoverSheetSections.Procedures,  "Upcoming procedures", prominent: true, max: 3),
            S(CoverSheetSections.Imaging,     "Latest imaging", prominent: true, max: 3),
            S(CoverSheetSections.Medications, "Active medications (incl. anticoagulants)", max: 8),
            S(CoverSheetSections.Problems,    "Active problems", max: 6),
            S(CoverSheetSections.Labs,        "Recent labs (pre-op)", max: 6),
            S(CoverSheetSections.Vitals,      "Recent vitals", max: 5),
            S(CoverSheetSections.Pgx,         "Pharmacogenomics alerts", max: 5),
        });

    /// <summary>All layouts, in picker order.</summary>
    public static IReadOnlyList<CoverSheetLayout> All { get; } = new[] { GeneralLayout, OncologyLayout, ProceduralLayout };

    /// <summary>Resolve a layout by id (defaults to General for unknown/empty).</summary>
    public static CoverSheetLayout Resolve(string? id)
        => All.FirstOrDefault(l => string.Equals(l.Id, id, StringComparison.OrdinalIgnoreCase)) ?? GeneralLayout;

    /// <summary>
    /// PER-PATIENT-THEN-VIEWER resolution. The patient's clinical context decides what's *relevant*
    /// (which layouts are available) and the loudest default (active cancer → Oncology, outranking an
    /// elective surgery; else upcoming surgery → Procedural; else General). The VIEWER's role/specialty
    /// then picks the *lens* — but only among the layouts the patient's context already made relevant,
    /// so a surgeon viewing a patient with no surgery falls back to the patient-loudest default. The
    /// patient's loudest concern still intrudes via the context banner regardless of the chosen lens.
    /// Returns the layout id and a human-readable reason.
    /// </summary>
    public static (string LayoutId, string Reason) ResolveDefault(
        bool hasActiveCancer, bool hasUpcomingSurgery, string? viewerRole)
    {
        string patientLoudest = hasActiveCancer ? Oncology : hasUpcomingSurgery ? Procedural : General;

        // The SPECIALTY layouts the patient's context makes relevant. General is the baseline — never a
        // viewer override, so a generalist lens can't bury a specialty concern (a PCP viewing a cancer
        // patient still leads with Oncology). The viewer only reorders among the patient's own specialty
        // concerns (e.g. a surgeon on a cancer-AND-surgery patient → Procedural, with the cancer banner).
        var relevantSpecialty = new List<string>();
        if (hasActiveCancer) relevantSpecialty.Add(Oncology);
        if (hasUpcomingSurgery) relevantSpecialty.Add(Procedural);

        string? viewerPref = MapViewerRole(viewerRole);
        if (viewerPref is not null && viewerPref != General
            && relevantSpecialty.Contains(viewerPref) && viewerPref != patientLoudest)
            return (viewerPref, $"Auto — {NameOf(viewerPref)} lens (viewer) over {NameOf(patientLoudest)} (patient's loudest)");

        string reason = hasActiveCancer ? "Auto — patient has active oncology"
            : hasUpcomingSurgery ? "Auto — upcoming procedure scheduled"
            : "Auto — general (no dominant context)";
        if (viewerPref is not null && viewerPref == patientLoudest)
            reason += $"; matches {NameOf(viewerPref)} viewer";
        return (patientLoudest, reason);
    }

    /// <summary>
    /// Maps a viewer's role / service / specialty free-text to a layout lens (or null if it doesn't
    /// map). Sourced from the provider record (File #200 ServiceSection/Specialty) — a provider
    /// attribute, independent of the unified-person identity concept.
    /// </summary>
    public static string? MapViewerRole(string? viewerRole)
    {
        if (string.IsNullOrWhiteSpace(viewerRole)) return null;
        string v = viewerRole.ToLowerInvariant();
        if (v.Contains("surg") || v.Contains("ortho") || v.Contains("procedur") || v.Contains("anesth"))
            return Procedural;
        if (v.Contains("onco") || v.Contains("hemat") || v.Contains("cancer") || v.Contains("radiation"))
            return Oncology;
        if (v.Contains("primary") || v.Contains("family") || v.Contains("internal") || v.Contains("general")
            || v.Contains("medicine") || v.Contains("pcp") || v.Contains("geriatric"))
            return General;
        return null;
    }

    private static string NameOf(string id) => Resolve(id).Name;
}
