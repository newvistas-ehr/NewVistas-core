// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainStates;

// ── Enums ────────────────────────────────────────────────────────────────────

/// <summary>Result of a DVBIC-inspired 4-question TBI screening.</summary>
[GenerateSerializer]
public enum TBIScreeningResult
{
    Negative = 0,
    PositiveRequiresEvaluation = 1,
    Inconclusive = 2
}

/// <summary>TBI severity classification from full clinical evaluation.</summary>
[GenerateSerializer]
public enum TBISeverity
{
    Mild = 0,
    ModerateSevere = 1,
    Penetrating = 2
}

/// <summary>Primary mechanism of trauma injury.</summary>
[GenerateSerializer]
public enum TraumaMechanism
{
    BlastExplosion = 0,
    MVA = 1,
    Fall = 2,
    AssaultPhysical = 3,
    SportsRecreation = 4,
    MilitaryCombat = 5,
    Other = 6
}

/// <summary>Status of a patient's polytrauma care enrollment.</summary>
[GenerateSerializer]
public enum PolytraumaStatus
{
    Active = 0,
    Inactive = 1,
    Transferred = 2,
    Deceased = 3
}

/// <summary>Abbreviated Injury Scale severity category (AIS 1–5).</summary>
[GenerateSerializer]
public enum InjurySeverityScore
{
    Minor = 1,
    Moderate = 2,
    Serious = 3,
    Severe = 4,
    Critical = 5
}

/// <summary>Body region for AIS-coded injury classification.</summary>
[GenerateSerializer]
public enum BodyRegion
{
    Head = 0,
    Face = 1,
    Neck = 2,
    Thorax = 3,
    Abdomen = 4,
    Spine = 5,
    UpperExtremity = 6,
    LowerExtremity = 7,
    External = 8
}

// ── Supporting Types ─────────────────────────────────────────────────────────

/// <summary>One DVBIC TBI screening question with the patient's answer.</summary>
[GenerateSerializer]
public class TBIScreeningAnswer
{
    /// <summary>Question number (1–4 for standard DVBIC screen).</summary>
    [Id(0)] public int QuestionNumber { get; set; }

    /// <summary>Text of the screening question.</summary>
    [Id(1)] public string QuestionText { get; set; } = string.Empty;

    /// <summary>Patient's answer (true = yes/positive).</summary>
    [Id(2)] public bool Answer { get; set; }
}

/// <summary>A single documented injury with AIS severity for ISS calculation.</summary>
[GenerateSerializer]
public class PolytraumaInjury
{
    /// <summary>Unique identifier for this injury record (auto-assigned GUID).</summary>
    [Id(0)] public string InjuryId { get; set; } = string.Empty;

    /// <summary>Body region classification per AIS.</summary>
    [Id(1)] public BodyRegion BodyRegion { get; set; }

    /// <summary>Free-text description of the injury.</summary>
    [Id(2)] public string InjuryDescription { get; set; } = string.Empty;

    /// <summary>Abbreviated Injury Scale score (1–6).</summary>
    [Id(3)] public int AisScore { get; set; }

    /// <summary>Categorical severity derived from AIS score.</summary>
    [Id(4)] public InjurySeverityScore SeverityScore { get; set; }

    /// <summary>Date injury was clinically resolved (null if ongoing).</summary>
    [Id(5)] public DateTime? ResolvedDate { get; set; }

    /// <summary>Clinical notes about this injury.</summary>
    [Id(6)] public string? Notes { get; set; }
}

/// <summary>Summary entry for the per-patient TBI screening index.</summary>
[GenerateSerializer]
public class TBIScreeningSummaryEntry
{
    [Id(0)] public string ScreeningId { get; set; } = string.Empty;
    [Id(1)] public string PatientId { get; set; } = string.Empty;
    [Id(2)] public string PatientName { get; set; } = string.Empty;
    [Id(3)] public DateTime ScreeningDate { get; set; }
    [Id(4)] public TBIScreeningResult Result { get; set; }
    [Id(5)] public string ScreenedById { get; set; } = string.Empty;
    [Id(6)] public string ScreenedByName { get; set; } = string.Empty;
    [Id(7)] public bool TriggeredFullEvaluation { get; set; }
}

/// <summary>Summary entry for the polytrauma registry index.</summary>
[GenerateSerializer]
public class PolytraumaRegistrySummaryEntry
{
    [Id(0)] public string PatientId { get; set; } = string.Empty;
    [Id(1)] public string PatientName { get; set; } = string.Empty;
    [Id(2)] public PolytraumaStatus Status { get; set; }
    [Id(3)] public DateTime RegistrationDate { get; set; }
    [Id(4)] public string PrimaryCareTeam { get; set; } = string.Empty;
    [Id(5)] public TBISeverity? TBISeverity { get; set; }
    [Id(6)] public int InjuryCount { get; set; }
    [Id(7)] public int IssTotalScore { get; set; }
    [Id(8)] public DateTime LastModifiedDate { get; set; }
}

// ── State Classes ─────────────────────────────────────────────────────────────

/// <summary>
/// Persisted state for a single TBI screening encounter.
/// Inspired by DVBIC 4-question screen used at VA post-deployment / primary care.
/// </summary>
[GenerateSerializer]
public class TBIScreeningState
{
    /// <summary>Unique identifier for this screening (grain key).</summary>
    [Id(0)] public string ScreeningId { get; set; } = string.Empty;

