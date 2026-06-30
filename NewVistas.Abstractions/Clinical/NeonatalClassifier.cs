// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Clinical;

/// <summary>
/// Deterministic newborn classification — gestational-age band, birth-weight category, and
/// weight-for-gestational-age (SGA/AGA/LGA). Curated/representative (like
/// <see cref="PrecisionOncology"/> and <see cref="HomeHealthGrouper"/>): the GA bands and weight
/// thresholds are the standard AAP/WHO cutoffs, and the size-for-GA percentile table is a
/// representative singleton band set — NOT the full Olsen/Fenton growth curves.
/// </summary>
public static class NeonatalClassifier
{
    /// <summary>Gestational-age classification from completed weeks.</summary>
    public static GestationalAgeClassification ClassifyGestationalAge(int weeks)
    {
        if (weeks <= 0) return GestationalAgeClassification.Unknown;
        if (weeks < 28) return GestationalAgeClassification.ExtremelyPreterm;
        if (weeks < 32) return GestationalAgeClassification.VeryPreterm;
        if (weeks < 34) return GestationalAgeClassification.Preterm;
        if (weeks < 37) return GestationalAgeClassification.LatePreterm;
        if (weeks < 42) return GestationalAgeClassification.Term;
        return GestationalAgeClassification.PostTerm;
    }

    /// <summary>Birth-weight magnitude category.</summary>
    public static BirthWeightCategory ClassifyBirthWeight(int? grams)
    {
        if (grams is null or <= 0) return BirthWeightCategory.Unknown;
        if (grams < 1000) return BirthWeightCategory.ExtremelyLowBirthWeight;
        if (grams < 1500) return BirthWeightCategory.VeryLowBirthWeight;
        if (grams < 2500) return BirthWeightCategory.LowBirthWeight;
        if (grams < 4000) return BirthWeightCategory.Normal;
        return BirthWeightCategory.Macrosomia;
    }

    // Representative 10th / 90th percentile birth weights (grams) by completed week, singleton.
    private static readonly (int Ga, int P10, int P90)[] Bands =
    {
        (24, 500, 750), (25, 560, 840), (26, 650, 960), (27, 760, 1100), (28, 900, 1300),
        (29, 1050, 1500), (30, 1200, 1700), (31, 1350, 1950), (32, 1500, 2200), (33, 1700, 2500),
        (34, 1900, 2800), (35, 2100, 3000), (36, 2300, 3200), (37, 2500, 3500), (38, 2700, 3700),
        (39, 2850, 3900), (40, 2950, 4050), (41, 3050, 4150), (42, 3100, 4250)
    };

    /// <summary>Weight-for-gestational-age (SGA &lt;10th pct / AGA / LGA &gt;90th pct).</summary>
    public static SizeForGestationalAge ClassifySizeForGestationalAge(int gaWeeks, int? grams)
    {
        if (grams is null or <= 0 || gaWeeks < 24) return SizeForGestationalAge.Unknown;
        int ga = Math.Clamp(gaWeeks, 24, 42);
        (int _, int p10, int p90) = Bands.First(b => b.Ga == ga);
        if (grams < p10) return SizeForGestationalAge.SmallForGestationalAge;
        if (grams > p90) return SizeForGestationalAge.LargeForGestationalAge;
        return SizeForGestationalAge.AppropriateForGestationalAge;
    }
}
