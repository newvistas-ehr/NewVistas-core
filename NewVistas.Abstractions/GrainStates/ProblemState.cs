// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Represents a single problem entry embedded in the patient grain.
/// Based on VistA PROBLEM LIST file (#9000011).
/// No PatientId — the patient grain owns this data.
/// </summary>
[GenerateSerializer]
public class ProblemEntry
{
    [Id(0)]
    public string ProblemId { get; set; } = string.Empty;

    /// <summary>
    /// Diagnosis (.01) � pointer to ICD DIAGNOSIS file (#80)
    /// </summary>
    [Id(2)]
    public string Diagnosis { get; set; } = string.Empty;

    [Id(3)]
    public string? DiagnosisCode { get; set; }

    /// <summary>
    /// Status � ACTIVE or INACTIVE
    /// </summary>
    [Id(4)]
    public string Status { get; set; } = "ACTIVE";

    /// <summary>
    /// Date of Onset (.13)
    /// </summary>
    [Id(5)]
    public DateTime? DateOfOnset { get; set; }

    /// <summary>
    /// Date Resolved (1.07)
    /// </summary>
    [Id(6)]
    public DateTime? DateResolved { get; set; }

    /// <summary>
    /// Date Recorded (1.09)
    /// </summary>
    [Id(7)]
    public DateTime DateRecorded { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Recording Provider (.04)
    /// </summary>
    [Id(8)]
    public string? RecordingProviderId { get; set; }

    [Id(9)]
    public string? RecordingProviderName { get; set; }

    /// <summary>
    /// Responsible Provider (1.04)
    /// </summary>
    [Id(10)]
    public string? ResponsibleProviderId { get; set; }

    [Id(11)]
    public string? ResponsibleProviderName { get; set; }

    /// <summary>
    /// Service Connected (1.1)
    /// </summary>
    [Id(12)]
    public bool IsServiceConnected { get; set; }

    /// <summary>
    /// Condition � ACUTE, CHRONIC, TRANSCRIBED, PERMANENT, or HIDDEN
    /// </summary>
    [Id(13)]
    public string? Condition { get; set; }

    /// <summary>
    /// Location of encounter (.08)
    /// </summary>
    [Id(14)]
    public string? ClinicId { get; set; }

    [Id(15)]
    public string? ClinicName { get; set; }

    /// <summary>
    /// Priority � ACUTE or CHRONIC
    /// </summary>
    [Id(16)]
    public string? Priority { get; set; }

    [Id(17)]
    public string? Comments { get; set; }

    [Id(18)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(19)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Deep copy. Used at event/state boundaries so that mutating the live
    /// problem on the patient grain does not retroactively mutate the
    /// historical <see cref="ProblemEntry"/> snapshot stored on a clinical
    /// event payload (which would break the hash chain).
    /// </summary>
    // ── Diagnosis provenance (ADR-006) ──────────────────────────────────────
    //
    // NOTE ON [Id(1)]: that slot is PERMANENTLY RETIRED. The numbering above jumps 0 → 2
    // because [Id(1)] once held a `string PatientId`, removed when problems became embedded
    // in PatientState (commit 9bc23279). Any row persisted under the old shape still carries
    // a string there, so reusing the id would silently corrupt deserialization. New fields
    // therefore start at 20.
    //
    // Every default below reads as *unknown*, which is what makes migration zero lines of code.

    /// <summary>
    /// Id of the assertion event that produced this head. Empty means no assertion event was
    /// ever observed — a legacy row or an import, not an assertion made here.
    /// </summary>
    [Id(20)] public string AssertionId { get; set; } = string.Empty;

    /// <summary>
    /// How many times this diagnosis has been asserted, counting the first. Zero means no
    /// assertion event was observed — deliberately <b>not</b> "revision 1", because backfilling
    /// 1 would claim a deliberate assertion for rows that are copies of someone else's list.
    /// </summary>
    [Id(21)] public int RevisionNumber { get; set; }

    /// <summary>Certainty of the current assertion. <c>Unspecified</c> means nobody stated one.</summary>
    [Id(22)] public ProblemVerificationStatus VerificationStatus { get; set; }

    /// <summary>
    /// Structured citations for the current assertion, including
    /// <see cref="EvidencePolarity.NotAssessed"/> rows recording what was deliberately not checked.
    /// An empty list means no evidence was ever <i>recorded</i> — not that none existed.
    /// </summary>
    [Id(23)] public List<EvidenceRef> Evidence { get; set; } = new();

    /// <summary>The problem this one replaced, when a revision split into a new entry.</summary>
    [Id(24)] public string? SupersedesProblemId { get; set; }

    /// <summary>The problem that replaced this one. Non-null means this entry is historical.</summary>
    [Id(25)] public string? SupersededByProblemId { get; set; }

    /// <summary>
    /// Why this diagnosis last changed. Null means never revised — distinct from
    /// <see cref="RevisionReason.Unspecified"/>, which means "revised, reason not stated."
    /// </summary>
    [Id(26)] public RevisionReason? LastRevisionReason { get; set; }

    /// <summary>The clinician's own words about the last revision.</summary>
    [Id(27)] public string? LastRevisionNarrative { get; set; }

    public ProblemEntry Clone() => new()
    {
        ProblemId = ProblemId,
        Diagnosis = Diagnosis,
        DiagnosisCode = DiagnosisCode,
        Status = Status,
        DateOfOnset = DateOfOnset,
        DateResolved = DateResolved,
        DateRecorded = DateRecorded,
        RecordingProviderId = RecordingProviderId,
        RecordingProviderName = RecordingProviderName,
        ResponsibleProviderId = ResponsibleProviderId,
        ResponsibleProviderName = ResponsibleProviderName,
        IsServiceConnected = IsServiceConnected,
        Condition = Condition,
        ClinicId = ClinicId,
        ClinicName = ClinicName,
        Priority = Priority,
        Comments = Comments,
        CreatedDate = CreatedDate,
        LastModifiedDate = LastModifiedDate,
        AssertionId = AssertionId,
        RevisionNumber = RevisionNumber,
        VerificationStatus = VerificationStatus,
        // Shallow list copy is correct: EvidenceRef is an immutable record, and Clone() exists
        // to stop an event snapshot aliasing live state — the list itself is what must not alias.
        Evidence = new List<EvidenceRef>(Evidence),
        SupersedesProblemId = SupersedesProblemId,
        SupersededByProblemId = SupersededByProblemId,
        LastRevisionReason = LastRevisionReason,
        LastRevisionNarrative = LastRevisionNarrative
    };
}
