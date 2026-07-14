// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// A health-related social need domain (the AHC-HRSN / PRAPARE core domains). Each maps to an ICD-10
/// Z-code and a community-referral service type via <c>SdohScreeningCatalog</c>.
/// </summary>
[GenerateSerializer]
public enum SdohDomain
{
    HousingInstability = 0,
    Homelessness = 1,
    FoodInsecurity = 2,
    TransportationInsecurity = 3,
    UtilityNeeds = 4,
    InterpersonalSafety = 5,
    FinancialStrain = 6,
    Employment = 7,
    Education = 8
}

/// <summary>Answer to a screening domain. Trinary — "not assessed" is distinct from "negative".</summary>
[GenerateSerializer]
public enum SdohResponse
{
    Unknown = 0,
    Negative = 1,
    Positive = 2
}

/// <summary>What a clinician did about a positive domain (the closed-loop intervention).</summary>
[GenerateSerializer]
public enum SdohActionType
{
    ProblemAdded = 0,     // Z-code placed on the problem list
    ReferralCreated = 1   // Social Work referral opened
}

/// <summary>One domain's answer on a screening.</summary>
[GenerateSerializer]
public record SdohScreeningResponse
{
    [Id(0)] public SdohDomain Domain { get; set; }
    [Id(1)] public SdohResponse Response { get; set; }
    [Id(2)] public string? Note { get; set; }
}

/// <summary>
/// A positive-domain finding: the coded consequence of a positive screen — the mapped Z-code and the
/// suggested referral service type. Produced by <c>SdohScreeningCatalog.Evaluate</c>.
/// </summary>
[GenerateSerializer]
public record SdohFinding
{
    [Id(0)] public SdohDomain Domain { get; set; }
    [Id(1)] public string Display { get; set; } = string.Empty;
    [Id(2)] public string ZCode { get; set; } = string.Empty;
    [Id(3)] public string ZCodeDisplay { get; set; } = string.Empty;
    [Id(4)] public SocialWorkReferralServiceType ReferralServiceType { get; set; }
}

/// <summary>A recorded closed-loop action taken for a positive domain.</summary>
[GenerateSerializer]
public record SdohActionRecord
{
    [Id(0)] public SdohDomain Domain { get; set; }
    [Id(1)] public SdohActionType ActionType { get; set; }
    /// <summary>The created problem id or referral id.</summary>
    [Id(2)] public string TargetId { get; set; } = string.Empty;
    [Id(3)] public DateTime Date { get; set; }
    [Id(4)] public string By { get; set; } = string.Empty;
}

/// <summary>
/// One SDOH screening event. Grain key: <c>SDOH:{guid}</c>. Records the per-domain answers, the
/// computed positive-domain findings (Z-code + referral suggestion), and the closed-loop actions taken.
/// </summary>
[GenerateSerializer]
public class SdohScreeningState
{
    [Id(0)] public string ScreeningId { get; set; } = string.Empty;
    [Id(1)] public string PatientId { get; set; } = string.Empty;
    /// <summary>Instrument name (e.g. "AHC-HRSN", "PRAPARE").</summary>
    [Id(2)] public string InstrumentName { get; set; } = string.Empty;
    [Id(3)] public DateTime ScreeningDate { get; set; }
    [Id(4)] public List<SdohScreeningResponse> Responses { get; set; } = new();
    /// <summary>Positive-domain findings computed by the catalog at record time.</summary>
    [Id(5)] public List<SdohFinding> Findings { get; set; } = new();
    [Id(6)] public List<SdohActionRecord> Actions { get; set; } = new();
    [Id(7)] public string RecordedBy { get; set; } = string.Empty;
    [Id(8)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>Summary row for a patient's screening index.</summary>
[GenerateSerializer]
public record SdohScreeningSummary
{
    [Id(0)] public string ScreeningId { get; set; } = string.Empty;
    [Id(1)] public string InstrumentName { get; set; } = string.Empty;
    [Id(2)] public DateTime ScreeningDate { get; set; }
    [Id(3)] public int PositiveDomainCount { get; set; }
}

/// <summary>Per-patient index of SDOH screenings. Grain key: <c>SDOH-IDX:{patientId}</c>.</summary>
[GenerateSerializer]
public class SdohScreeningIndexState
{
    [Id(0)] public string PatientId { get; set; } = string.Empty;
    [Id(1)] public List<SdohScreeningSummary> Entries { get; set; } = new();
}

/// <summary>
/// Reverse-index shard: patients with a positive screen for one SDOH domain. Grain key:
/// <c>SDOH-COHORT:{domain}</c>. Powers population reporting ("how many screen positive for food insecurity").
/// </summary>
[GenerateSerializer]
public class SdohCohortState
{
    [Id(0)] public string Domain { get; set; } = string.Empty;
    [Id(1)] public HashSet<string> PatientIds { get; set; } = new();
}
