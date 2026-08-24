// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Text.RegularExpressions;
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Services;

/// <summary>
/// Seam for extracting clinical CLAIMS from a note so ICD-10 codes can be suggested. SEAM
/// only: the default registration is <see cref="LexiconCodingAssistant"/>, which runs offline
/// with no model. A live Claude assistant drops in behind this interface.
///
/// The design decision that makes this safe: <b>an assistant never emits a code.</b> It emits
/// claims — a clinical term, the verbatim sentence it came from, and three modifiers
/// (polarity, subject, temporality). A separate deterministic resolver
/// (<see cref="Clinical.ClaimToCodeResolver"/>) turns claims into codes using the site's own
/// ICD-10 index, so a hallucinated code is structurally impossible: every code a clinician
/// sees came out of the CMS file, never out of a model.
///
/// The modifiers exist because they are exactly where naive text→code goes dangerously wrong:
/// "My father had osteoporosis" must become Z82.62 (family history), not M81.0; "History of
/// melanoma" must become Z85.820, not an active C43.x; and "No chest pain" must become a
/// Refutes claim — a usable informative negative — not an R07.9 suggestion.
/// </summary>
public interface IClinicalCodingAssistant
{
    bool IsLiveModel { get; }
    string ProviderName { get; }

    /// <summary>Extracts coded-claim candidates from the note text.</summary>
    Task<CodingClaimsResult> SuggestClaimsAsync(string noteText, CancellationToken cancellationToken = default);
}

/// <summary>Whose observation a claim is about.</summary>
public enum ClaimSubject
{
    Patient = 0,
    /// <summary>A relative — routes to the Z82/Z83 family-history code families.</summary>
    FamilyMember = 1,
}

/// <summary>When the claimed condition applies.</summary>
public enum ClaimTemporality
{
    Current = 0,
    /// <summary>Resolved past condition — routes to the Z85–Z87 personal-history families.</summary>
    History = 1,
}

/// <summary>
/// One clinical claim extracted from a note: a term in standard clinical vocabulary, the
/// verbatim sentence it came from, and the modifiers that decide which code family (if any)
/// it may map to. Reuses <see cref="EvidencePolarity"/> so a claim carries the same meaning
/// ADR-006 evidence does: Supports = stated present, Refutes = explicitly denied,
/// NotAssessed = explicitly not measured/checked.
/// </summary>
[GenerateSerializer]
public sealed class ClinicalClaim
{
    [Id(0)] public string Term { get; set; } = string.Empty;

    /// <summary>The sentence, VERBATIM, that this claim came from. Verified against the note.</summary>
    [Id(1)] public string SourceQuote { get; set; } = string.Empty;

    [Id(2)] public EvidencePolarity Polarity { get; set; } = EvidencePolarity.Supports;
    [Id(3)] public ClaimSubject Subject { get; set; }
    [Id(4)] public ClaimTemporality Temporality { get; set; }

    /// <summary>"left" / "right" / "bilateral" when the text states it; null otherwise.</summary>
    [Id(5)] public string? Laterality { get; set; }

    /// <summary>Set by <see cref="ClinicalClaimVerifier"/>; a claim whose quote is not in the note is flagged.</summary>
    [Id(6)] public bool QuoteVerified { get; set; }
    [Id(7)] public string? VerificationNote { get; set; }
}

/// <summary>Result of a claim-extraction pass. Mirrors <see cref="RadiologyExtractionResult"/>.</summary>
[GenerateSerializer]
public sealed class CodingClaimsResult
{
    [Id(0)] public List<ClinicalClaim> Claims { get; set; } = new();
    [Id(1)] public string ProviderName { get; set; } = string.Empty;
    /// <summary>Non-null when a setup notice should be surfaced (live AI enabled, no key).</summary>
    [Id(2)] public string? ConfigurationNotice { get; set; }
}

