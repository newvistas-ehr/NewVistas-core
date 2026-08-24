// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.UnitTests;

/// <summary>
/// Rules tests for <see cref="BoneDensityClassifier"/>. These cover the three places where
/// getting it wrong produces a confident wrong answer rather than a visibly missing one:
/// which score applies to whom, whether two scans may be compared, and whether a turnover
/// marker was collected under conditions that make it comparable.
/// </summary>
[TestFixture]
public class BoneDensityClassifierTests
{
    private static DxaSiteMeasurement Site(
        BoneDensitySite site = BoneDensitySite.LumbarSpine,
        decimal bmd = 0.750m,
        decimal? t = null,
        decimal? z = null) =>
        new() { Site = site, BmdGramsPerCm2 = bmd, TScore = t, ZScore = z };

    // ── T-score vs Z-score by sex and age ───────────────────────────────────

    [Test]
    public void Classifier_PostmenopausalWoman_UsesTScore_Osteoporosis()
    {
        ClassifiedBoneDensity result = BoneDensityClassifier.ClassifySite(
            Site(t: -2.6m, z: -1.0m), new DateTime(2026, 1, 1), "F", 68, isPostmenopausal: true);

        Assert.That(result.Category, Is.EqualTo(BoneDensityCategory.Osteoporosis));
        Assert.That(result.ScoreUsed, Is.EqualTo("T-score"));
    }

    [Test]
    public void Classifier_ManOver50_UsesTScore()
    {
        ClassifiedBoneDensity result = BoneDensityClassifier.ClassifySite(
            Site(t: -3.1m), new DateTime(2026, 1, 1), "M", 61, isPostmenopausal: null);

        Assert.That(result.Category, Is.EqualTo(BoneDensityCategory.Osteoporosis));
        Assert.That(result.ScoreUsed, Is.EqualTo("T-score"));
    }

    [Test]
    public void Classifier_ManUnder50_UsesZScore_NotTScore()
    {
        // A T-score of -2.6 would read as "osteoporosis" if the WHO criteria were misapplied.
        // For a man under 50 the Z-score governs, and -1.4 is within the expected range.
        ClassifiedBoneDensity result = BoneDensityClassifier.ClassifySite(
            Site(t: -2.6m, z: -1.4m), new DateTime(2026, 1, 1), "M", 41, isPostmenopausal: null);

        Assert.That(result.ScoreUsed, Is.EqualTo("Z-score"));
        Assert.That(result.Category, Is.EqualTo(BoneDensityCategory.WithinExpectedRangeForAge));
        Assert.That(result.Category, Is.Not.EqualTo(BoneDensityCategory.Osteoporosis));
        Assert.That(result.Rationale, Does.Contain("do NOT apply"));
    }

    [Test]
    public void Classifier_PremenopausalWoman_UsesZScore_BelowExpectedRange()
    {
        ClassifiedBoneDensity result = BoneDensityClassifier.ClassifySite(
            Site(t: -2.7m, z: -2.3m), new DateTime(2026, 1, 1), "F", 34, isPostmenopausal: false);

        Assert.That(result.ScoreUsed, Is.EqualTo("Z-score"));
        Assert.That(result.Category, Is.EqualTo(BoneDensityCategory.BelowExpectedRangeForAge));
    }

    [Test]
    public void Classifier_PremenopausalWoman_WithoutZScore_ReportsNoDataNotOsteoporosis()
    {
        ClassifiedBoneDensity result = BoneDensityClassifier.ClassifySite(
            Site(t: -2.9m, z: null), new DateTime(2026, 1, 1), "F", 30, isPostmenopausal: false);

        Assert.That(result.Category, Is.EqualTo(BoneDensityCategory.NoData));
        Assert.That(result.Rationale, Does.Contain("Z-score is required"));
    }

    [Test]
    public void Classifier_WomanWithUnknownMenopausalStatus_FallsBackToAgeAndSaysSo()
    {
        ClassifiedBoneDensity result = BoneDensityClassifier.ClassifySite(
            Site(t: -2.8m), new DateTime(2026, 1, 1), "F", 62, isPostmenopausal: null);

        Assert.That(result.Category, Is.EqualTo(BoneDensityCategory.Osteoporosis));
        Assert.That(result.Rationale, Does.Contain("assumed from age"));
    }

