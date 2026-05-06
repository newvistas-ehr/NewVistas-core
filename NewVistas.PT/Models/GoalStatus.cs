// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.PT.Models;

/// <summary>
/// Lifecycle status of a physical therapy goal.
/// </summary>
[GenerateSerializer]
public enum GoalStatus
{
    Active,
    Achieved,
    Discontinued,
    OnHold
}
