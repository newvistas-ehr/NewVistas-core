// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Concurrency;
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Services;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// The clinical-coding pipeline: assistant → verifier → resolver.
///
///  1. The assistant (offline lexicon by default, live model when configured) extracts
///     CLAIMS — never codes.
///  2. <see cref="ClinicalClaimVerifier"/> checks every quote verbatim against the note; a
///     claim the note does not contain is flagged and excluded from code resolution.
///  3. <see cref="ClaimToCodeResolver"/> maps verified claims to ICD-10 candidates through
///     the site's own index — the only place a code string can come from.
/// </summary>
[StatelessWorker]
public class ClinicalCodingWorkerGrain : Grain, IClinicalCodingWorkerGrain
{
    public const string Key = "CLINICAL-CODING";

    private readonly IClinicalCodingAssistant _assistant;

    public ClinicalCodingWorkerGrain(IClinicalCodingAssistant assistant)
    {
        _assistant = assistant;
    }

    public async Task<NoteCodingSuggestions> SuggestForTextAsync(string noteText)
    {
        CodingClaimsResult extracted = await _assistant.SuggestClaimsAsync(noteText);
        ClinicalClaimVerifier.Verify(noteText, extracted.Claims);

        var index = GrainFactory.GetGrain<IIcd10IndexGrain>("ICD10-INDEX");
        var result = new NoteCodingSuggestions
        {
            ProviderName = extracted.ProviderName,
            ConfigurationNotice = extracted.ConfigurationNotice,
            GeneratedAt = DateTime.UtcNow,
        };

        foreach (ClinicalClaim claim in extracted.Claims)
        {
            // An unverified quote never reaches code resolution: a code without a real
            // sentence behind it is exactly the suggestion this design exists to prevent.
            if (!claim.QuoteVerified)
            {
                result.UnresolvedClaims.Add(claim);
                continue;
            }

            List<CodedSuggestion> resolved = new();
            foreach (string term in ClaimToCodeResolver.BuildSearchTerms(claim))
            {
                // Ranked fetch, not the code-ordered SearchAsync: the ranking's first tiers
                // run inside the index BEFORE the window is cut, so a high-frequency term
                // ("fracture", 20,365 matches) can no longer starve the honest generic code
                // out of the candidate pool. SelectCandidates stays the final arbiter.
                List<Icd10IndexEntry> candidates =
                    await index.SearchRankedAsync(term, billableOnly: true, ClaimToCodeResolver.MaxCandidatesToFetch);
                resolved = ClaimToCodeResolver.SelectCandidates(claim, term, candidates);
                if (resolved.Count > 0)
                    break;
            }

            if (resolved.Count == 0)
                result.UnresolvedClaims.Add(claim);
            else
                result.Suggestions.AddRange(
                    resolved.Where(s => result.Suggestions.All(x => x.Code != s.Code || x.Claim.SourceQuote != s.Claim.SourceQuote)));
        }

        return result;
    }
}