    [Test]
    public void Classifier_TScoreBoundaries_MapToExpectedCategories()
    {
        var d = new DateTime(2026, 1, 1);
        Assert.Multiple(() =>
        {
            Assert.That(BoneDensityClassifier.ClassifySite(Site(t: -0.5m), d, "M", 70, null).Category,
                Is.EqualTo(BoneDensityCategory.Normal));
            Assert.That(BoneDensityClassifier.ClassifySite(Site(t: -1.0m), d, "M", 70, null).Category,
                Is.EqualTo(BoneDensityCategory.Normal));
            Assert.That(BoneDensityClassifier.ClassifySite(Site(t: -1.8m), d, "M", 70, null).Category,
                Is.EqualTo(BoneDensityCategory.LowBoneMass));
            Assert.That(BoneDensityClassifier.ClassifySite(Site(t: -2.5m), d, "M", 70, null).Category,
                Is.EqualTo(BoneDensityCategory.Osteoporosis));
        });
    }

    // ── Scanner comparability ───────────────────────────────────────────────

    private static DxaScan Scan(DateTime date, string? scannerId, decimal bmd, decimal? lsc = 0.030m) =>
        new()
        {
            ScanDate = date,
            ScannerId = scannerId,
            LeastSignificantChangeGramsPerCm2 = lsc,
            Measurements = { Site(bmd: bmd, t: -2.6m) },
        };

    [Test]
    public void Compare_DifferentScanners_IsNotAValidComparison()
    {
        BoneDensityChange change = BoneDensityClassifier.CompareSite(
            Scan(new DateTime(2024, 1, 1), "DXA-A", 0.700m),
            Scan(new DateTime(2026, 1, 1), "DXA-B", 0.800m),
            BoneDensitySite.LumbarSpine);

        Assert.That(change.SameScanner, Is.False);
        Assert.That(change.ExceedsLeastSignificantChange, Is.False);
        Assert.That(change.Caveat, Does.Contain("different scanners"));
    }

    [Test]
    public void Compare_SameScanner_ChangeBelowLsc_IsNotARealChange()
    {
        // 0.010 g/cm² against an LSC of 0.030 — within precision error.
        BoneDensityChange change = BoneDensityClassifier.CompareSite(
            Scan(new DateTime(2024, 1, 1), "DXA-A", 0.750m),
            Scan(new DateTime(2026, 1, 1), "DXA-A", 0.760m),
            BoneDensitySite.LumbarSpine);

        Assert.That(change.SameScanner, Is.True);
        Assert.That(change.ExceedsLeastSignificantChange, Is.False);
        Assert.That(change.Caveat, Does.Contain("least significant change"));
    }

    [Test]
    public void Compare_SameScanner_ChangeAboveLsc_IsReal()
    {
        BoneDensityChange change = BoneDensityClassifier.CompareSite(
            Scan(new DateTime(2024, 1, 1), "DXA-A", 0.700m),
            Scan(new DateTime(2026, 1, 1), "DXA-A", 0.760m),
            BoneDensitySite.LumbarSpine);

        Assert.That(change.ExceedsLeastSignificantChange, Is.True);
        Assert.That(change.Caveat, Is.Null);
        Assert.That(change.ChangeGramsPerCm2, Is.EqualTo(0.060m));
    }

    [Test]
    public void Compare_MissingLsc_CannotAssertRealChange()
    {
        BoneDensityChange change = BoneDensityClassifier.CompareSite(
            Scan(new DateTime(2024, 1, 1), "DXA-A", 0.700m, lsc: null),
            Scan(new DateTime(2026, 1, 1), "DXA-A", 0.800m, lsc: null),
            BoneDensitySite.LumbarSpine);

        Assert.That(change.ExceedsLeastSignificantChange, Is.False);
        Assert.That(change.Caveat, Does.Contain("least significant change"));
    }

    // ── Turnover marker collection conditions ───────────────────────────────

