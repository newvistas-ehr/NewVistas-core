// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Text.RegularExpressions;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Services;

namespace NewVistas.Abstractions.Clinical;

/// <summary>
/// Deterministically turns a <see cref="ClinicalClaim"/> into ICD-10 candidates using the
/// site's own index. This is the half of the suggester that owns CODING RULES, and it is pure
/// so those rules are unit-testable:
///
///  * the assistant (lexicon or model) decides WHAT the note says;
///  * this class decides WHICH CODES that maps to — and the model is never consulted.
///
/// The modifier routing works on the index descriptions themselves rather than on hand-kept
/// code ranges: a FamilyMember claim keeps only "family history …" entries (Z82.62, not
/// M81.0), a History claim keeps only "personal history …" entries (Z85.820, not C43.x), and
/// a current-patient claim excludes both. That way the routing is correct for every code the
/// CMS file contains, not just the ones someone remembered to enumerate.
///
/// The history branches additionally require the description to state a history OF THE TERM
/// ITSELF, not of a narrower condition that merely contains the term as a substring — see
/// <see cref="BuildHistoryOfTermRegex"/> for the trap (Z86.32, gestational diabetes) that
/// made this necessary.
/// </summary>
public static class ClaimToCodeResolver
{
    /// <summary>
    /// Candidates to pull from the index per search term, before filtering. Originally sized
    /// from the CMS file itself, because the index returned matches in code order and any cap
    /// below a term's whole description-family starved the ranking: "osteoporosis" appears in
    /// 360 descriptions (the M80.x fracture-encounter herd sorts ahead of M81.0 and of
    /// Z82.62) and "diabetes" in 642. Found live against the full file — a 30-row window
    /// never even fetched the family-history code, and 200 still ranked a fracture-sequela
    /// code first because M81.0 was beyond the page. But no size fixed that class of bug:
    /// "fracture" matches 20,365 rows, beyond ANY sane window.
    ///
    /// The fetch now uses <c>SearchRankedAsync</c>, which applies the first two ranking tiers
    /// of <see cref="SelectCandidates"/> (description-starts-with, then shortest code) over
    /// ALL matches BEFORE this cap — so the window holds the best-ranked slice of the whole
    /// corpus and can no longer starve at any term frequency. The constant survives as a
    /// working-set bound only: big enough for the modifier/laterality filtering below to
    /// arbitrate over a well-ranked pool, no longer required to contain a code-ordered
    /// description-family.
    /// </summary>
    public const int MaxCandidatesToFetch = 800;

    /// <summary>Suggestions surfaced per claim (3 when bilateral, so both sides can show).</summary>
    public const int MaxSuggestionsPerClaim = 2;

