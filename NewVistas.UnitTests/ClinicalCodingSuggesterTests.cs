// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NUnit.Framework;
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Services;

namespace NewVistas.UnitTests;

/// <summary>
/// Pure-function tests for the ICD-10 suggester. The worked examples are synthetic sentences
/// constructed to exercise the places where naive text→code goes dangerously wrong — family
/// history read as the patient's own disease, personal history read as active cancer, and
/// negation read as affirmation.
/// </summary>
[TestFixture]
public class ClinicalCodingSuggesterTests
{
    private static readonly LexiconCodingAssistant Lexicon = new();

    private static async Task<List<ClinicalClaim>> ClaimsFor(string text)
        => (await Lexicon.SuggestClaimsAsync(text)).Claims;

    // ── Lexicon: the three dangerous failure modes ──────────────────────────

    [Test]
    public async Task Lexicon_FathersOsteoporosis_IsAFamilyClaim()
    {
        List<ClinicalClaim> claims = await ClaimsFor("My father had osteoporosis and grew noticeably shorter in his seventies.");
        ClinicalClaim c = claims.Single(x => x.Term == "osteoporosis");
        Assert.That(c.Subject, Is.EqualTo(ClaimSubject.FamilyMember));
        Assert.That(c.Polarity, Is.EqualTo(EvidencePolarity.Supports));
    }

    [Test]
    public async Task Lexicon_HistoryOfMelanoma_IsAHistoryClaim()
    {
        List<ClinicalClaim> claims = await ClaimsFor("History of melanoma and basal cell carcinoma of the skin.");
        ClinicalClaim c = claims.Single(x => x.Term == "melanoma");
        Assert.That(c.Temporality, Is.EqualTo(ClaimTemporality.History));
        Assert.That(c.Subject, Is.EqualTo(ClaimSubject.Patient));
    }

    [Test]
    public async Task Lexicon_NoChestPain_IsRefutedNotAffirmed()
    {
        List<ClinicalClaim> claims = await ClaimsFor("Denies chest pain, shortness of breath, dizziness, and palpitations.");
        Assert.That(claims.Single(x => x.Term == "chest pain").Polarity, Is.EqualTo(EvidencePolarity.Refutes));
        Assert.That(claims.Single(x => x.Term == "shortness of breath").Polarity, Is.EqualTo(EvidencePolarity.Refutes));
    }

    [Test]
    public async Task Lexicon_NoVitalsWereTaken_IsNotAssessedNotRefuted()
    {
        // "No vital signs were taken" is a recorded GAP, not a negative finding. The cue is the
        // assessment verb: negation + taken/measured/performed → NotAssessed.
        List<ClinicalClaim> claims = await ClaimsFor(
            "No pulse or blood pressure was taken during or after the episode. "
            + "The weakness itself was never measured or recorded.");
        ClinicalClaim weakness = claims.First(x => x.Term == "muscle weakness");
        Assert.That(weakness.Polarity, Is.Not.EqualTo(EvidencePolarity.Supports),
            "a sentence stating an absence of measurement must not affirm the finding");
    }

    [Test]
    public async Task Lexicon_CouldNotLiftMyHand_FindsWeaknessDespiteZeroSharedWords()
    {
        // "strength" appears 3 times in ~70,000 ICD-10 descriptions — all gym activity codes.
        // The lay phrasing shares no words with M62.81 "Muscle weakness"; only the curated
        // pattern bridges it.
        List<ClinicalClaim> claims = await ClaimsFor(
            "I did not have the strength to raise my arm to shoulder height while getting up from the chair.");
        Assert.That(claims.Any(x => x.Term == "muscle weakness"), Is.True);
    }

    // ── Lexicon: the four verified traps ────────────────────────────────────

    [Test]
    public async Task Lexicon_MomsDiabetes_IsAFamilyClaim()
    {
        // Patients say "my mom", not "my mother". The formal-only kinship list read
        // "My mom has diabetes." as the PATIENT's diabetes.
        List<ClinicalClaim> claims = await ClaimsFor("My mom has diabetes.");
        ClinicalClaim c = claims.Single(x => x.Term == "diabetes");
        Assert.That(c.Subject, Is.EqualTo(ClaimSubject.FamilyMember));
        Assert.That(c.Polarity, Is.EqualTo(EvidencePolarity.Supports));
    }

