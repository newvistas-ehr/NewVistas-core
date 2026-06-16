// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
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