/// <summary>One ICD-10 code resolved from a claim — what the clinician is actually shown.</summary>
[GenerateSerializer]
public sealed class CodedSuggestion
{
    /// <summary>The code, always taken from the site's ICD-10 index, never from a model.</summary>
    [Id(0)] public string Code { get; set; } = string.Empty;

    /// <summary>The index's official description for that code.</summary>
    [Id(1)] public string Display { get; set; } = string.Empty;

    /// <summary>The claim this code resolved from — carries the quote and the modifiers.</summary>
    [Id(2)] public ClinicalClaim Claim { get; set; } = new();
}

/// <summary>Everything the UI needs to render suggestions for one note.</summary>
[GenerateSerializer]
public sealed class NoteCodingSuggestions
{
    [Id(0)] public List<CodedSuggestion> Suggestions { get; set; } = new();

    /// <summary>Claims that resolved to no code — shown so the clinician sees what was noticed but unmapped.</summary>
    [Id(1)] public List<ClinicalClaim> UnresolvedClaims { get; set; } = new();

    [Id(2)] public string ProviderName { get; set; } = string.Empty;
    [Id(3)] public string? ConfigurationNotice { get; set; }
    [Id(4)] public DateTime GeneratedAt { get; set; }

    /// <summary>Non-suppressible. The UI may not hide this.</summary>
    [Id(5)] public string Disclaimer { get; set; } =
        "Machine-suggested candidates resolved from the site's ICD-10 index against quoted note "
        + "text. Not a coding determination. Review the quoted sentence before accepting; a code "
        + "you accept is filed as Unconfirmed and marked machine-cited.";
}

/// <summary>
/// Offline default. A deterministic, no-model floor: a curated pattern lexicon over the note's
/// sentences, with explicit negation / family / history / not-assessed cue handling. It is
/// intentionally modest — the live model handles paraphrase the lexicon cannot ("did not have
/// the strength to lift my hand" is covered only because a curated pattern says so) — but it
/// works with no network, no key and no cost, and every entry is individually defensible.
/// </summary>
public sealed class LexiconCodingAssistant : IClinicalCodingAssistant
{
    public bool IsLiveModel => false;
    public string ProviderName => "offline-lexicon";