    /// <summary>Patient this screening belongs to.</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient display name.</summary>
    [Id(2)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Date and time the screening was performed.</summary>
    [Id(3)] public DateTime ScreeningDate { get; set; }

    /// <summary>Physical or virtual location of the screening encounter.</summary>
    [Id(4)] public string ScreeningLocation { get; set; } = string.Empty;

    /// <summary>Provider or staff who administered the screen.</summary>
    [Id(5)] public string ScreenedById { get; set; } = string.Empty;

    /// <summary>Screener display name.</summary>
    [Id(6)] public string ScreenedByName { get; set; } = string.Empty;

    /// <summary>Type of encounter (e.g., Primary Care, Post-Deployment, Specialty).</summary>
    [Id(7)] public string EncounterType { get; set; } = string.Empty;

    /// <summary>Patient responses to each DVBIC screening question.</summary>
    [Id(8)] public List<TBIScreeningAnswer> Answers { get; set; } = new();

    /// <summary>Final result of the screening.</summary>
    [Id(9)] public TBIScreeningResult Result { get; set; }

    /// <summary>Number of questions answered positively (yes).</summary>
    [Id(10)] public int PositiveAnswerCount { get; set; }

    /// <summary>Whether this positive screen triggered a full TBI evaluation referral.</summary>
    [Id(11)] public bool TriggeredFullEvaluation { get; set; }

    /// <summary>Date the follow-on full TBI evaluation was completed.</summary>
    [Id(12)] public DateTime? FullEvaluationDate { get; set; }

    /// <summary>Provider who conducted the full evaluation.</summary>
    [Id(13)] public string? FullEvaluationProviderId { get; set; }

    /// <summary>Full evaluation provider display name.</summary>
    [Id(14)] public string? FullEvaluationProviderName { get; set; }

    /// <summary>TBI severity confirmed by full evaluation (null if not yet evaluated).</summary>
    [Id(15)] public TBISeverity? ConfirmedTBISeverity { get; set; }

    /// <summary>Clinical notes on the screening encounter.</summary>
    [Id(16)] public string? Notes { get; set; }

    /// <summary>Date/time the screening record was created.</summary>
    [Id(17)] public DateTime CreatedDate { get; set; }
}

/// <summary>
/// Persisted state for a patient's polytrauma enrollment record.
/// Tracks injuries (AIS/ISS), TBI status, rehabilitation, and VA Polytrauma System of Care status.
/// </summary>
[GenerateSerializer]
public class PolytraumaRecordState
{
    /// <summary>Patient identifier (grain key suffix).</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient display name.</summary>
    [Id(1)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Patient date of birth.</summary>
    [Id(2)] public DateTime? DateOfBirth { get; set; }

    /// <summary>Current polytrauma care status.</summary>
    [Id(3)] public PolytraumaStatus Status { get; set; }

    /// <summary>Date the patient was enrolled in the polytrauma registry.</summary>
    [Id(4)] public DateTime RegistrationDate { get; set; }

    /// <summary>Date care was deactivated (transferred, deceased, etc.).</summary>
    [Id(5)] public DateTime? DeactivationDate { get; set; }

    /// <summary>Primary mechanism by which trauma occurred.</summary>
    [Id(6)] public TraumaMechanism TraumaMechanism { get; set; }

    /// <summary>Date of the traumatic event.</summary>
    [Id(7)] public DateTime? TraumaDate { get; set; }

    /// <summary>Location or theatre where the trauma occurred.</summary>
    [Id(8)] public string TraumaLocation { get; set; } = string.Empty;

    /// <summary>List of documented injuries with AIS codes.</summary>
    [Id(9)] public List<PolytraumaInjury> Injuries { get; set; } = new();

    /// <summary>Injury Severity Score total (sum of squared top-3 AIS regions).</summary>
    [Id(10)] public int IssTotalScore { get; set; }

    /// <summary>Confirmed TBI severity (null if no TBI diagnosis).</summary>
    [Id(11)] public TBISeverity? TBISeverity { get; set; }

    /// <summary>Whether the patient has a co-existing TBI diagnosis.</summary>
    [Id(12)] public bool HasTBI { get; set; }

    /// <summary>Whether the patient carries a PTSD diagnosis.</summary>
    [Id(13)] public bool PtsdDiagnosis { get; set; }

    /// <summary>Whether the patient carries a chronic pain diagnosis.</summary>
    [Id(14)] public bool ChronicPainDiagnosis { get; set; }

    /// <summary>Rehabilitation goals narrative.</summary>
    [Id(15)] public string? RehabGoals { get; set; }

    /// <summary>Primary polytrauma care team identifier.</summary>
    [Id(16)] public string PrimaryPolytraumaTeamId { get; set; } = string.Empty;

    /// <summary>Primary polytrauma care team display name.</summary>
    [Id(17)] public string PrimaryPolytraumaTeamName { get; set; } = string.Empty;

    /// <summary>Assigned case manager identifier.</summary>
    [Id(18)] public string CaseManagerId { get; set; } = string.Empty;

    /// <summary>Case manager display name.</summary>
    [Id(19)] public string CaseManagerName { get; set; } = string.Empty;

    /// <summary>VA Polytrauma Network Site designation (e.g., PRC, PTRP, PSC, LPPOC).</summary>
    [Id(20)] public string PolytraumaNetworkSite { get; set; } = string.Empty;

    /// <summary>Source of the referral to polytrauma care.</summary>
    [Id(21)] public string ReferralSource { get; set; } = string.Empty;

    /// <summary>Site to which patient was transferred (if applicable).</summary>
    [Id(22)] public string? TransferredToSite { get; set; }

    /// <summary>Clinical notes.</summary>
    [Id(23)] public string? Notes { get; set; }

    /// <summary>Date/time the record was created.</summary>
    [Id(24)] public DateTime CreatedDate { get; set; }

    /// <summary>Date/time the record was last updated.</summary>
    [Id(25)] public DateTime LastModifiedDate { get; set; }
}