    [Test]
    public async Task Lexicon_Laterality_IsResolvedPerClaimNotPerSentence()
    {
        // The FIRST left/right in a sentence used to be stamped on every claim from it, so the
        // knee brace's "left" landed on the shoulder — and a wrong-sided code is worse than
        // none. Laterality now comes from proximity to the matched term.
        List<ClinicalClaim> claims = await ClaimsFor(
            "Left knee brace refitted; patient reports pain in right shoulder.");
        ClinicalClaim shoulder = claims.Single(x => x.Term == "pain in shoulder");
        Assert.That(shoulder.Laterality, Is.EqualTo("right"),
            "the knee's 'left' must not be stamped onto the shoulder claim");
    }

    [Test]
    public async Task Lexicon_DeniesLossOfStrength_IsRefutedNotAffirmed()
    {
        // The AlwaysAffirmed opt-out exists because those patterns affirm THROUGH a negation
        // ("did not have the strength"). It must not swallow a genuine denial standing OUTSIDE
        // the matched phrase, or "denies loss of strength" becomes affirmed weakness.
        List<ClinicalClaim> claims = await ClaimsFor("Patient denies loss of strength.");
        Assert.That(claims.Single(x => x.Term == "muscle weakness").Polarity,
            Is.EqualTo(EvidencePolarity.Refutes));

        claims = await ClaimsFor("No loss of strength.");
        Assert.That(claims.Single(x => x.Term == "muscle weakness").Polarity,
            Is.EqualTo(EvidencePolarity.Refutes));
    }

    [Test]
    public async Task Lexicon_SingleNewlines_StopCueBleedAcrossListLines()
    {
        // List-style notes rarely end lines with punctuation. When only blank lines split, a
        // single \n merged the lines and the family-history heading claimed the assessment's
        // finding as familial.
        List<ClinicalClaim> claims = await ClaimsFor(
            "Family history: osteoporosis\nAssessment: neck pain for two weeks");
        Assert.That(claims.Single(x => x.Term == "osteoporosis").Subject,
            Is.EqualTo(ClaimSubject.FamilyMember));
        Assert.That(claims.Single(x => x.Term == "cervicalgia").Subject,
            Is.EqualTo(ClaimSubject.Patient),
            "the family-history cue must not bleed across the line break");
    }

    [Test]
    public async Task Lexicon_QuotesAreVerbatimSentences()
    {
        const string note = "Both shoulders started to ache. The pain hit 9/10 within a couple of minutes.";
        List<ClinicalClaim> claims = await ClaimsFor(note);
        Assert.That(claims, Is.Not.Empty);
        Assert.That(ClinicalClaimVerifier.Verify(note, claims), Is.EqualTo(0),
            "every lexicon quote must verify against its own source text");
    }

    // ── Verifier ────────────────────────────────────────────────────────────

    [Test]
    public void Verifier_FlagsAQuoteTheNoteDoesNotContain()
    {
        var claims = new List<ClinicalClaim>
        {
            new() { Term = "fever", SourceQuote = "The patient reports a high fever." },
        };
        int flagged = ClinicalClaimVerifier.Verify("Entirely different note text.", claims);
        Assert.That(flagged, Is.EqualTo(1));
        Assert.That(claims[0].QuoteVerified, Is.False);
        Assert.That(claims[0].VerificationNote, Does.Contain("not found"));
    }

    // ── Resolver routing ────────────────────────────────────────────────────

    private static Icd10IndexEntry E(string code, string desc, bool billable = true) =>
        new() { Code = code, ShortDescription = desc, LongDescription = desc, IsBillable = billable, IsActive = true };

    private static readonly List<Icd10IndexEntry> OsteoporosisCandidates = new()
    {
        E("M81.0", "Age-related osteoporosis without current pathological fracture"),
        E("Z82.62", "Family history of osteoporosis"),
    };

