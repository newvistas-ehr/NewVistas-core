// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.PT.Models;

/// <summary>
/// Categories of physical therapy exercises.
/// </summary>
[GenerateSerializer]
public enum ExerciseCategory
{
    Strengthening,
    Stretching,
    Balance,
    Endurance,
    Functional,
    Modality,
    Other
}
