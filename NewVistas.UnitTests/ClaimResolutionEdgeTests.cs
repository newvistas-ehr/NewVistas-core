// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using Orleans.TestingHost;
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Services;

namespace NewVistas.UnitTests;

/// <summary>
/// Regression tests for three verified retrieval/ranking traps in the ICD-10 suggester's
/// code resolution:
///  1. a "history of diabetes" claim resolved to Z86.32 (personal history of GESTATIONAL
///     diabetes — the only "personal history"+"diabetes" entry in the CMS file), including
///     for men;
///  2. the wrong-side laterality exclusion tested only the SHORT description, whose CMS text
///     abbreviates the sides to bare "r"/"l", so it silently never fired;
///  3. the code-ordered fetch window starved free-vocabulary terms — a term matching more
///     rows than MaxCandidatesToFetch returned only the earliest chapters, and ranking could
///     never reach the honest generic code.
/// </summary>
[TestFixture]
public class ClaimResolutionEdgeTests
{
    private TestCluster _cluster = default!;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _cluster = SharedCluster.Instance;
    }

    private static Icd10IndexEntry E(string code, string desc, int orderNumber = 0) => new()
    {
        Code = code, ShortDescription = desc, LongDescription = desc,
        IsBillable = true, IsActive = true, OrderNumber = orderNumber,
    };

    // ── Trap 1: history claims must never resolve to a narrower condition ──────

    [Test]
    public void Resolver_HistoryOfDiabetes_YieldsNoSuggestion_NotTheGestationalCode()
    {
        // The CMS-file shape: Z86.32 is the ONLY entry containing both "personal history"
        // and "diabetes", so the old substring filter made it win for every diabetes-history
        // claim. The narrowing guard must reject it ("gestational" intervenes between
        // "history of" and the term) and leave the claim honestly unresolved — the safe
        // failure is NO suggestion, never a wrong narrower code.
        var candidates = new List<Icd10IndexEntry>
        {
            E("Z86.32", "Personal history of gestational diabetes"),
            E("E11.9", "Type 2 diabetes mellitus without complications"),
        };
        var claim = new ClinicalClaim
        {
            Term = "diabetes", Temporality = ClaimTemporality.History, QuoteVerified = true,
        };

        // Mirror the worker's loop: no search term may produce a suggestion from this pool.
        foreach (string term in ClaimToCodeResolver.BuildSearchTerms(claim))
        {
            List<CodedSuggestion> picks = ClaimToCodeResolver.SelectCandidates(claim, term, candidates);
            Assert.That(picks, Is.Empty,
                $"search term '{term}' must not resolve a plain diabetes-history claim to a "
                + "gestational-history or active-diabetes code");
        }
    }

    [Test]
    public void Resolver_HistoryOfMelanoma_StillResolvesToZ85820()
    {
        // The existing win must stay a win. Z85.820's real CMS description is "Personal
        // history of MALIGNANT melanoma OF SKIN" — "malignant" is a curated surface form
        // (melanoma is malignant by definition) and "of skin" an innocuous prepositional
        // tail, so the guard accepts it. Z86.006's trailing "in-situ" is a genuine
        // narrowing and must be rejected, or the modifier-aware retrieval phrase would stop
        // at it and Z85.820 could never surface against the full CMS file.
        var candidates = new List<Icd10IndexEntry>
        {
            E("C43.9", "Malignant melanoma of skin, unspecified"),
            E("Z85.820", "Personal history of malignant melanoma of skin"),
            E("Z86.006", "Personal history of melanoma in-situ"),
        };
        var claim = new ClinicalClaim
        {
            Term = "melanoma", Temporality = ClaimTemporality.History, QuoteVerified = true,
        };

        List<CodedSuggestion> picks = ClaimToCodeResolver.SelectCandidates(claim, "melanoma", candidates);

        Assert.That(picks.Select(p => p.Code), Is.EqualTo(new[] { "Z85.820" }));
    }

    // ── Trap 2: wrong-side exclusion vs abbreviated short descriptions ─────────

    [Test]
    public void Resolver_LeftClaim_NeverYieldsRightSideCode_DespiteAbbreviatedShorts()
    {
        // The real CMS shape: SHORT descriptions write "r"/"l", so \bright\b against the
        // short text never matched and the wrong-side code survived. The exclusion now runs
        // against the LONG description, which always spells the side out.
        var candidates = new List<Icd10IndexEntry>
        {
            new()
            {
                Code = "M75.101",
                ShortDescription = "Unsp rotator cuff tear/ruptr of r shoulder, not trauma",
                LongDescription = "Unspecified rotator cuff tear or rupture of right shoulder, not specified as traumatic",
                IsBillable = true, IsActive = true, OrderNumber = 1,
            },
            new()
            {
                Code = "M75.102",
                ShortDescription = "Unsp rotator cuff tear/ruptr of l shoulder, not trauma",
                LongDescription = "Unspecified rotator cuff tear or rupture of left shoulder, not specified as traumatic",
                IsBillable = true, IsActive = true, OrderNumber = 2,
            },
        };
        var claim = new ClinicalClaim
        {
            Term = "rotator cuff tear", Laterality = "left", QuoteVerified = true,
        };

        List<CodedSuggestion> picks = ClaimToCodeResolver.SelectCandidates(claim, "rotator cuff", candidates);

        Assert.That(picks.Select(p => p.Code), Does.Contain("M75.102"));
        Assert.That(picks.Select(p => p.Code), Does.Not.Contain("M75.101"),
            "a left-sided claim must never suggest the right-side code, even when the short "
            + "description abbreviates the side to 'r'");
    }

    // ── Trap 3: ranked fetch — the window can no longer starve ─────────────────

    [Test]
    public async Task RankedSearch_ReturnsStartsWithMatchTheCodeOrderedWindowStarved()
    {
        // Construct the starvation shape in miniature: eight low-OrderNumber noise entries
        // match the term mid-description, and the honest generic entry — the only one whose
        // description STARTS with the term — sorts last in code order, beyond a window of 5.
        var index = _cluster.GrainFactory.GetGrain<IIcd10IndexGrain>($"ICD10-EDGE-{Guid.NewGuid()}");
        var entries = new List<Icd10IndexEntry>();
        for (int i = 1; i <= 8; i++)
        {
            entries.Add(E($"S02.10{i}A", $"Aftercare following surgical fracture repair, stage {i}", orderNumber: i));
        }
        entries.Add(E("M84.40XA", "Fracture of bone, unspecified, initial encounter", orderNumber: 998));
        entries.Add(E("M84.4", "Fracture of bone, unspecified", orderNumber: 999));
        await index.LoadCodesAsync(entries);

        // The code-ordered page demonstrates the trap this replaces: the honest entries sit
        // beyond the window and are never fetched at all.
        List<Icd10IndexEntry> codeOrdered = await index.SearchAsync("fracture", billableOnly: true, maxResults: 5);
        Assert.That(codeOrdered.Select(e => e.Code), Does.Not.Contain("M84.4"),
            "precondition: the code-ordered window must exhibit the starvation being fixed");

        // The ranked fetch orders BEFORE cutting the window: starts-with first, then the
        // shortest (least-specific) code — so M84.4 beats both the noise herd and its own
        // longer-coded sibling despite carrying the largest OrderNumber.
        List<Icd10IndexEntry> ranked = await index.SearchRankedAsync("fracture", billableOnly: true, maxResults: 5);
        Assert.That(ranked, Has.Count.EqualTo(5));
        Assert.That(ranked[0].Code, Is.EqualTo("M84.4"),
            "the honest generic code must lead the window regardless of corpus position");
        Assert.That(ranked[1].Code, Is.EqualTo("M84.40XA"),
            "starts-with matches outrank mid-description matches; shorter code wins the tie");
    }

    [Test]
    public async Task RankedSearch_BlankTerm_ReturnsEmpty()
    {
        var index = _cluster.GrainFactory.GetGrain<IIcd10IndexGrain>($"ICD10-EDGE-{Guid.NewGuid()}");
        await index.LoadCodesAsync([E("M84.4", "Fracture of bone, unspecified", orderNumber: 1)]);

        Assert.That(await index.SearchRankedAsync("  ", billableOnly: true, maxResults: 5), Is.Empty);
    }
}