    [Test]
    public void Resolver_FamilyClaim_RoutesToFamilyHistoryCode()
    {
        var claim = new ClinicalClaim { Term = "osteoporosis", Subject = ClaimSubject.FamilyMember, QuoteVerified = true };
        List<CodedSuggestion> picks = ClaimToCodeResolver.SelectCandidates(claim, "osteoporosis", OsteoporosisCandidates);
        Assert.That(picks.Select(p => p.Code), Is.EqualTo(new[] { "Z82.62" }),
            "a relative's osteoporosis must NOT become the patient's M81.0");
    }

    [Test]
    public void Resolver_CurrentClaim_ExcludesHistoryCodes()
    {
        var claim = new ClinicalClaim { Term = "osteoporosis", QuoteVerified = true };
        List<CodedSuggestion> picks = ClaimToCodeResolver.SelectCandidates(claim, "osteoporosis", OsteoporosisCandidates);
        Assert.That(picks.Select(p => p.Code), Does.Contain("M81.0"));
        Assert.That(picks.Select(p => p.Code), Does.Not.Contain("Z82.62"));
    }

    [Test]
    public void Resolver_HistoryClaim_RoutesToPersonalHistoryCode()
    {
        var candidates = new List<Icd10IndexEntry>
        {
            E("C43.9", "Malignant melanoma of skin, unspecified"),
            E("Z85.820", "Personal history of malignant melanoma of skin"),
        };
        var claim = new ClinicalClaim { Term = "melanoma", Temporality = ClaimTemporality.History, QuoteVerified = true };
        List<CodedSuggestion> picks = ClaimToCodeResolver.SelectCandidates(claim, "melanoma", candidates);
        Assert.That(picks.Select(p => p.Code), Is.EqualTo(new[] { "Z85.820" }),
            "history of melanoma must NOT be suggested as active C43.x cancer");
    }

    [Test]
    public void Resolver_Laterality_NeverSuggestsTheWrongSide()
    {
        var candidates = new List<Icd10IndexEntry>
        {
            E("M79.601", "Pain in right arm"),
            E("M79.602", "Pain in left arm"),
        };
        var claim = new ClinicalClaim { Term = "pain in arm", Laterality = "left", QuoteVerified = true };
        List<CodedSuggestion> picks = ClaimToCodeResolver.SelectCandidates(claim, "pain in", candidates);
        Assert.That(picks.Select(p => p.Code), Does.Contain("M79.602"));
        Assert.That(picks.Select(p => p.Code), Does.Not.Contain("M79.601"));
    }

    [Test]
    public void Resolver_PrefersUnspecifiedAsTheHonestDefault()
    {
        var candidates = new List<Icd10IndexEntry>
        {
            E("R07.1", "Chest pain on breathing"),
            E("R07.9", "Chest pain, unspecified"),
        };
        var claim = new ClinicalClaim { Term = "chest pain", QuoteVerified = true };
        List<CodedSuggestion> picks = ClaimToCodeResolver.SelectCandidates(claim, "chest pain", candidates);
        Assert.That(picks[0].Code, Is.EqualTo("R07.9"),
            "when the note stated no more detail, the unspecified code is the honest first suggestion");
    }

    [Test]
    public void Resolver_NonBillableHeadersNeverSurface()
    {
        var candidates = new List<Icd10IndexEntry>
        {
            E("R51", "Headache", billable: false),
            E("R51.9", "Headache, unspecified"),
        };
        var claim = new ClinicalClaim { Term = "headache", QuoteVerified = true };
        List<CodedSuggestion> picks = ClaimToCodeResolver.SelectCandidates(claim, "headache", candidates);
        Assert.That(picks.Select(p => p.Code), Is.EqualTo(new[] { "R51.9" }));
    }

