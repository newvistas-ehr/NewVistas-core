// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Concurrency;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// MPI Match Grain — StatelessWorker that performs probabilistic patient matching
/// against the MPI search index.
///
/// [StatelessWorker] allows Orleans to maintain multiple local activations per
/// silo under load. All activations query the same MPI-INDEX singleton grain.
///
/// Scoring algorithm (inspired by VistA MPI matching in MPIF001.m, RGADTP.m):
///   - Exact SSN match:            +0.50
///   - Name similarity (Levenshtein normalized): +0.30
///   - Date of birth exact match:  +0.15
///   - Sex match:                  +0.05
///   - Total score 0.0 – 1.0
///   - Match threshold: 0.70
/// </summary>
[StatelessWorker]
public class MpiMatchGrain : Grain, IMpiMatchGrain
{
    private const double MatchThreshold = 0.70;
    private const double SsnWeight = 0.50;
    private const double NameWeight = 0.30;
    private const double DobWeight = 0.15;
    private const double SexWeight = 0.05;

    private readonly IGrainFactory _grainFactory;

    public MpiMatchGrain(IGrainFactory grainFactory)
    {
        _grainFactory = grainFactory;
    }

    public async Task<MpiMatchResult> FindMatchAsync(string patientName, string ssn, DateTime? dateOfBirth, string? sex)
    {
        List<MpiMatchCandidate> candidates = await FindCandidatesAsync(patientName, ssn, dateOfBirth, sex, 0.0);

        if (candidates.Count == 0)
        {
            return new MpiMatchResult
            {
                MatchFound = false,
                BestCandidate = null,
                Confidence = 0.0
            };
        }

        MpiMatchCandidate best = candidates[0];

        return new MpiMatchResult
        {
            MatchFound = best.Score >= MatchThreshold,
            BestCandidate = best,
            Confidence = best.Score
        };
    }

    public async Task<List<MpiMatchCandidate>> FindCandidatesAsync(string patientName, string? ssn, DateTime? dateOfBirth, string? sex, double minimumScore)
    {
        IMpiSearchGrain searchGrain = _grainFactory.GetGrain<IMpiSearchGrain>("MPI-INDEX");

        // Gather candidate entries from the index using available search criteria
        Dictionary<string, MpiSearchEntry> candidateMap = new();

        // If SSN provided, get SSN matches (most discriminating field)
        if (!string.IsNullOrEmpty(ssn))
        {
            List<MpiSearchResult> ssnResults = await searchGrain.LookupBySsnAsync(ssn);
            foreach (MpiSearchResult result in ssnResults)
            {
                candidateMap[result.Icn] = new MpiSearchEntry
                {
                    Icn = result.Icn,
                    PatientName = result.PatientName,
                    Ssn = result.Ssn,
                    DateOfBirth = result.DateOfBirth,
                    Sex = result.Sex,
                    IsDeceased = result.IsDeceased
                };
            }
        }

        // Also search by name to catch SSN mismatches / typos
        if (!string.IsNullOrEmpty(patientName))
        {
            // Extract last name for prefix search
            string searchName = patientName.Contains(',')
                ? patientName[..patientName.IndexOf(',')]
                : patientName;

            if (searchName.Length >= 2)
            {
                List<MpiSearchResult> nameResults = await searchGrain.SearchAsync(searchName, 50);
                foreach (MpiSearchResult result in nameResults)
                {
                    candidateMap.TryAdd(result.Icn, new MpiSearchEntry
                    {
                        Icn = result.Icn,
                        PatientName = result.PatientName,
                        Ssn = result.Ssn,
                        DateOfBirth = result.DateOfBirth,
                        Sex = result.Sex,
                        IsDeceased = result.IsDeceased
                    });
                }
            }
        }

        // Score each candidate
        List<MpiMatchCandidate> scoredCandidates = new();

        foreach (MpiSearchEntry candidate in candidateMap.Values)
        {
            double score = 0.0;
            List<string> details = new();

            // SSN match: +0.50
            if (!string.IsNullOrEmpty(ssn) && !string.IsNullOrEmpty(candidate.Ssn))
            {
                if (ssn == candidate.Ssn)
                {
                    score += SsnWeight;
                    details.Add($"SSN exact match: +{SsnWeight:F2}");
                }
            }

            // Name similarity: +0.30 (Levenshtein-based normalized)
            if (!string.IsNullOrEmpty(patientName) && !string.IsNullOrEmpty(candidate.PatientName))
            {
                double nameSimilarity = CalculateNameSimilarity(patientName, candidate.PatientName);
                double nameScore = nameSimilarity * NameWeight;
                score += nameScore;
                details.Add($"Name similarity ({nameSimilarity:F2}): +{nameScore:F2}");
            }

            // DOB match: +0.15
            if (dateOfBirth.HasValue && candidate.DateOfBirth.HasValue)
            {
                if (dateOfBirth.Value.Date == candidate.DateOfBirth.Value.Date)
                {
                    score += DobWeight;
                    details.Add($"DOB exact match: +{DobWeight:F2}");
                }
            }

            // Sex match: +0.05
            if (!string.IsNullOrEmpty(sex) && !string.IsNullOrEmpty(candidate.Sex))
            {
                if (string.Equals(sex, candidate.Sex, StringComparison.OrdinalIgnoreCase))
                {
                    score += SexWeight;
                    details.Add($"Sex match: +{SexWeight:F2}");
                }
            }

            if (score >= minimumScore)
            {
                scoredCandidates.Add(new MpiMatchCandidate
                {
                    Icn = candidate.Icn,
                    PatientName = candidate.PatientName,
                    Ssn = candidate.Ssn,
                    DateOfBirth = candidate.DateOfBirth,
                    Sex = candidate.Sex,
                    Score = score,
                    MatchDetails = string.Join("; ", details)
                });
            }
        }

        // Sort by descending score
        scoredCandidates.Sort((a, b) => b.Score.CompareTo(a.Score));

        return scoredCandidates;
    }

