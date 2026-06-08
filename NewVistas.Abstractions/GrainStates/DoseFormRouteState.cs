// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// An RxNorm Dose Form Group (TTY=DFG) — a grouping of dose forms related by
/// route of administration (e.g. "Oral Product", "Injectable Product") or by
/// physical form (e.g. "Pill"). RxNorm publishes ~50 dose form groups.
///
/// RxNorm does not ship a clean machine-readable "DFG → allowed routes" table;
/// the association is implied by the group's clinical semantics. The
/// <see cref="ValidVistaRoutes"/> list is therefore a CURATED mapping onto the
/// canonical VistA Standard Medication Routes (File #51.23) so that every
/// downstream consumer (pharmacy, MAR, order entry) keeps a single route
/// vocabulary. This is the one place clinical judgment lives.
/// </summary>
[GenerateSerializer]
public class DoseFormGroup
{
    /// <summary>
    /// RxNorm dose form group name (e.g. "Oral Product", "Injectable Product").
    /// </summary>
    [Id(0)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// RxNorm concept unique identifier (RxCUI) for the dose form group, when known.
    /// </summary>
    [Id(1)]
    public string? RxCui { get; set; }

    /// <summary>
    /// Curated set of valid VistA route names (File #51.23, field .01) for this
    /// dose form group. Names must match File #51.23 exactly (e.g. "ORAL",
    /// "INTRAVENOUS", "INTRA-ARTERIAL", "NASAL").
    /// </summary>
    [Id(2)]
    public List<string> ValidVistaRoutes { get; set; } = new();
}

/// <summary>
/// An RxNorm Dose Form (TTY=DF) — a specific physical formulation such as
/// "Oral Tablet" or "Injectable Solution". RxNorm publishes ~150 dose forms.
/// Each dose form belongs to one or more dose form groups (TTY=DFG).
/// </summary>
[GenerateSerializer]
public class DoseFormEntry
{
    /// <summary>
    /// RxNorm dose form name (e.g. "Oral Tablet", "Injectable Solution").
    /// </summary>
    [Id(0)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// RxNorm concept unique identifier (RxCUI) for the dose form, when known.
    /// </summary>
    [Id(1)]
    public string? RxCui { get; set; }

    /// <summary>
    /// Names of the dose form group(s) this dose form belongs to
    /// (RxNorm <c>has_dose_form_group</c> relationship).
    /// </summary>
    [Id(2)]
    public List<string> DoseFormGroupNames { get; set; } = new();
}

/// <summary>
/// Persistent state for the DoseFormRouteIndexGrain singleton
/// (key: "DOSE-FORM-ROUTE-INDEX"). Self-seeded from embedded RxNorm-derived
/// tables on first activation — no admin load or internet access required.
///
/// The dose-form / dose-form-group / route vocabulary is small (~200 rows) and
/// stable (changes a few times per year), so it ships embedded. An optional
/// RxNav refresh may later update the DF→DFG and VistA-form→DF bridges; the
/// curated DFG→route mapping is human-owned and never overwritten.
/// </summary>
[GenerateSerializer]
public class DoseFormRouteIndexState
{
    /// <summary>Dose form groups keyed by group name (uppercase).</summary>
    [Id(0)]
    public Dictionary<string, DoseFormGroup> GroupsByName { get; set; } = new();

    /// <summary>Dose forms keyed by dose form name (uppercase).</summary>
    [Id(1)]
    public Dictionary<string, DoseFormEntry> FormsByName { get; set; } = new();

    /// <summary>
    /// Bridge from a VistA dose form / dispense unit string (uppercase) to one or
    /// more RxNorm dose form names. VistA forms are coarse (e.g. "TABLET",
    /// "INJECTION"); this maps them onto the finer RxNorm dose form vocabulary.
    /// A form maps to MULTIPLE RxNorm dose forms when it is genuinely ambiguous
    /// (e.g. "SUPPOSITORY" → rectal and vaginal), in which case the valid routes
    /// are the union across all plausible forms — avoiding false warnings.
    /// </summary>
    [Id(2)]
    public Dictionary<string, List<string>> VistaFormToRxNormForm { get; set; } = new();

    /// <summary>
    /// True once the grain has been seeded from the embedded tables.
    /// Prevents re-seeding on subsequent activations.
    /// </summary>
    [Id(3)]
    public bool IsLoaded { get; set; }

    /// <summary>
    /// Provenance of the currently loaded data (e.g. "embedded-2026" or an
    /// RxNav release marker after a refresh).
    /// </summary>
    [Id(4)]
    public string SourceVersion { get; set; } = string.Empty;

    /// <summary>UTC timestamp of the last seed or refresh.</summary>
    [Id(5)]
    public DateTime? LastRefreshedUtc { get; set; }
}

/// <summary>
/// Outcome of a route-vs-dose-form validation check.
/// </summary>
[GenerateSerializer]
public enum RouteValidationOutcome
{
    /// <summary>
    /// Route is valid for the dose form, or the dose form is unknown/unmapped
    /// (fail-open — the check never blocks an order it cannot evaluate).
    /// </summary>
    Valid = 0,

    /// <summary>
    /// Route does not match any valid route for the dose form. The order is
    /// still accepted (warn-only), but a warning is recorded for clinician review.
    /// </summary>
    Warn = 1,
}

/// <summary>
/// Result of validating a route of administration against a drug's dose form.
/// Returned by <c>IRouteValidationService</c>. Warn-only by design: a mismatch
/// produces <see cref="RouteValidationOutcome.Warn"/> with suggested routes,
/// never a hard block (clinicians legitimately override, e.g. crushing a tablet
/// for an enteral feeding tube).
/// </summary>
[GenerateSerializer]
public class RouteValidationResult
{
    /// <summary>The validation outcome.</summary>
    [Id(0)]
    public RouteValidationOutcome Outcome { get; set; } = RouteValidationOutcome.Valid;

    /// <summary>Human-readable advisory message when the outcome is Warn; null otherwise.</summary>
    [Id(1)]
    public string? Message { get; set; }

    /// <summary>Valid routes for the dose form, offered as suggestions on a Warn.</summary>
    [Id(2)]
    public List<string> SuggestedRoutes { get; set; } = new();

    /// <summary>The dose form the route was evaluated against (null if unresolved).</summary>
    [Id(3)]
    public string? DoseForm { get; set; }

    /// <summary>The route that was evaluated.</summary>
    [Id(4)]
    public string? Route { get; set; }

    /// <summary>Convenience factory for a passing result.</summary>
    public static RouteValidationResult Valid(string? doseForm = null, string? route = null) =>
        new() { Outcome = RouteValidationOutcome.Valid, DoseForm = doseForm, Route = route };
}
