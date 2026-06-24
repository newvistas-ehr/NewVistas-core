// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Services;

/// <summary>
/// Verifies generated claims against the grounded context. This is the guard that
/// stops a fluent-but-wrong summary from being shown as trusted: a claim survives as
/// <c>Verified</c> only if it cites at least one source fact AND every fact it cites is
/// actually present in the context. A claim that cites nothing (ungrounded) or cites a
/// fact the model invented (a hallucinated id) is flagged.
///
/// This is the structural grounding check — the cheap, deterministic floor. A live
/// deployment would layer a semantic check on top (does the claim's asserted value
/// match the source fact's value), but the structural guarantee alone already prevents
/// the most dangerous failure: a sentence with no traceable source reaching a clinician.
/// </summary>
public static class ClinicalSummaryVerifier
{
    /// <summary>
    /// Marks each claim verified/flagged in place against the context's facts.
    /// Returns the number of claims that failed verification.
    /// </summary>
    public static int Verify(ClinicalSummaryContext context, IReadOnlyList<SummaryClaim> claims)
    {
        HashSet<string> knownFactIds = context.Facts.Select(f => f.FactId).ToHashSet(StringComparer.Ordinal);
        int flagged = 0;

        foreach (SummaryClaim claim in claims)
        {
            if (claim.SupportingFactIds.Count == 0)
            {
                claim.Verified = false;
                claim.VerificationNote = "Ungrounded: claim cites no source fact.";
                flagged++;
                continue;
            }

            List<string> missing = claim.SupportingFactIds
                .Where(id => !knownFactIds.Contains(id))
                .ToList();

            if (missing.Count > 0)
            {
                claim.Verified = false;
                claim.VerificationNote =
                    $"Unverifiable: cites fact(s) not in the record — {string.Join(", ", missing)}.";
                flagged++;
            }
            else
            {
                claim.Verified = true;
                claim.VerificationNote = null;
            }
        }

        return flagged;
    }
}
