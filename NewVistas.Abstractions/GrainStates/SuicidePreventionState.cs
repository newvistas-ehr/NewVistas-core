// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

// ── Enums ─────────────────────────────────────────────────────────────────────

[GenerateSerializer]
public enum RiskLevel
{
    NotAssessed,
    Low,
    Moderate,
    High,
    Imminent,
}

[GenerateSerializer]
public enum SafetyPlanStatus
{
    Draft,
    Active,
    Updated,
    Archived,
}

[GenerateSerializer]
public enum FollowUpContactType
{
    Phone,
    InPerson,
    SecureMessage,
    UnableToReach,
}

[GenerateSerializer]
public enum FollowUpContactOutcome
{
    Contacted,
    LeftMessage,
    NoAnswer,
    Refused,
    Hospitalized,
}

// ── Supporting Types ──────────────────────────────────────────────────────────

[GenerateSerializer]
public class SupportContact
{
    /// <summary>Full name of the social support person.</summary>
    [Id(0)] public string Name { get; set; } = string.Empty;

    /// <summary>Phone number to reach this contact.</summary>
    [Id(1)] public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>Relationship to patient (e.g., "Spouse", "Friend").</summary>
    [Id(2)] public string Relationship { get; set; } = string.Empty;
}

[GenerateSerializer]
public class ProfessionalContact
{
    /// <summary>Full name of the professional contact.</summary>
    [Id(0)] public string Name { get; set; } = string.Empty;

    /// <summary>Phone number for this professional contact.</summary>
    [Id(1)] public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>Agency or organization (e.g., "VA Medical Center", "Veterans Crisis Line").</summary>
    [Id(2)] public string Agency { get; set; } = string.Empty;

    /// <summary>Role of the contact (e.g., "Psychiatrist", "Social Worker", "Crisis Line").</summary>
    [Id(3)] public string Role { get; set; } = string.Empty;
}

[GenerateSerializer]
public class FollowUpContact
{
    /// <summary>Unique identifier for this follow-up contact record.</summary>
    [Id(0)] public string ContactId { get; set; } = string.Empty;

    /// <summary>Date and time of the follow-up attempt.</summary>
    [Id(1)] public DateTime ContactDate { get; set; }

    /// <summary>Method used for the follow-up attempt.</summary>
    [Id(2)] public FollowUpContactType ContactType { get; set; }

    /// <summary>Outcome of the follow-up attempt.</summary>
    [Id(3)] public FollowUpContactOutcome Outcome { get; set; }

    /// <summary>Name of the provider who made the follow-up contact.</summary>
    [Id(4)] public string ProviderName { get; set; } = string.Empty;

    /// <summary>Clinical notes about this follow-up contact.</summary>
    [Id(5)] public string Notes { get; set; } = string.Empty;
}

[GenerateSerializer]
public class RiskDesignationEntry
{
    /// <summary>Date this risk designation was made.</summary>
    [Id(0)] public DateTime DesignatedDate { get; set; }

    /// <summary>Risk level assigned at this designation.</summary>
    [Id(1)] public RiskLevel RiskLevel { get; set; }

    /// <summary>Identifier of the provider making this designation.</summary>
    [Id(2)] public string ProviderId { get; set; } = string.Empty;

    /// <summary>Name of the provider making this designation.</summary>
    [Id(3)] public string ProviderName { get; set; } = string.Empty;
}

[GenerateSerializer]
public class SafetyPlanSummary
{
    /// <summary>Unique identifier for this safety plan.</summary>
    [Id(0)] public string PlanId { get; set; } = string.Empty;

