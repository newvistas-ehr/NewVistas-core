// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>Program-agnostic domain a case-management goal addresses (broader than, but aligned with, SDOH).</summary>
[GenerateSerializer]
public enum CaseGoalDomain
{
    Housing = 0,
    Food = 1,
    Health = 2,
    BehavioralHealth = 3,
    Employment = 4,
    Education = 5,
    Benefits = 6,
    Legal = 7,
    Transportation = 8,
    Safety = 9,
    Financial = 10,
    Other = 11
}

[GenerateSerializer]
public enum CaseGoalStatus { Active = 0, Achieved = 1, Discontinued = 2, OnHold = 3 }

[GenerateSerializer]
public enum CaseWorkStepStatus { Pending = 0, InProgress = 1, Done = 2, Cancelled = 3 }

/// <summary>A concrete step toward a goal.</summary>
[GenerateSerializer]
public record CaseWorkStep
{
    [Id(0)] public string StepId { get; set; } = string.Empty;
    [Id(1)] public string Description { get; set; } = string.Empty;
    [Id(2)] public CaseWorkStepStatus Status { get; set; }
    [Id(3)] public DateTime? DueDate { get; set; }
    [Id(4)] public DateTime? CompletedDate { get; set; }
}

/// <summary>A scheduled follow-up on a goal.</summary>
[GenerateSerializer]
public record CaseFollowUp
{
    [Id(0)] public string FollowUpId { get; set; } = string.Empty;
    [Id(1)] public DateTime Date { get; set; }
    [Id(2)] public string Note { get; set; } = string.Empty;
    [Id(3)] public bool Completed { get; set; }
}

/// <summary>
/// A case-management goal: what we are trying to achieve for this person in one life domain, the
/// work-steps toward it, scheduled follow-ups, and the recorded outcome. Program-agnostic — it serves
/// social programs and clinical care coordination alike, and can cite the source that opened it
/// (an SDOH screening domain, a Social Work referral).
/// </summary>
[GenerateSerializer]
public record CaseGoal
{
    [Id(0)] public string GoalId { get; set; } = string.Empty;
    [Id(1)] public string Description { get; set; } = string.Empty;
    [Id(2)] public CaseGoalDomain Domain { get; set; }
    [Id(3)] public CaseGoalStatus Status { get; set; }
    [Id(4)] public DateTime? TargetDate { get; set; }
    [Id(5)] public DateTime CreatedDate { get; set; }
    [Id(6)] public string CreatedBy { get; set; } = string.Empty;
    /// <summary>Optional citation to what opened this goal (e.g. "SDOH:{id}", "SW-REFERRAL:{id}").</summary>
    [Id(7)] public string? SourceReference { get; set; }
    [Id(8)] public List<CaseWorkStep> WorkSteps { get; set; } = new();
    [Id(9)] public List<CaseFollowUp> FollowUps { get; set; } = new();
    // ── Outcome ──
    [Id(10)] public bool? OutcomeAchieved { get; set; }
    [Id(11)] public string? OutcomeNote { get; set; }
    [Id(12)] public DateTime? OutcomeDate { get; set; }
}

/// <summary>
/// A patient's case-management record — the longitudinal goal→work-step→follow-up→outcome spine that
/// makes collected social/clinical data actionable and trackable. Grain key: <c>CASE-MGMT:{patientId}</c>.
/// Composes with (does not replace) the clinical nursing/home-care plans.
/// </summary>
[GenerateSerializer]
public class CaseManagementState
{
    [Id(0)] public string PatientId { get; set; } = string.Empty;
    [Id(1)] public List<CaseGoal> Goals { get; set; } = new();
    [Id(2)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>Caseload roll-up row (the caseworker's list of people with open goals).</summary>
[GenerateSerializer]
public record CaseloadEntry
{
    [Id(0)] public string PatientId { get; set; } = string.Empty;
    [Id(1)] public int ActiveGoalCount { get; set; }
    [Id(2)] public DateTime LastModifiedDate { get; set; }
}

/// <summary>Directory of patients with case-management goals. Grain key: <c>CASE-MGMT-INDEX</c>.</summary>
[GenerateSerializer]
public class CaseManagementIndexState
{
    [Id(0)] public List<CaseloadEntry> Entries { get; set; } = new();
}