    /// <summary>
    /// Calculates normalized name similarity using Levenshtein distance.
    /// Returns a value between 0.0 (completely different) and 1.0 (identical).
    /// </summary>
    private static double CalculateNameSimilarity(string name1, string name2)
    {
        string a = name1.ToUpperInvariant().Trim();
        string b = name2.ToUpperInvariant().Trim();

        if (a == b) return 1.0;
        if (a.Length == 0 || b.Length == 0) return 0.0;

        int distance = LevenshteinDistance(a, b);
        int maxLength = Math.Max(a.Length, b.Length);

        return 1.0 - ((double)distance / maxLength);
    }

    /// <summary>
    /// Computes the Levenshtein (edit) distance between two strings.
    /// Uses the standard dynamic programming approach with O(min(m,n)) space.
    /// </summary>
    private static int LevenshteinDistance(string source, string target)
    {
        if (source.Length == 0) return target.Length;
        if (target.Length == 0) return source.Length;

        // Ensure source is the shorter string for space optimization
        if (source.Length > target.Length)
        {
            (source, target) = (target, source);
        }

        int sourceLength = source.Length;
        int targetLength = target.Length;

        // Only two rows needed: previous and current
        int[] previousRow = new int[sourceLength + 1];
        int[] currentRow = new int[sourceLength + 1];

        // Initialize the previous row (distance from empty string)
        for (int i = 0; i <= sourceLength; i++)
        {
            previousRow[i] = i;
        }

        for (int j = 1; j <= targetLength; j++)
        {
            currentRow[0] = j;

            for (int i = 1; i <= sourceLength; i++)
            {
                int cost = source[i - 1] == target[j - 1] ? 0 : 1;

                currentRow[i] = Math.Min(
                    Math.Min(
                        currentRow[i - 1] + 1,      // insertion
                        previousRow[i] + 1),         // deletion
                    previousRow[i - 1] + cost);      // substitution
            }

            // Swap rows
            (previousRow, currentRow) = (currentRow, previousRow);
        }

        return previousRow[sourceLength];
    }
}