    private static BoneTurnoverMarkerResult Ctx(
        DateTime collectedAt, bool? fasting, bool timeKnown, decimal value = 300m) =>
        new()
        {
            MarkerType = BoneTurnoverMarkerType.SerumCtx,
            Value = value,
            Units = "pg/mL",
            CollectedAt = collectedAt,
            Fasting = fasting,
            CollectionTimeKnown = timeKnown,
        };

    [Test]
    public void Marker_FastingMorningDraw_IsInterpretable()
    {
        var (interp, caveat) = BoneDensityClassifier.ClassifyTurnoverMarker(
            Ctx(new DateTime(2026, 3, 1, 8, 0, 0), fasting: true, timeKnown: true));

        Assert.That(interp, Is.EqualTo(BoneTurnoverInterpretability.Interpretable));
        Assert.That(caveat, Is.Null);
    }

    [Test]
    public void Marker_NonFasting_IsFlagged()
    {
        var (interp, caveat) = BoneDensityClassifier.ClassifyTurnoverMarker(
            Ctx(new DateTime(2026, 3, 1, 8, 0, 0), fasting: false, timeKnown: true));

        Assert.That(interp, Is.EqualTo(BoneTurnoverInterpretability.NotFasting));
        Assert.That(caveat, Does.Contain("suppresses"));
    }

    [Test]
    public void Marker_AfternoonDraw_IsFlagged()
    {
        var (interp, caveat) = BoneDensityClassifier.ClassifyTurnoverMarker(
            Ctx(new DateTime(2026, 3, 1, 15, 30, 0), fasting: true, timeKnown: true));

        Assert.That(interp, Is.EqualTo(BoneTurnoverInterpretability.OutsideMorningWindow));
        Assert.That(caveat, Does.Contain("falls through the day"));
    }

    [Test]
    public void Marker_NoCollectionConditions_IsFlaggedAsUnknown()
    {
        var (interp, _) = BoneDensityClassifier.ClassifyTurnoverMarker(
            Ctx(new DateTime(2026, 3, 1), fasting: null, timeKnown: false));

        Assert.That(interp, Is.EqualTo(BoneTurnoverInterpretability.CollectionConditionsUnknown));
    }

    // ── Snapshot integration ────────────────────────────────────────────────

    [Test]
    public void Snapshot_TrendsOnlyInterpretableMarkers()
    {
        var state = new BoneHealthState { Icn = "P1", IsEnrolled = true };
        state.TurnoverMarkers.Add(Ctx(new DateTime(2025, 4, 16, 8, 15, 0), true, true, 346m));
        state.TurnoverMarkers.Add(Ctx(new DateTime(2025, 6, 1), null, false, 402m));       // not comparable
        state.TurnoverMarkers.Add(Ctx(new DateTime(2025, 12, 3, 8, 5, 0), true, true, 270m));

        BoneHealthSnapshot snap = BoneDensityClassifier.BuildSnapshot(
            state, "M", new DateTime(1958, 1, 1), null, new DateTime(2026, 1, 1));

        ClassifiedTurnoverMarker flagged = snap.TurnoverMarkers.Single(m => m.Result.Value == 402m);
        Assert.That(flagged.Interpretability, Is.Not.EqualTo(BoneTurnoverInterpretability.Interpretable));
        Assert.That(flagged.PercentChangeFromPrevious, Is.Null, "a non-comparable draw must not produce a trend");

        // 346 → 270 is -22.0%, computed against the previous INTERPRETABLE value, skipping 402.
        ClassifiedTurnoverMarker third = snap.TurnoverMarkers.Single(m => m.Result.Value == 270m);
        Assert.That(third.PercentChangeFromPrevious, Is.EqualTo(-22.0m));
        Assert.That(snap.Caveats, Is.Not.Empty);
    }