    /// <summary>
    /// Terms whose own text is a poor substring for the right default code. Kept tiny and
    /// individually defensible:
    ///  * "hypertension" alone ranks exotic variants above I10, whose description is
    ///    "Essential (primary) hypertension";
    ///  * plain "diabetes mellitus" with no stated type defaults to the E11 family under the
    ///    AHA coding guideline, whose unspecified-complication entry is the E11.9 description.
    /// The clinician always sees the full official description and can decline.
    /// </summary>
    private static readonly Dictionary<string, string> PreferredPhrase =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["hypertension"] = "essential (primary) hypertension",
            ["diabetes"] = "type 2 diabetes mellitus without complications",
            // ICD words the sides differently by joint family: M79.60x ends "…arm, unspecified"
            // (so the plain term substring-matches) but M25.519 is "Pain in unspecified
            // shoulder" — "pain in shoulder" is a substring of neither, and the claim went
            // unmapped live. The laterality-aware phrase in BuildSearchTerms handles the sided
            // cases; this covers the unsided one.
            ["pain in shoulder"] = "pain in unspecified shoulder",
        };

    private static readonly Regex LeftRx = new(@"\bleft\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RightRx = new(@"\bright\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Ordered search terms for a claim. The caller queries the index with each in turn and
    /// stops at the first that yields candidates surviving <see cref="SelectCandidates"/>.
    /// </summary>
    public static IReadOnlyList<string> BuildSearchTerms(ClinicalClaim claim)
    {
        var terms = new List<string>();

        // Modifier-aware retrieval first: "family history of osteoporosis" finds Z82.62
        // directly instead of hoping it survives a code-ordered fetch window dominated by the
        // M80.x herd. The plain term stays as the fallback; the description filter in
        // SelectCandidates remains the guard either way.
        if (claim.Subject == ClaimSubject.FamilyMember)
            terms.Add($"family history of {claim.Term}");
        else if (claim.Temporality == ClaimTemporality.History)
            terms.Add($"personal history of {claim.Term}");

        // Laterality-aware retrieval for "pain in {site}" terms: the ICD description IS
        // "Pain in left shoulder" / "Pain in right arm", so the sided phrase finds the exact
        // entry the plain term cannot substring-match.
        if (claim.Term.StartsWith("pain in ", StringComparison.OrdinalIgnoreCase)
            && claim.Laterality is "left" or "right")
        {
            terms.Add($"pain in {claim.Laterality} {claim.Term["pain in ".Length..]}");
        }

        if (PreferredPhrase.TryGetValue(claim.Term, out string? preferred))
            terms.Add(preferred);
        if (!string.IsNullOrWhiteSpace(claim.Term))
            terms.Add(claim.Term);
        return terms;
    }

    /// <summary>
    /// Filters and ranks raw index hits for a claim. Pure: claim + candidates in, ranked
    /// suggestions out.
    /// </summary>
    public static List<CodedSuggestion> SelectCandidates(
        ClinicalClaim claim, string searchTerm, IReadOnlyList<Icd10IndexEntry> candidates)
    {
        IEnumerable<Icd10IndexEntry> pool = candidates.Where(c => c.IsBillable && c.IsActive);

        // ── Modifier routing over the descriptions ───────────────────────────
        // The history branches need TWO checks. Containing "personal history" plus the term
        // somewhere was not enough — verified live: Z86.32 "Personal history of GESTATIONAL
        // diabetes" is the only "personal history"+"diabetes" entry in the CMS file, so a
        // plain "history of diabetes" claim resolved to a gestational-history code, including
        // for men. The regex requires the description to state a history of the term ITSELF
        // (see BuildHistoryOfTermRegex); an entry that narrows the term with an intervening
        // qualifier fails it, and the claim lists as "noticed but not mapped" instead — the
        // safe failure direction. Never a wrong narrower code.
        Regex? historyOfTerm = BuildHistoryOfTermRegex(claim.Term);
        pool = claim.Subject == ClaimSubject.FamilyMember
            ? pool.Where(c => (ContainsIgnoreCase(c.ShortDescription, "family history")
                               || ContainsIgnoreCase(c.LongDescription, "family history"))
                              && MatchesEitherDescription(historyOfTerm, c))
            : claim.Temporality == ClaimTemporality.History
                ? pool.Where(c => (ContainsIgnoreCase(c.ShortDescription, "personal history")
                                   || ContainsIgnoreCase(c.LongDescription, "personal history"))
                                  && MatchesEitherDescription(historyOfTerm, c))
                : pool.Where(c => !ContainsIgnoreCase(c.ShortDescription, "family history")
                                  && !ContainsIgnoreCase(c.LongDescription, "family history")
                                  && !ContainsIgnoreCase(c.ShortDescription, "personal history")
                                  && !ContainsIgnoreCase(c.LongDescription, "personal history"));

        // ── Laterality: never suggest the wrong side ─────────────────────────
        // The exclusion is decided on the LONG description, which always spells the side out.
        // The CMS SHORT descriptions abbreviate the sides to bare "r"/"l" ("…tear/ruptr of r
        // shoulder…"), so a \bright\b test against the short text alone silently never fired
        // and wrong-side codes survived the filter. The short text is still consulted for the
        // rare entry that spells a side out only there — in ICD descriptions a spelled-out
        // "right"/"left" always means the side.
        if (string.Equals(claim.Laterality, "left", StringComparison.OrdinalIgnoreCase))
            pool = pool.Where(c => !RightRx.IsMatch(c.LongDescription) && !RightRx.IsMatch(c.ShortDescription));
        else if (string.Equals(claim.Laterality, "right", StringComparison.OrdinalIgnoreCase))
            pool = pool.Where(c => !LeftRx.IsMatch(c.LongDescription) && !LeftRx.IsMatch(c.ShortDescription));

        int take = string.Equals(claim.Laterality, "bilateral", StringComparison.OrdinalIgnoreCase)
            ? MaxSuggestionsPerClaim + 1
            : MaxSuggestionsPerClaim;

        // ── Rank. Learned live against the full CMS file, not just hand-picked candidates.
        // The pool now arrives from the index's RANKED search (its first two tiers mirror the
        // first two below), no longer in code order — but this ordering stays the final
        // arbiter over the fetched window, and re-applying the shared tiers is harmless:
        //  * exact-start (either description) is the strongest signal;
        //  * a SHORTER CODE is a less-specific code — for a bare "osteoporosis" mention the
        //    honest default is M81.0, never an M80.011D "right shoulder, subsequent encounter"
        //    fracture code, and code length is what separates those tiers;
        //  * "unspecified" is checked as "unsp" too, because the CMS SHORT descriptions
        //    abbreviate it — the un-abbreviated check silently never fired;
        //  * "Other …" NEC codes and bracketed eponyms ("[Lequesne]") are deprioritized as
        //    coder-of-last-resort entries. ──
        return pool
            .OrderByDescending(c =>
                (c.ShortDescription.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase)
                 || c.LongDescription.StartsWith(searchTerm, StringComparison.OrdinalIgnoreCase)) ? 1 : 0)
            .ThenBy(c => c.Code.Length)
            .ThenByDescending(c =>
                (ContainsIgnoreCase(c.LongDescription, "unspecified") || ContainsIgnoreCase(c.ShortDescription, "unsp") ? 2 : 0)
                - (c.LongDescription.StartsWith("Other ", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                - (c.LongDescription.Contains('[') ? 1 : 0))
            .ThenBy(c => c.LongDescription.Length)
            .ThenBy(c => c.OrderNumber)
            .Take(take)
            .Select(c => new CodedSuggestion
            {
                Code = c.Code,
                Display = string.IsNullOrWhiteSpace(c.LongDescription) ? c.ShortDescription : c.LongDescription,
                Claim = claim,
            })
            .ToList();
    }

    /// <summary>
    /// Curated alternate surface forms a term takes inside ICD "history of …" descriptions.
    /// Tiny and individually defensible, like <see cref="PreferredPhrase"/>:
    ///  * Z85.820 writes melanoma as "malignant melanoma [of skin]" — a redundancy, not a
    ///    narrowing (melanoma is malignant by definition), and Z85.820 IS the standard
    ///    history-of-melanoma code;
    ///  * Z83.3 writes diabetes as "diabetes mellitus" — ICD's full name for the same
    ///    disease, again not a narrowing.
    /// "gestational diabetes" is deliberately absent: that IS a narrowing, and its absence is
    /// exactly what keeps Z86.32 unmatched for a plain diabetes-history claim.
    /// </summary>
    private static readonly Dictionary<string, string[]> HistoryTermSurfaceForms =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["melanoma"] = ["malignant melanoma"],
            ["diabetes"] = ["diabetes mellitus"],
        };

    /// <summary>
    /// Builds the guard for history-routed claims: the description must contain
    /// "history of {term}" CONTIGUOUSLY (whitespace-flexible; curated surface forms from
    /// <see cref="HistoryTermSurfaceForms"/> allowed), followed only by an innocuous tail —
    /// end of text, punctuation, or an "of {site}" prepositional phrase ("…malignant melanoma
    /// of skin"). An intervening or trailing qualifier the claim never stated —
    /// "history of GESTATIONAL diabetes", "history of (HEALED) osteoporosis FRACTURE",
    /// "history of melanoma IN-SITU" — narrows the condition and disqualifies the entry.
    /// Returns null for a blank term (no entry can qualify).
    /// </summary>
    private static Regex? BuildHistoryOfTermRegex(string term)
    {
        if (string.IsNullOrWhiteSpace(term))
            return null;

        IEnumerable<string> forms = [term.Trim()];
        if (HistoryTermSurfaceForms.TryGetValue(term.Trim(), out string[]? extra))
            forms = forms.Concat(extra);

        string alternation = string.Join("|",
            forms.Select(f => Regex.Escape(f).Replace(@"\ ", @"\s+")));
        return new Regex(
            $@"\bhistory\s+of\s+(?:{alternation})(?=\s*$|\s*[,.;:()\[\]]|\s+of\b)",
            RegexOptions.IgnoreCase);
    }

    private static bool MatchesEitherDescription(Regex? rx, Icd10IndexEntry c) =>
        rx is not null && (rx.IsMatch(c.ShortDescription) || rx.IsMatch(c.LongDescription));

    private static bool ContainsIgnoreCase(string haystack, string needle) =>
        haystack?.Contains(needle, StringComparison.OrdinalIgnoreCase) == true;
}
