// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Nursing care plan for a single patient — VistA File #212 (NURSING PATIENT).
/// Holds the full set of nursing diagnoses (NANDA-style), measurable goals,
/// nursing interventions, and outcome evaluations.
/// </summary>
[GenerateSerializer]
public class NursingCarePlanState
{
    [Id(0)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Active and resolved nursing diagnoses for this patient.</summary>
    [Id(1)] public List<NursingCarePlanProblem> Problems { get; set; } = new();

    [Id(2)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
    [Id(3)] public string? LastModifiedById { get; set; }
    [Id(4)] public string? LastModifiedByName { get; set; }
}

/// <summary>A nursing diagnosis (NANDA-style problem) with goals, interventions, and evaluations.</summary>
[GenerateSerializer]
public class NursingCarePlanProblem
{
    /// <summary>Unique problem ID (prefix NDP-).</summary>
    [Id(0)] public string ProblemId { get; set; } = string.Empty;

    /// <summary>NANDA nursing diagnosis label (e.g., "Acute Pain", "Impaired Physical Mobility").</summary>
    [Id(1)] public string NursingDiagnosis { get; set; } = string.Empty;

    /// <summary>Etiology / related factors ("related to" clause).</summary>
    [Id(2)] public string? RelatedTo { get; set; }

    /// <summary>Defining characteristics ("as evidenced by" clause).</summary>
    [Id(3)] public string? EvidencedBy { get; set; }

    [Id(4)] public NursingCarePlanStatus Status { get; set; } = NursingCarePlanStatus.Active;

    [Id(5)] public DateTime CreatedDateTime { get; set; }
    [Id(6)] public string? CreatedById { get; set; }
    [Id(7)] public string? CreatedByName { get; set; }

    /// <summary>Measurable patient goals linked to this diagnosis.</summary>
    [Id(8)] public List<NursingGoal> Goals { get; set; } = new();

    /// <summary>Nursing actions prescribed for this diagnosis.</summary>
    [Id(9)] public List<NursingIntervention> Interventions { get; set; } = new();

    /// <summary>Periodic outcome evaluations.</summary>
    [Id(10)] public List<NursingOutcomeEvaluation> OutcomeEvaluations { get; set; } = new();

    [Id(11)] public DateTime? ResolvedDateTime { get; set; }
    [Id(12)] public string? ResolutionNotes { get; set; }

    /// <summary>Nursing problem priority (1 = highest).</summary>
    [Id(13)] public int? Priority { get; set; }
}

/// <summary>A measurable patient outcome goal linked to a nursing diagnosis.</summary>
[GenerateSerializer]
public class NursingGoal
{
    /// <summary>Unique goal ID (prefix NGL-).</summary>
    [Id(0)] public string GoalId { get; set; } = string.Empty;

    /// <summary>Measurable outcome statement (e.g., "Patient will report pain ≤3/10 by EOD").</summary>
    [Id(1)] public string GoalText { get; set; } = string.Empty;

    [Id(2)] public DateTime? TargetDate { get; set; }
    [Id(3)] public NursingGoalStatus Status { get; set; } = NursingGoalStatus.Pending;
}

/// <summary>A nursing intervention (action order) prescribed for a nursing diagnosis.</summary>
[GenerateSerializer]
public class NursingIntervention
{
    /// <summary>Unique intervention ID (prefix NIV-).</summary>
    [Id(0)] public string InterventionId { get; set; } = string.Empty;

    /// <summary>Action statement (e.g., "Reposition patient every 2 hours").</summary>
    [Id(1)] public string InterventionText { get; set; } = string.Empty;

    /// <summary>Frequency directive (e.g., "Q2H", "PRN pain", "BID", "Continuous").</summary>
    [Id(2)] public string? Frequency { get; set; }

    [Id(3)] public bool IsActive { get; set; } = true;
    [Id(4)] public DateTime CreatedDateTime { get; set; }
    [Id(5)] public string? CreatedById { get; set; }
    [Id(6)] public string? CreatedByName { get; set; }
}

/// <summary>Periodic evaluation of goal achievement for a nursing diagnosis.</summary>
[GenerateSerializer]
public class NursingOutcomeEvaluation
{
    /// <summary>Unique evaluation ID (prefix NOE-).</summary>
    [Id(0)] public string EvaluationId { get; set; } = string.Empty;

    [Id(1)] public DateTime EvaluationDateTime { get; set; }
    [Id(2)] public string EvaluatedById { get; set; } = string.Empty;
    [Id(3)] public string EvaluatedByName { get; set; } = string.Empty;
    [Id(4)] public NursingOutcomeRating OutcomeRating { get; set; }
    [Id(5)] public string? Notes { get; set; }

    /// <summary>Problem this evaluation is linked to.</summary>
    [Id(6)] public string? ProblemId { get; set; }
}
