// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.PT.Models;

/// <summary>
/// Anatomical body groups used in physical therapy evaluation.
/// Each group has a standard set of movements for ROM and strength testing.
/// </summary>
[GenerateSerializer]
public enum BodyGroup
{
    Cervical,
    Shoulder,
    Elbow,
    Wrist,
    Hand,
    Hip,
    Knee,
    Ankle,
    Foot,
    ThoracicSpine,
    LumbarSpine,
    TMJ
}