    /// <summary>Patient identifier.</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient full name.</summary>
    [Id(2)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Current status of the safety plan.</summary>
    [Id(3)] public SafetyPlanStatus Status { get; set; }

    /// <summary>Date the plan was originally created.</summary>
    [Id(4)] public DateTime CreatedDate { get; set; }

    /// <summary>Date the plan was last reviewed with the patient.</summary>
    [Id(5)] public DateTime? LastReviewedDate { get; set; }
}

[GenerateSerializer]
public class PatientHighRiskSummary
{
    /// <summary>Patient identifier.</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Patient full name.</summary>
    [Id(1)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Current assessed risk level.</summary>
    [Id(2)] public RiskLevel CurrentRiskLevel { get; set; }

    /// <summary>Whether the patient is currently flagged as High Risk for Suicide.</summary>
    [Id(3)] public bool IsHighRiskFlagged { get; set; }

    /// <summary>Date of the most recent follow-up contact.</summary>
    [Id(4)] public DateTime? LastContactDate { get; set; }

    /// <summary>Number of active or draft safety plans for this patient.</summary>
    [Id(5)] public int ActivePlanCount { get; set; }

    /// <summary>Date and time this summary was last modified.</summary>
    [Id(6)] public DateTime LastModifiedDate { get; set; }
}

// ── Primary State Classes ─────────────────────────────────────────────────────

[GenerateSerializer]
public class SafetyPlanState
{
    // Identity (0–4)
    /// <summary>Unique identifier for this safety plan (matches grain key suffix).</summary>
    [Id(0)] public string PlanId { get; set; } = string.Empty;

    /// <summary>Identifier of the patient this plan belongs to.</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Full name of the patient.</summary>
    [Id(2)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Identifier of the provider who created this plan.</summary>
    [Id(3)] public string ProviderId { get; set; } = string.Empty;

    /// <summary>Name of the provider who created this plan.</summary>
    [Id(4)] public string ProviderName { get; set; } = string.Empty;

    // Metadata (5–7)
    /// <summary>Current status of the safety plan.</summary>
    [Id(5)] public SafetyPlanStatus Status { get; set; } = SafetyPlanStatus.Draft;

    /// <summary>Date and time the plan was originally created.</summary>
    [Id(6)] public DateTime CreatedDate { get; set; }

    /// <summary>Date of the most recent review session with the patient.</summary>
    [Id(7)] public DateTime? LastReviewedDate { get; set; }

    // Stanley-Brown Safety Planning sections (8–16)
    /// <summary>Warning signs the patient recognizes as indicating a developing crisis.</summary>
    [Id(8)] public List<string> WarningSigns { get; set; } = new();

    /// <summary>Internal coping strategies the patient can use without contacting others.</summary>
    [Id(9)] public List<string> InternalCopingStrategies { get; set; } = new();

    /// <summary>Social contacts and distracting activities that take mind off the crisis.</summary>
    [Id(10)] public List<string> DistractionContacts { get; set; } = new();

    /// <summary>People the patient can ask for help (name + phone).</summary>
    [Id(11)] public List<SupportContact> SupportContacts { get; set; } = new();

    /// <summary>Professionals and agencies the patient can contact in crisis.</summary>
    [Id(12)] public List<ProfessionalContact> ProfessionalContacts { get; set; } = new();

    /// <summary>Crisis line phone numbers (e.g., Veterans Crisis Line: 988 press 1).</summary>
    [Id(13)] public List<string> CrisisLineNumbers { get; set; } = new();

    /// <summary>Lethal means removed from the patient's environment (means restriction).</summary>
    [Id(14)] public List<string> MeansRemoved { get; set; } = new();

    /// <summary>Notes on environmental safety and means restriction steps taken.</summary>
    [Id(15)] public string EnvironmentSafetyNotes { get; set; } = string.Empty;

    /// <summary>Reasons the patient identifies for living.</summary>
    [Id(16)] public List<string> ReasonsForLiving { get; set; } = new();

    // Audit (17)
    /// <summary>Date and time this record was last modified.</summary>
    [Id(17)] public DateTime LastModifiedDate { get; set; }
}

[GenerateSerializer]
public class PatientRiskState
{
    /// <summary>Identifier of the patient.</summary>
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Full name of the patient.</summary>
    [Id(1)] public string PatientName { get; set; } = string.Empty;

    /// <summary>Current assessed risk level for suicide.</summary>
    [Id(2)] public RiskLevel CurrentRiskLevel { get; set; } = RiskLevel.NotAssessed;

    /// <summary>Whether the patient is currently flagged as High Risk for Suicide (PRF equivalent).</summary>
    [Id(3)] public bool IsHighRiskFlagged { get; set; }

    /// <summary>History of all risk level designations for this patient.</summary>
    [Id(4)] public List<RiskDesignationEntry> DesignationHistory { get; set; } = new();

    /// <summary>Follow-up contact records for this patient.</summary>
    [Id(5)] public List<FollowUpContact> FollowUpContacts { get; set; } = new();

    /// <summary>Date and time this record was last modified.</summary>
    [Id(6)] public DateTime LastModifiedDate { get; set; }
}
