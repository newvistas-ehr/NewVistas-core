// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Helpers;

/// <summary>
/// The ORWPT LOOKUP search heuristic, shared by PatientIndexGrain (singleton
/// source of truth) and PatientSearchGrain (stateless-worker readers) so the
/// two can never drift:
///   4-digit numeric     → SSN last-4 exact match (^DPT "BS5" x-ref)
///   1–8 digit numeric   → DFN exact match (^DPT direct IEN)
///   Otherwise           → case-insensitive Name prefix (^DPT "B" x-ref)
/// </summary>
public static class PatientIndexSearchHelper
{
    public static List<PatientIndexEntry> Search(
        IEnumerable<PatientIndexEntry> entries, string searchTerm, int maxResults)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return new List<PatientIndexEntry>();

        string term = searchTerm.Trim();

        IEnumerable<PatientIndexEntry> results;

        if (term.Length == 4 && term.All(char.IsDigit))
        {
            // 4-digit all-numeric: SSN last-4 exact match
            results = entries.Where(e => e.SsnLast4 == term);
        }
        else if (term.Length >= 1 && term.Length <= 8 && term.All(char.IsDigit))
        {
            // 1–8 digit numeric (not 4): DFN exact match
            results = entries.Where(e => e.Dfn == term);
        }
        else
        {
            // Name prefix search — mirrors VistA "B" cross-reference on ^DPT
            // Supports "LAST" or "LAST,FIRST" prefix, case-insensitive
            results = entries.Where(e => e.Name.StartsWith(term, StringComparison.OrdinalIgnoreCase));
        }

        return results
            .OrderBy(e => e.Name)
            .Take(maxResults)
            .ToList();
    }
}