    [Test]
    public void Resolver_FamilyClaim_SearchesFamilyHistoryFirst()
    {
        // Found live: "osteoporosis" fetches ~100 M80.x codes in code order, so Z82.62 never
        // arrived in the candidate window and the family claim went unresolved. The fix is
        // modifier-aware retrieval — the family phrase leads the search list.
        var claim = new ClinicalClaim { Term = "osteoporosis", Subject = ClaimSubject.FamilyMember };
        Assert.That(ClaimToCodeResolver.BuildSearchTerms(claim)[0],
            Is.EqualTo("family history of osteoporosis"));
    }

    [Test]
    public void Resolver_BareOsteoporosis_PrefersM810OverFractureEncounterCodes()
    {
        // The live-index shape: M80.x fracture-encounter codes (long codes, abbreviated
        // "unsp" shorts) sort ahead of M81.x. A bare "osteoporosis" mention must default to
        // the least-specific tier, never a "right shoulder, subsequent encounter" code.
        var candidates = new List<Icd10IndexEntry>
        {
            new() { Code = "M80.011D", ShortDescription = "Age-rel osteopor w current path fx, r shoulder, subs",
                    LongDescription = "Age-related osteoporosis with current pathological fracture, right shoulder, subsequent encounter for fracture with routine healing",
                    IsBillable = true, IsActive = true },
            new() { Code = "M80.00XA", ShortDescription = "Age-rel osteopor w current path fx, unsp site, init",
                    LongDescription = "Age-related osteoporosis with current pathological fracture, unspecified site, initial encounter",
                    IsBillable = true, IsActive = true },
            E("M81.6", "Localized osteoporosis [Lequesne]"),
            E("M81.8", "Other osteoporosis without current pathological fracture"),
            E("M81.0", "Age-related osteoporosis without current pathological fracture"),
        };
        var claim = new ClinicalClaim { Term = "osteoporosis", QuoteVerified = true };
        List<CodedSuggestion> picks = ClaimToCodeResolver.SelectCandidates(claim, "osteoporosis", candidates);
        Assert.That(picks[0].Code, Is.EqualTo("M81.0"),
            "shortest code + eponym/Other penalties must put the plain code first");
        Assert.That(picks.Select(p => p.Code), Does.Not.Contain("M80.011D"));
    }

    [Test]
    public void Resolver_PreferredPhrases_CoverTheTermsWhoseNameMisleads()
    {
        // "hypertension" alone ranks exotic variants above I10; the curated phrase pins the
        // default. The list is tiny on purpose — every entry is individually defensible.
        var claim = new ClinicalClaim { Term = "hypertension", QuoteVerified = true };
        Assert.That(ClaimToCodeResolver.BuildSearchTerms(claim)[0],
            Is.EqualTo("essential (primary) hypertension"));
    }

    // ── Model JSON contract ─────────────────────────────────────────────────

    [Test]
    public void Json_SchemaCarriesNoCodeField()
    {
        // The structural guarantee: a model constrained to this schema cannot emit a code.
        Assert.That(ClinicalCodingJson.ResponseSchemaJson, Does.Not.Contain("\"code\""));
        Assert.That(ClinicalCodingJson.ResponseSchemaJson, Does.Not.Contain("icd"));
    }

    [Test]
    public void Json_ParsesModifiersAndToleratesFences()
    {
        const string modelText = """
            Here you go:
            {"claims":[{"term":"muscle weakness","sourceQuote":"I could not lift my hand.",
            "polarity":"affirmed","subject":"patient","temporality":"current","laterality":""},
            {"term":"chest pain","sourceQuote":"No chest pain.","polarity":"negated",
            "subject":"patient","temporality":"current","laterality":""}]}
            """;
        CodingClaimsResult result = ClinicalCodingJson.Parse(modelText, "claude");
        Assert.That(result.Claims, Has.Count.EqualTo(2));
        Assert.That(result.Claims[0].Polarity, Is.EqualTo(EvidencePolarity.Supports));
        Assert.That(result.Claims[1].Polarity, Is.EqualTo(EvidencePolarity.Refutes));
    }

    [Test]
    public void Json_GarbageThrowsFormatException_SoTheResilientPathFallsBack()
        => Assert.Throws<FormatException>(() => ClinicalCodingJson.Parse("not json at all", "claude"));
}