    /// <summary>
    /// Surface-pattern → clinical term. Patterns are matched per sentence, case-insensitive.
    /// Terms are chosen to be findable in the CMS ICD-10 descriptions by substring search
    /// (that is the resolver's contract), which is why "neck pain" maps to "cervicalgia".
    /// </summary>
    private static readonly (Regex Pattern, string Term, bool AlwaysAffirmed)[] Lexicon =
    {
        (Rx(@"\b(muscle\s+)?weak(ness)?\b"), "muscle weakness", false),
        // These phrasings AFFIRM weakness through a negation of strength — "did not have the
        // strength", "could not lift", "losing strength". The negation is part of the pattern,
        // so the sentence-level negation cue must not flip them to Refutes; that is exactly how
        // "I did not have the strength to lift my hand" would otherwise be read as a denial of
        // weakness. (A double negation like "did not lose strength" is mis-read as affirmed —
        // a rare miss the live model path handles; the floor accepts it.)
        (Rx(@"\blos(s|ing|t)\s+(of\s+)?(much\s+of\s+)?(my\s+)?strength\b|\b(did|do|does)\s*(not|n[o']t)\s+have\s+the\s+strength\b|\bcould\s*(not|n[o']t)\s+lift\b"), "muscle weakness", true),
        (Rx(@"\bneck\b[^.]{0,40}\b(pain|hurt(s|ing)?|ache|aching|sore)\b|\b(pain|ache)\s+in\s+(the\s+|my\s+)?neck\b|\bcervicalgia\b"), "cervicalgia", false),
        (Rx(@"\bchest\s+pain\b|\bpain\s+in\s+(the\s+|my\s+)?chest\b"), "chest pain", false),
        (Rx(@"\bshort(ness)?\s+of\s+breath\b|\bdyspn(o?e)a\b"), "shortness of breath", false),
        (Rx(@"\b(arm|arms|forearm)s?\b[^.]{0,40}\b(pain|hurt(s|ing)?|ache|aching|sore)\b|\bpain\s+in\s+(the\s+|my\s+)?(right\s+|left\s+)?arm\b"), "pain in arm", false),
        (Rx(@"\bshoulders?\b[^.]{0,40}\b(pain|hurt(s|ing)?|ache|aching|sore)\b|\bpain\s+in\s+(the\s+|my\s+)?(right\s+|left\s+)?shoulder\b"), "pain in shoulder", false),
        (Rx(@"\b(low(er)?\s+)?back\s+pain\b|\bback\b[^.]{0,30}\b(hurt(s|ing)?|ache|aching)\b"), "low back pain", false),
        (Rx(@"\bdizz(y|iness)\b|\blight-?headed(ness)?\b"), "dizziness", false),
        (Rx(@"\bheadaches?\b"), "headache", false),
        (Rx(@"\bnausea(ted)?\b|\bnauseous\b"), "nausea", false),
        (Rx(@"\bvomit(ing|ed|s)?\b"), "vomiting", false),
        (Rx(@"\bfever(ish)?\b|\bfebrile\b"), "fever", false),
        (Rx(@"\bcough(ing|ed|s)?\b"), "cough", false),
        (Rx(@"\bnumb(ness)?\b|\btingling\b|\bpins\s+and\s+needles\b|\bparesthesia\b"), "paresthesia", false),
        (Rx(@"\bfatigue(d)?\b|\bexhaust(ed|ion)\b"), "fatigue", false),
        (Rx(@"\bpalpitations?\b"), "palpitations", false),
        (Rx(@"\bosteoporosis\b"), "osteoporosis", false),
        (Rx(@"\bmelanoma\b"), "melanoma", false),
        (Rx(@"\bdiabet(es|ic)\b"), "diabetes", false),
        (Rx(@"\bhypertension\b|\bhigh\s+blood\s+pressure\b"), "hypertension", false),
    };

    private static readonly Regex NegationRx =
        Rx(@"\b(no|not|denies|denied|without|never|none)\b");

    /// <summary>Negation of MEASUREMENT ("no vitals were taken") is NotAssessed, not Refutes.</summary>
    private static readonly Regex AssessmentVerbRx =
        Rx(@"\b(taken|measured|checked|performed|assessed|recorded|examined|obtained|done|tested)\b");

    // The informal kinship words matter as much as the formal ones — patients say "my mom has
    // diabetes", not "my mother". Missing them made the relative's disease the PATIENT's.
    // \b keeps the short forms honest: "mom" cannot match inside "moment".
    private static readonly Regex FamilyRx =
        Rx(@"\b(mother|father|mom|dad|brother|sister|siblings?|cousins?|son|daughter|grand(mother|father|ma|pa)|aunt|uncle|parents?|family\s+history|parental)\b");

    private static readonly Regex HistoryRx =
        Rx(@"\b(history\s+of|h/o|hx\s+of|status\s+post|s/p|previous|prior|past)\b");

    private static readonly Regex LateralityRx = Rx(@"\b(left|right|bilateral)\b");

