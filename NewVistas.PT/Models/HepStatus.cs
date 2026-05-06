// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.PT.Models;

/// <summary>
/// Lifecycle status of a home exercise program prescription.
/// </summary>
[GenerateSerializer]
public enum HepStatus
{
    Active,
    Completed,
    Discontinued
}
