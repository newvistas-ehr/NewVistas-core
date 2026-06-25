// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>Clinical domain a grounded fact was drawn from.</summary>
public enum ClinicalFactCategory
{
    Problem = 0,
    Medication = 1,
    Allergy = 2,
    Lab = 3,
    Vital = 4,
}

/// <summary>Lifecycle of a generated summary.</summary>
public enum SummaryStatus
{
    /// <summary>Generated; awaiting clinician review and sign-off.</summary>
    DraftPendingSignoff = 0,

    /// <summary>Reviewed and signed by a clinician.</summary>
    Signed = 1,
}

/// <summary>
/// A single discrete clinical fact pulled from a source grain, with provenance.
/// This is the grounding unit: every sentence in a generated summary must trace
/// back to one or more of these, and each carries the grain + id it came from so a
/// reviewer (or an automated check) can verify it against the source of truth.
/// </summary>
[GenerateSerializer]
public class ClinicalFact
{
    /// <summary>Local id within a summary context (e.g., "F3"); cited by claims.</summary>
    [Id(0)]
    public string FactId { get; set; } = string.Empty;

    /// <summary>Clinical domain this fact belongs to.</summary>
    [Id(1)]
    public ClinicalFactCategory Category { get; set; }

    /// <summary>Human-readable fact text (e.g., "Type 2 diabetes mellitus (E11.9)").</summary>
    [Id(2)]
    public string Text { get; set; } = string.Empty;

    /// <summary>Source grain the fact was read from (e.g., "ProblemGrain").</summary>
    [Id(3)]
    public string SourceGrain { get; set; } = string.Empty;

    /// <summary>Source record id within that grain (problem id, prescription id, LOINC, …).</summary>
    [Id(4)]
    public string SourceId { get; set; } = string.Empty;
}

/// <summary>
/// The grounded context bundle assembled for one summary: the discrete facts (with
/// provenance) the narrative is allowed to draw from. Retrieval happens BEFORE
/// generation — the model narrates these facts, it does not supply them.
/// </summary>
[GenerateSerializer]
public class ClinicalSummaryContext
{
    [Id(0)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Why the summary is being generated (e.g., "pre-op", "consult handoff").</summary>
    [Id(1)]
    public string Purpose { get; set; } = string.Empty;

    [Id(2)]
    public List<ClinicalFact> Facts { get; set; } = new();
}

/// <summary>
/// One sentence/assertion in a generated summary together with the source facts that
/// back it. <see cref="Verified"/> is set by the verification pass: a claim that cites
/// no fact, or a fact not present in the context, is flagged rather than shown as trusted.
/// </summary>
[GenerateSerializer]
public class SummaryClaim
{
    [Id(0)]
    public string Text { get; set; } = string.Empty;

    /// <summary>FactIds (from the context) that support this claim.</summary>
    [Id(1)]
    public List<string> SupportingFactIds { get; set; } = new();

    /// <summary>True once the claim is confirmed grounded in the source facts.</summary>
    [Id(2)]
    public bool Verified { get; set; }

    /// <summary>Why a claim was flagged, when not verified.</summary>
    [Id(3)]
    public string? VerificationNote { get; set; }
}

/// <summary>
/// A generated clinical summary draft: the narrative, the claims with their grounding
/// and verification state, the facts it was built from, and sign-off status. Never
/// shown as final — a clinician reviews and signs.
/// </summary>
[GenerateSerializer]
public class ClinicalSummaryDraft
{
    [Id(0)]
    public string PatientId { get; set; } = string.Empty;

    [Id(1)]
    public string Purpose { get; set; } = string.Empty;

    [Id(2)]
    public string Narrative { get; set; } = string.Empty;

    [Id(3)]
    public List<SummaryClaim> Claims { get; set; } = new();

    /// <summary>The grounded facts the draft was generated from (the audit trail).</summary>
    [Id(4)]
    public List<ClinicalFact> GroundingFacts { get; set; } = new();

    /// <summary>Which narrative provider produced it (e.g., "offline-template", "claude").</summary>
    [Id(5)]
    public string ModelProvider { get; set; } = string.Empty;

    [Id(6)]
    public SummaryStatus Status { get; set; } = SummaryStatus.DraftPendingSignoff;

    [Id(7)]
    public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Count of claims that failed verification — must be reviewed before trust.</summary>
    [Id(8)]
    public int UnverifiedClaimCount { get; set; }

    [Id(9)]
    public string? SignedBy { get; set; }

    [Id(10)]
    public DateTime? SignedDate { get; set; }

    /// <summary>
    /// Setup notice to surface to the clinician (e.g., live AI enabled but no API key, so this
    /// is the offline summary and the text says how to configure a key). Null in the normal case.
    /// </summary>
    [Id(11)]
    public string? ConfigurationNotice { get; set; }
}

/// <summary>
/// Persistent state for the per-patient summary grain: the current draft (if any).
/// Keyed by patient id.
/// </summary>
[GenerateSerializer]
public class PatientSummaryState
{
    [Id(0)]
    public string PatientId { get; set; } = string.Empty;

    [Id(1)]
    public ClinicalSummaryDraft? CurrentDraft { get; set; }
}