    private static Regex Rx(string p) => new(p, RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// A left/right/bilateral token counts for a claim only when it sits inside the matched
    /// span or within this many characters of it. Wide enough for "pain in the right shoulder"
    /// and "right shoulder pain"; narrow enough that "Left knee brace refitted; patient reports
    /// pain in right shoulder" cannot stamp the knee's side onto the shoulder claim.
    /// </summary>
    private const int LateralityWindowChars = 30;

    public Task<CodingClaimsResult> SuggestClaimsAsync(string noteText, CancellationToken cancellationToken = default)
    {
        List<ClinicalClaim> claims = new();

        // A SINGLE newline splits sentences too. Clinical notes are full of list-style lines
        // with no terminal punctuation ("Family history:\nosteoporosis\nAssessment: ..."), and
        // merging such lines let cues bleed across them — a family-history heading claiming the
        // next section's finding as familial. The tradeoff is deliberate: a hard-wrapped
        // sentence may now lose a suggestion (the safe direction — a missed suggestion), but a
        // cue can no longer bleed into an unrelated line (the unsafe direction this prevents).
        foreach (string raw in Regex.Split(noteText ?? string.Empty, @"(?<=[.!?])\s+|[\r\n]+"))
        {
            string sentence = Regex.Replace(raw, @"\s+", " ").Trim();
            if (sentence.Length == 0)
                continue;

            bool negated = NegationRx.IsMatch(sentence);
            bool aboutAssessment = AssessmentVerbRx.IsMatch(sentence);
            bool family = FamilyRx.IsMatch(sentence);
            bool history = HistoryRx.IsMatch(sentence);

            foreach ((Regex pattern, string term, bool alwaysAffirmed) in Lexicon)
            {
                Match match = pattern.Match(sentence);
                if (!match.Success)
                    continue;
                if (claims.Any(c => c.Term == term && c.SourceQuote == sentence))
                    continue;

                // AlwaysAffirmed patterns affirm THROUGH a negation of strength ("did not have
                // the strength"), so the sentence-level negation cue must not flip them — but a
                // negation cue OUTSIDE the matched span is a genuine external denial ("Patient
                // denies loss of strength", "No loss of strength") and must be honored, or the
                // opt-out turns a denial into an affirmed finding. Only cues BEFORE the match
                // start count: a cue inside the span is the pattern's own phrasing.
                bool externallyNegated = alwaysAffirmed
                    && NegationRx.IsMatch(sentence[..match.Index]);

                claims.Add(new ClinicalClaim
                {
                    Term = term,
                    SourceQuote = sentence,
                    // Sentence-scope negation is deliberately blunt: one "no" marks every claim
                    // in the sentence. That over-negates occasionally, which is the safe error —
                    // an affirmed symptom shown as denied loses a suggestion; a denied symptom
                    // shown as affirmed suggests a problem the patient does not have.
                    // AlwaysAffirmed entries opt out unless externally negated (see above); an
                    // external denial takes the same NotAssessed/Refutes path as any negation.
                    Polarity = (alwaysAffirmed ? externallyNegated : negated)
                        ? (aboutAssessment ? EvidencePolarity.NotAssessed : EvidencePolarity.Refutes)
                        : EvidencePolarity.Supports,
                    Subject = family ? ClaimSubject.FamilyMember : ClaimSubject.Patient,
                    Temporality = history && !family ? ClaimTemporality.History : ClaimTemporality.Current,
                    Laterality = NearestLaterality(sentence, match),
                });
            }
        }

        return Task.FromResult(new CodingClaimsResult { Claims = claims, ProviderName = ProviderName });
    }

    /// <summary>
    /// Laterality is resolved PER CLAIM, not per sentence: the nearest left/right/bilateral
    /// token inside the matched span or within <see cref="LateralityWindowChars"/> characters
    /// of it, else none. Stamping the sentence's FIRST side token on every claim mis-attributed
    /// laterality the moment a sentence mentioned two body parts — "Left knee brace refitted;
    /// patient reports pain in right shoulder" gave the shoulder claim "left", and a
    /// wrong-sided code is worse than no code at all.
    /// </summary>
    private static string? NearestLaterality(string sentence, Match claim)
    {
        int claimEnd = claim.Index + claim.Length;
        string? nearest = null;
        int nearestDistance = int.MaxValue;

        foreach (Match side in LateralityRx.Matches(sentence))
        {
            int sideEnd = side.Index + side.Length;
            int distance =
                sideEnd <= claim.Index ? claim.Index - sideEnd    // token precedes the span
                : side.Index >= claimEnd ? side.Index - claimEnd  // token follows the span
                : 0;                                              // token inside the span
            if (distance <= LateralityWindowChars && distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = side.Value.ToLowerInvariant();
            }
        }

        return nearest;
    }
}
