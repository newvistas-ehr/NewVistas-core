// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.UnitTests;

/// <summary>
/// Unit tests for the deterministic newborn classifier (NEONATAL_CARE) — gestational-age band,
/// birth-weight category, and weight-for-gestational-age (SGA/AGA/LGA).
/// Pure logic — no Orleans cluster needed.
/// </summary>
[TestFixture]
public class NeonatalClassifierTests
{
    // ── ClassifyGestationalAge ───────────────────────────────────────────────────

    [Test]
    public void ClassifyGestationalAge_27Weeks_ExtremelyPreterm()
        => Assert.That(NeonatalClassifier.ClassifyGestationalAge(27),
            Is.EqualTo(GestationalAgeClassification.ExtremelyPreterm));

    [Test]
    public void ClassifyGestationalAge_30Weeks_VeryPreterm()
        => Assert.That(NeonatalClassifier.ClassifyGestationalAge(30),
            Is.EqualTo(GestationalAgeClassification.VeryPreterm));

    [Test]
    public void ClassifyGestationalAge_33Weeks_Preterm()
        => Assert.That(NeonatalClassifier.ClassifyGestationalAge(33),
            Is.EqualTo(GestationalAgeClassification.Preterm));

    [Test]
    public void ClassifyGestationalAge_35Weeks_LatePreterm()
        => Assert.That(NeonatalClassifier.ClassifyGestationalAge(35),
            Is.EqualTo(GestationalAgeClassification.LatePreterm));

    [Test]
    public void ClassifyGestationalAge_39Weeks_Term()
        => Assert.That(NeonatalClassifier.ClassifyGestationalAge(39),
            Is.EqualTo(GestationalAgeClassification.Term));

    [Test]
    public void ClassifyGestationalAge_42Weeks_PostTerm()
        => Assert.That(NeonatalClassifier.ClassifyGestationalAge(42),
            Is.EqualTo(GestationalAgeClassification.PostTerm));

    [Test]
    public void ClassifyGestationalAge_Zero_Unknown()
        => Assert.That(NeonatalClassifier.ClassifyGestationalAge(0),
            Is.EqualTo(GestationalAgeClassification.Unknown));

    // ── ClassifyBirthWeight ──────────────────────────────────────────────────────

    [Test]
    public void ClassifyBirthWeight_900g_ExtremelyLow()
        => Assert.That(NeonatalClassifier.ClassifyBirthWeight(900),
            Is.EqualTo(BirthWeightCategory.ExtremelyLowBirthWeight));

    [Test]
    public void ClassifyBirthWeight_1400g_VeryLow()
        => Assert.That(NeonatalClassifier.ClassifyBirthWeight(1400),
            Is.EqualTo(BirthWeightCategory.VeryLowBirthWeight));

    [Test]
    public void ClassifyBirthWeight_2400g_Low()
        => Assert.That(NeonatalClassifier.ClassifyBirthWeight(2400),
            Is.EqualTo(BirthWeightCategory.LowBirthWeight));

    [Test]
    public void ClassifyBirthWeight_3300g_Normal()
        => Assert.That(NeonatalClassifier.ClassifyBirthWeight(3300),
            Is.EqualTo(BirthWeightCategory.Normal));

    [Test]
    public void ClassifyBirthWeight_4200g_Macrosomia()
        => Assert.That(NeonatalClassifier.ClassifyBirthWeight(4200),
            Is.EqualTo(BirthWeightCategory.Macrosomia));

    [Test]
    public void ClassifyBirthWeight_Null_Unknown()
        => Assert.That(NeonatalClassifier.ClassifyBirthWeight(null),
            Is.EqualTo(BirthWeightCategory.Unknown));

    // ── ClassifySizeForGestationalAge (at 39 weeks) ──────────────────────────────

    [Test]
    public void ClassifySizeForGestationalAge_At39w_2000g_SmallForGestationalAge()
        => Assert.That(NeonatalClassifier.ClassifySizeForGestationalAge(39, 2000),
            Is.EqualTo(SizeForGestationalAge.SmallForGestationalAge));

    [Test]
    public void ClassifySizeForGestationalAge_At39w_3300g_AppropriateForGestationalAge()
        => Assert.That(NeonatalClassifier.ClassifySizeForGestationalAge(39, 3300),
            Is.EqualTo(SizeForGestationalAge.AppropriateForGestationalAge));

    [Test]
    public void ClassifySizeForGestationalAge_At39w_4100g_LargeForGestationalAge()
        => Assert.That(NeonatalClassifier.ClassifySizeForGestationalAge(39, 4100),
            Is.EqualTo(SizeForGestationalAge.LargeForGestationalAge));

    [Test]
    public void ClassifySizeForGestationalAge_NullGrams_Unknown()
        => Assert.That(NeonatalClassifier.ClassifySizeForGestationalAge(39, null),
            Is.EqualTo(SizeForGestationalAge.Unknown));
}
