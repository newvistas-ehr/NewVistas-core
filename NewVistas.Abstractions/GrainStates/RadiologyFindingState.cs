// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>Graded severity of a radiology finding (best-effort normalization of the report's wording).</summary>
public enum FindingSeverity
{
    Unspecified = 0,
    Minimal = 1,
    Mild = 2,
    Moderate = 3,
    Severe = 4,
}

/// <summary>Laterality of a finding.</summary>
public enum FindingLaterality
{
    Unspecified = 0,
    Left = 1,
    Right = 2,
    Bilateral = 3,
}

/// <summary>Provider disposition of a finding within the acknowledgment gate.</summary>
public enum FindingAcknowledgment
{
    /// <summary>Surfaced but not yet acted on — the forcing-function state.</summary>
    Pending = 0,

    /// <summary>Clinician confirmed the finding is noted and accounted for.</summary>
    Acknowledged = 1,

    /// <summary>Clinician actively rejected the finding — requires a recorded reason.</summary>
    Rejected = 2,
}

/// <summary>
/// One discrete finding extracted from a radiology report. The AI does not diagnose —
/// it surfaces a finding the radiologist already documented, anchored to the verbatim
/// <see cref="SourceQuote"/> sentence. <see cref="QuoteVerified"/> records that the quote
/// actually appears in the report, so an invented finding is flagged rather than trusted.
///
/// Material findings (see <see cref="RequiresAcknowledgment"/>) must be acknowledged or
/// rejected-with-reason before an irreversible step — the forcing function. A rejection is
/// recorded and patient-visible so a careless dismissal is neither free nor invisible.
/// </summary>
[GenerateSerializer]
public class RadiologyFinding
{
    [Id(0)]
    public string FindingId { get; set; } = string.Empty;

    /// <summary>What the finding is, e.g., "Neural foraminal stenosis", "Central canal stenosis".</summary>
    [Id(1)]
    public string FindingType { get; set; } = string.Empty;

    /// <summary>Anatomic level, e.g., "C5-C6". Best-effort from the source sentence.</summary>
    [Id(2)]
    public string Level { get; set; } = string.Empty;

    [Id(3)]
    public FindingLaterality Laterality { get; set; } = FindingLaterality.Unspecified;

    [Id(4)]
    public FindingSeverity Severity { get; set; } = FindingSeverity.Unspecified;

    /// <summary>Raw severity wording from the report (e.g., "moderate to severe").</summary>
    [Id(5)]
    public string SeverityText { get; set; } = string.Empty;

    /// <summary>The verbatim sentence from the report this finding was drawn from (the citation).</summary>
    [Id(6)]
    public string SourceQuote { get; set; } = string.Empty;

    /// <summary>True once the source quote is confirmed present in the report text.</summary>
    [Id(7)]
    public bool QuoteVerified { get; set; }

    /// <summary>Why a finding failed verification, when not verified.</summary>
    [Id(8)]
    public string? VerificationNote { get; set; }

    /// <summary>Whether this finding is material enough to force an acknowledge/reject decision.</summary>
    [Id(9)]
    public bool RequiresAcknowledgment { get; set; }

    [Id(10)]
    public FindingAcknowledgment Acknowledgment { get; set; } = FindingAcknowledgment.Pending;

    [Id(11)]
    public string? DispositionedBy { get; set; }

    [Id(12)]
    public DateTime? DispositionedDate { get; set; }

    /// <summary>Recorded reason a clinician gave for rejecting the finding.</summary>
    [Id(13)]
    public string? RejectionReason { get; set; }

    /// <summary>Whether the disposition (and its reason) is visible to the patient.</summary>
    [Id(14)]
    public bool PatientVisible { get; set; }
}

/// <summary>
/// Per-report extraction record: the source report, the discrete findings extracted from it
/// with their acknowledgment state, and provenance. Keyed by report id.
/// </summary>
[GenerateSerializer]
public class RadiologyExtractionState
{
    [Id(0)]
    public string ReportId { get; set; } = string.Empty;

    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>The source radiology report the findings were extracted from (the ground truth).</summary>
    [Id(2)]
    public string ReportText { get; set; } = string.Empty;

    [Id(3)]
    public string ExtractedBy { get; set; } = string.Empty;

    [Id(4)]
    public DateTime ExtractedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Which extractor produced the findings (e.g., "offline-heuristic", "claude").</summary>
    [Id(5)]
    public string ModelProvider { get; set; } = string.Empty;

    [Id(6)]
    public List<RadiologyFinding> Findings { get; set; } = new();
}
