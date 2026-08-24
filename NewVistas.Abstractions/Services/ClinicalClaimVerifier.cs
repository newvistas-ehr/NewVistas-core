// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Text.RegularExpressions;

namespace NewVistas.Abstractions.Services;

/// <summary>
/// Verifies extracted claims against the source note — the same grounding guarantee
/// <see cref="RadiologyFindingVerifier"/> gives radiology findings. Each claim must quote a
/// sentence that actually appears in the note (whitespace-normalized, case-insensitive); a
/// claim that does not is flagged, never shown as an equal. The assistant surfaces what the
/// note says — it cannot invent a sentence the note does not contain.
/// </summary>
public static class ClinicalClaimVerifier
{
    /// <summary>Marks each claim verified/flagged in place; returns the flagged count.</summary>
    public static int Verify(string noteText, IReadOnlyList<ClinicalClaim> claims)
    {
        string note = Normalize(noteText);
        int flagged = 0;

        foreach (ClinicalClaim claim in claims)
        {
            if (string.IsNullOrWhiteSpace(claim.SourceQuote))
            {
                claim.QuoteVerified = false;
                claim.VerificationNote = "Ungrounded: claim cites no source sentence.";
                flagged++;
                continue;
            }

            if (note.Contains(Normalize(claim.SourceQuote), StringComparison.Ordinal))
            {
                claim.QuoteVerified = true;
                claim.VerificationNote = null;
            }
            else
            {
                claim.QuoteVerified = false;
                claim.VerificationNote = "Unverifiable: source sentence not found in the note.";
                flagged++;
            }
        }

        return flagged;
    }

    private static string Normalize(string s) =>
        Regex.Replace(s ?? string.Empty, @"\s+", " ").Trim().ToLowerInvariant();
}
