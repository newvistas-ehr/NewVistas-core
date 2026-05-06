// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>Assessment type — initial on admission, per-shift, focused, or discharge.</summary>
[GenerateSerializer]
public enum NursingAssessmentType
{
    Initial = 0,
    Shift = 1,
    Focused = 2,
    Discharge = 3,
    PRN = 4
}

/// <summary>Workflow status of a nursing assessment document.</summary>
[GenerateSerializer]
public enum NursingAssessmentStatus
{
    Draft = 0,
    Signed = 1,
    Amended = 2
}

/// <summary>
/// VistA patient classification levels — File #210 NURSING UNIT, ACUITY sub-record.
/// Lower value = less nursing-care intensity; 5 = critical.
/// </summary>
[GenerateSerializer]
public enum AcuityLevel
{
    NotAssigned = 0,
    MinimalCare = 1,
    AverageCare = 2,
    AboveAverageCare = 3,
    IntensiveCare = 4,
    CriticalCare = 5
}

/// <summary>Status of a nursing care plan problem (NANDA-inspired diagnosis).</summary>
[GenerateSerializer]
public enum NursingCarePlanStatus
{
    Active = 0,
    Resolved = 1,
    OnHold = 2,
    Discontinued = 3
}

/// <summary>Progress status of a nursing care plan goal.</summary>
[GenerateSerializer]
public enum NursingGoalStatus
{
    Pending = 0,
    Achieved = 1,
    PartiallyAchieved = 2,
    NotAchieved = 3,
    Revised = 4
}

/// <summary>Outcome evaluation rating recorded at each evaluation interval.</summary>
[GenerateSerializer]
public enum NursingOutcomeRating
{
    GoalMet = 0,
    GoalPartiallyMet = 1,
    GoalNotMet = 2,
    GoalRevised = 3
}