    [Test]
    public void Snapshot_HipOrVertebralFragilityFracture_IsDiagnosticRegardlessOfBmd()
    {
        var state = new BoneHealthState { Icn = "P1", IsEnrolled = true };
        state.DxaScans.Add(new DxaScan
        {
            ScanDate = new DateTime(2026, 1, 1),
            Measurements = { Site(BoneDensitySite.FemoralNeck, 0.900m, t: -1.2m) },   // only low bone mass
        });
        state.Fractures.Add(new BoneFracture
        {
            Site = "L1 vertebral body",
            FractureDate = new DateTime(2025, 6, 1),
            Mechanism = FractureMechanism.Fragility,
            IsHipOrVertebral = true,
        });

        BoneHealthSnapshot snap = BoneDensityClassifier.BuildSnapshot(
            state, "M", new DateTime(1955, 1, 1), null, new DateTime(2026, 2, 1));

        Assert.That(snap.OverallCategory, Is.EqualTo(BoneDensityCategory.ClinicalOsteoporosis));
        Assert.That(snap.OverallRationale, Does.Contain("irrespective of bone mineral density"));
    }

    [Test]
    public void Snapshot_OsteoporosisPlusFragilityFracture_EscalatesToSevere()
    {
        var state = new BoneHealthState { Icn = "P1", IsEnrolled = true };
        state.DxaScans.Add(new DxaScan
        {
            ScanDate = new DateTime(2026, 1, 1),
            Measurements = { Site(BoneDensitySite.LumbarSpine, 0.700m, t: -3.1m) },
        });
        state.Fractures.Add(new BoneFracture
        {
            Site = "Distal radius",
            FractureDate = new DateTime(2025, 6, 1),
            Mechanism = FractureMechanism.Fragility,
            IsHipOrVertebral = false,
        });

        BoneHealthSnapshot snap = BoneDensityClassifier.BuildSnapshot(
            state, "M", new DateTime(1955, 1, 1), null, new DateTime(2026, 2, 1));

        Assert.That(snap.OverallCategory, Is.EqualTo(BoneDensityCategory.SevereOsteoporosis));
    }

    [Test]
    public void Snapshot_DiagnosisTakesTheWorstDiagnosticSite()
    {
        var state = new BoneHealthState { Icn = "P1", IsEnrolled = true };
        state.DxaScans.Add(new DxaScan
        {
            ScanDate = new DateTime(2026, 1, 1),
            Measurements =
            {
                Site(BoneDensitySite.LumbarSpine, 0.950m, t: -0.8m),   // normal
                Site(BoneDensitySite.FemoralNeck, 0.521m, t: -3.0m),   // osteoporosis
            },
        });

        BoneHealthSnapshot snap = BoneDensityClassifier.BuildSnapshot(
            state, "M", new DateTime(1955, 1, 1), null, new DateTime(2026, 2, 1));

        Assert.That(snap.OverallCategory, Is.EqualTo(BoneDensityCategory.Osteoporosis));
        Assert.That(snap.OverallRationale, Does.Contain("Femoral neck"));
    }

    [Test]
    public void Snapshot_AttributesMarkersToTheTherapyInForceWhenDrawn()
    {
        var state = new BoneHealthState { Icn = "P1", IsEnrolled = true };
        state.Therapies.Add(new OsteoporosisTherapyCourse
        {
            AgentName = "Teriparatide",
            TherapyClass = OsteoporosisTherapyClass.AnabolicPthAnalogue,
            StartDate = new DateTime(2024, 12, 10),
        });
        state.TurnoverMarkers.Add(Ctx(new DateTime(2024, 6, 1, 8, 0, 0), true, true, 300m));   // before therapy
        state.TurnoverMarkers.Add(Ctx(new DateTime(2025, 4, 16, 8, 15, 0), true, true, 346m)); // on therapy

        BoneHealthSnapshot snap = BoneDensityClassifier.BuildSnapshot(
            state, "M", new DateTime(1958, 1, 1), null, new DateTime(2026, 1, 1));

        Assert.That(snap.TurnoverMarkers[0].TherapyInForce, Is.Null);
        Assert.That(snap.TurnoverMarkers[1].TherapyInForce, Is.EqualTo("Teriparatide"));
    }

    [Test]
    public void Snapshot_NoData_IsReportedAsSuch()
    {
        BoneHealthSnapshot snap = BoneDensityClassifier.BuildSnapshot(
            new BoneHealthState { Icn = "P1" }, "M", new DateTime(1955, 1, 1), null, DateTime.UtcNow);

        Assert.That(snap.OverallCategory, Is.EqualTo(BoneDensityCategory.NoData));
        Assert.That(snap.LatestDensities, Is.Empty);
    }
}
