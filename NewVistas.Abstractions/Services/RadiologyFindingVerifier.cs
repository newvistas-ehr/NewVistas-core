// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Text.RegularExpressions;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Services;

/// <summary>
/// Verifies extracted findings against the source report. The grounding guarantee for
/// radiology: each finding must quote a sentence that actually appears in the report.
/// A finding whose <see cref="RadiologyFinding.SourceQuote"/> is not present verbatim
/// (whitespace-normalized, case-insensitive) is flagged — the extractor surfaces what the
/// radiologist wrote, it cannot invent a finding the report doesn't contain.
/// </summary>
public static class RadiologyFindingVerifier
{
    /// <summary>Marks each finding verified/flagged in place; returns the flagged count.</summary>
    public static int Verify(string reportText, IReadOnlyList<RadiologyFinding> findings)
    {
        string report = Normalize(reportText);
        int flagged = 0;

        foreach (RadiologyFinding finding in findings)
        {
            if (string.IsNullOrWhiteSpace(finding.SourceQuote))
            {
                finding.QuoteVerified = false;
                finding.VerificationNote = "Ungrounded: finding cites no source sentence.";
                flagged++;
                continue;
            }

            if (report.Contains(Normalize(finding.SourceQuote), StringComparison.Ordinal))
            {
                finding.QuoteVerified = true;
                finding.VerificationNote = null;
            }
            else
            {
                finding.QuoteVerified = false;
                finding.VerificationNote = "Unverifiable: source sentence not found in the report.";
                flagged++;
            }
        }

        return flagged;
    }

    private static string Normalize(string s) =>
        Regex.Replace(s, @"\s+", " ").Trim().ToLowerInvariant();
}
