// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.PT.Models;

/// <summary>
/// Standard movements measured during physical therapy evaluation.
/// Movements are shared across body groups where anatomically applicable.
/// </summary>
[GenerateSerializer]
public enum Movement
{
    // Shared across multiple body groups
    Flexion,
    Extension,
    Abduction,
    Adduction,
    InternalRotation,
    ExternalRotation,

    // Cervical / Thoracic / Lumbar Spine
    LateralFlexionLeft,
    LateralFlexionRight,
    RotationLeft,
    RotationRight,

    // Shoulder-specific
    HorizontalAbduction,
    HorizontalAdduction,

    // Elbow-specific
    Pronation,
    Supination,

    // Wrist-specific
    RadialDeviation,
    UlnarDeviation,

    // Hand-specific
    Grip,
    LateralPinch,
    TipPinch,
    PalmarPinch,
    FingerFlexion,
    FingerExtension,
    ThumbOpposition,

    // Ankle-specific
    Dorsiflexion,
    PlantarFlexion,
    Inversion,
    Eversion,

    // Foot-specific
    ToeFlexion,
    ToeExtension,
    ToeAbduction,
    ToeAdduction,

    // TMJ-specific
    Opening,
    LateralExcursionLeft,
    LateralExcursionRight,
    Protrusion
}
