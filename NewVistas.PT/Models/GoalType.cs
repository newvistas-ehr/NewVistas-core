// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.PT.Models;

/// <summary>
/// Types of physical therapy goals that can be set for a patient.
/// </summary>
[GenerateSerializer]
public enum GoalType
{
    ROM,
    Strength,
    PainReduction,
    Functional,
    Balance,
    Endurance
}
