// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// MPI Match Grain — StatelessWorker that performs probabilistic patient matching
/// against the MPI index to identify potential duplicate or matching patients.
///
/// Grain Key: "MPI-MATCHER" (key is ignored by StatelessWorker routing)
///
/// Implements a weighted scoring algorithm inspired by VistA's MPI matching
/// logic in MPIF001.m and RGADTP.m:
///   - Exact SSN match:            +0.50
///   - Name similarity (Levenshtein): +0.30
///   - Date of birth exact match:  +0.15
///   - Sex match:                  +0.05
///   - Total score 0.0 – 1.0
///   - Match threshold: 0.70
///
/// This grain carries no persistent state. Orleans may maintain multiple
/// local activations per silo under load via [StatelessWorker].
/// </summary>
public interface IMpiMatchGrain : IGrainWithStringKey
{
    /// <summary>
    /// Finds the best matching patient in the MPI for the given demographics.
    /// Returns a result indicating whether a match was found (score >= 0.70),
    /// the best candidate, and the confidence score.
    /// </summary>
    Task<MpiMatchResult> FindMatchAsync(string patientName, string ssn, DateTime? dateOfBirth, string? sex);

    /// <summary>
    /// Finds all candidate matches above the specified minimum score threshold.
    /// Returns candidates sorted by descending score.
    /// </summary>
    Task<List<MpiMatchCandidate>> FindCandidatesAsync(string patientName, string? ssn, DateTime? dateOfBirth, string? sex, double minimumScore);
}

/// <summary>
/// Result of an MPI match operation.
/// </summary>
[GenerateSerializer]
public class MpiMatchResult
{
    /// <summary>
    /// Whether a match was found above the threshold (0.70).
    /// </summary>
    [Id(0)]
    public bool MatchFound { get; set; }

    /// <summary>
    /// The best matching candidate, or null if no candidates found.
    /// </summary>
    [Id(1)]
    public MpiMatchCandidate? BestCandidate { get; set; }

    /// <summary>
    /// Confidence score of the best match (0.0 – 1.0).
    /// </summary>
    [Id(2)]
    public double Confidence { get; set; }
}

/// <summary>
/// A candidate patient match from the MPI.
/// </summary>
[GenerateSerializer]
public class MpiMatchCandidate
{
    /// <summary>
    /// Integration Control Number of the candidate.
    /// </summary>
    [Id(0)]
    public string Icn { get; set; } = string.Empty;

    /// <summary>
    /// Patient name of the candidate.
    /// </summary>
    [Id(1)]
    public string PatientName { get; set; } = string.Empty;

    /// <summary>
    /// Social Security Number of the candidate.
    /// </summary>
    [Id(2)]
    public string Ssn { get; set; } = string.Empty;

    /// <summary>
    /// Date of birth of the candidate.
    /// </summary>
    [Id(3)]
    public DateTime? DateOfBirth { get; set; }

    /// <summary>
    /// Sex of the candidate.
    /// </summary>
    [Id(4)]
    public string? Sex { get; set; }

    /// <summary>
    /// Match score (0.0 – 1.0).
    /// </summary>
    [Id(5)]
    public double Score { get; set; }

    /// <summary>
    /// Human-readable breakdown of how the score was calculated.
    /// </summary>
    [Id(6)]
    public string MatchDetails { get; set; } = string.Empty;
}
