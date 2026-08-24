// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.TestingHost;

namespace NewVistas.FunctionalTests;

/// <summary>
/// Bone health / osteoporosis end-to-end through the workflow grain: the longitudinal record
/// round-trips, the snapshot applies the diagnostic rule that matches the patient's recorded
/// sex, and the feature gate closes the surface when the site turns it off.
/// NonParallelizable — toggles the BONE_HEALTH feature.
/// </summary>
[TestFixture, NonParallelizable]
public class BoneHealthWorkflowTests
{
    private TestCluster _cluster = null!;
    private const string Feature = "BONE_HEALTH";

    [OneTimeSetUp]
    public void OneTimeSetup() => _cluster = SharedCluster.Instance;

    private ISiteParametersGrain SiteParams() =>
        _cluster.GrainFactory.GetGrain<ISiteParametersGrain>("SITE:DEFAULT");

    [SetUp]
    public async Task SetUp() => await SiteParams().EnableFeatureAsync(Feature);

    [TearDown]
    public async Task TearDown() => await SiteParams().EnableFeatureAsync(Feature);

    private IPatientWorkflowGrain Wf(string patientId) =>
        _cluster.GrainFactory.GetGrain<IPatientWorkflowGrain>(patientId);

    /// <summary>Registers a patient so the snapshot has a sex and date of birth to reason from.</summary>
    private async Task<string> NewPatientAsync(string sex, DateTime dateOfBirth)
    {
        string id = $"BONE-{Guid.NewGuid()}";
        await Wf(id).UpdateDemographicsAsync($"BONETEST,{sex}", sex, dateOfBirth, null);
        return id;
    }

    private static DxaScan Scan(DateTime date, string scannerId, decimal spineBmd, decimal spineT) => new()
    {
        ScanDate = date,
        ScannerId = scannerId,
        ScannerModel = "Hologic Horizon A",
        LeastSignificantChangeGramsPerCm2 = 0.030m,
        Measurements =
        {
            new DxaSiteMeasurement
            {
                Site = BoneDensitySite.LumbarSpine,
                BmdGramsPerCm2 = spineBmd,
                TScore = spineT,
                ZScore = spineT + 1.2m,
            },
        },
    };

    [Test]
    public async Task BoneHealth_DxaAndMarkers_RoundTripThroughTheWorkflow()
    {
        string patient = await NewPatientAsync("M", new DateTime(1958, 3, 4));

        await Wf(patient).RecordDxaScanAsync(Scan(new DateTime(2024, 11, 26), "DXA-A", 0.753m, -3.1m));
        await Wf(patient).RecordBoneTurnoverMarkerAsync(new BoneTurnoverMarkerResult
        {
            MarkerType = BoneTurnoverMarkerType.SerumCtx,
            Value = 346m,
            Units = "pg/mL",
            CollectedAt = new DateTime(2025, 4, 16, 8, 15, 0),
            CollectionTimeKnown = true,
            Fasting = true,
        });

        BoneHealthState record = await Wf(patient).GetBoneHealthRecordAsync();

        Assert.Multiple(() =>
        {
            Assert.That(record.IsEnrolled, Is.True, "first write should open the record");
            Assert.That(record.DxaScans, Has.Count.EqualTo(1));
            Assert.That(record.TurnoverMarkers, Has.Count.EqualTo(1));
            Assert.That(record.DxaScans[0].Measurements[0].BmdGramsPerCm2, Is.EqualTo(0.753m));
        });
    }

    [Test]
    public async Task BoneHealth_Snapshot_UsesTScoreForAManOverFifty()
    {
        string patient = await NewPatientAsync("M", new DateTime(1958, 3, 4));   // 66 at scan
        await Wf(patient).RecordDxaScanAsync(Scan(new DateTime(2024, 11, 26), "DXA-A", 0.753m, -3.1m));

        BoneHealthSnapshot snap = await Wf(patient).GetBoneHealthSnapshotAsync();

        Assert.That(snap.OverallCategory, Is.EqualTo(BoneDensityCategory.Osteoporosis));
        Assert.That(snap.LatestDensities[0].ScoreUsed, Is.EqualTo("T-score"));
    }

    [Test]
    public async Task BoneHealth_Snapshot_UsesZScoreForAManUnderFifty()
    {
        // Same T-score as the test above; the difference is entirely the patient's age.
        string patient = await NewPatientAsync("M", new DateTime(1990, 3, 4));   // 34 at scan
        await Wf(patient).RecordDxaScanAsync(Scan(new DateTime(2024, 11, 26), "DXA-A", 0.753m, -3.1m));

        BoneHealthSnapshot snap = await Wf(patient).GetBoneHealthSnapshotAsync();

        Assert.That(snap.LatestDensities[0].ScoreUsed, Is.EqualTo("Z-score"));
        Assert.That(snap.OverallCategory, Is.Not.EqualTo(BoneDensityCategory.Osteoporosis),
            "the WHO T-score categories must not be applied to a man under 50");
    }

    [Test]
    public async Task BoneHealth_SerialScansOnDifferentScanners_AreFlaggedAsNotComparable()
    {
        string patient = await NewPatientAsync("F", new DateTime(1950, 1, 1));

        await Wf(patient).RecordDxaScanAsync(Scan(new DateTime(2022, 1, 10), "DXA-A", 0.700m, -3.2m));
        await Wf(patient).RecordDxaScanAsync(Scan(new DateTime(2024, 1, 10), "DXA-B", 0.780m, -2.7m));

        BoneHealthSnapshot snap = await Wf(patient).GetBoneHealthSnapshotAsync();

        BoneDensityChange change = snap.DensityChanges.Single();
        Assert.That(change.SameScanner, Is.False);
        Assert.That(change.ExceedsLeastSignificantChange, Is.False);
        Assert.That(snap.Caveats, Is.Not.Empty);
    }

    [Test]
    public async Task BoneHealth_TherapyCourse_StartsAndStopsWithATransition()
    {
        string patient = await NewPatientAsync("F", new DateTime(1950, 1, 1));

        string courseId = await Wf(patient).StartOsteoporosisTherapyAsync(new OsteoporosisTherapyCourse
        {
            AgentName = "Denosumab",
            TherapyClass = OsteoporosisTherapyClass.RankLigandInhibitor,
            StartDate = new DateTime(2024, 1, 1),
            DosingIntervalDays = 182,
        });

        BoneHealthState afterStart = await Wf(patient).GetBoneHealthRecordAsync();
        Assert.That(afterStart.Therapies.Single().NextDoseDue, Is.Not.Null,
            "an interval-dosed agent should get a next-dose date derived on start");

        await Wf(patient).StopOsteoporosisTherapyAsync(
            courseId, new DateTime(2026, 1, 1), "Transitioned", "Alendronate");

        OsteoporosisTherapyCourse stopped = (await Wf(patient).GetBoneHealthRecordAsync()).Therapies.Single();
        Assert.Multiple(() =>
        {
            Assert.That(stopped.StopDate, Is.EqualTo(new DateTime(2026, 1, 1)));
            Assert.That(stopped.TransitionedToAgent, Is.EqualTo("Alendronate"));
            Assert.That(stopped.NextDoseDue, Is.Null);
        });
    }

    [Test]
    public async Task BoneHealth_SnapshotShowsOnlyActiveTherapies()
    {
        string patient = await NewPatientAsync("F", new DateTime(1950, 1, 1));

        string first = await Wf(patient).StartOsteoporosisTherapyAsync(new OsteoporosisTherapyCourse
        {
            AgentName = "Teriparatide",
            TherapyClass = OsteoporosisTherapyClass.AnabolicPthAnalogue,
            StartDate = new DateTime(2022, 1, 1),
        });
        await Wf(patient).StopOsteoporosisTherapyAsync(first, new DateTime(2024, 1, 1), "Course complete", "Alendronate");

        await Wf(patient).StartOsteoporosisTherapyAsync(new OsteoporosisTherapyCourse
        {
            AgentName = "Alendronate",
            TherapyClass = OsteoporosisTherapyClass.Bisphosphonate,
            StartDate = new DateTime(2024, 1, 1),
        });

        BoneHealthSnapshot snap = await Wf(patient).GetBoneHealthSnapshotAsync();

        Assert.That(snap.ActiveTherapies, Has.Count.EqualTo(1));
        Assert.That(snap.ActiveTherapies[0].AgentName, Is.EqualTo("Alendronate"));
    }

    // ── Enrollment ──────────────────────────────────────────────────────────

    [Test]
    public async Task BoneHealth_ExplicitEnroll_OpensRecordAndIndexesThePatient()
    {
        string patient = await NewPatientAsync("F", new DateTime(1952, 6, 1));

        await Wf(patient).EnrollInBoneHealthAsync("Osteoporosis", new DateTime(2024, 11, 26));

        BoneHealthState record = await Wf(patient).GetBoneHealthRecordAsync();
        Assert.Multiple(() =>
        {
            Assert.That(record.IsEnrolled, Is.True);
            Assert.That(record.EnrollmentDate, Is.EqualTo(new DateTime(2024, 11, 26)));
            Assert.That(record.PrimaryDiagnosis, Is.EqualTo("Osteoporosis"));
        });

        // The site-wide index is a shared singleton across the run — membership only.
        List<string> enrolled = await _cluster.GrainFactory
            .GetGrain<IBoneHealthIndexGrain>("BONE-HEALTH-IDX").GetEnrolledAsync();
        Assert.That(enrolled, Contains.Item(patient));
    }

    [Test]
    public async Task BoneHealth_EnrollTwice_KeepsTheOriginalEnrollmentDate()
    {
        string patient = await NewPatientAsync("F", new DateTime(1952, 6, 1));

        await Wf(patient).EnrollInBoneHealthAsync("Osteopenia", new DateTime(2020, 1, 15));
        await Wf(patient).EnrollInBoneHealthAsync("Osteoporosis", new DateTime(2024, 6, 1));

        BoneHealthState record = await Wf(patient).GetBoneHealthRecordAsync();
        Assert.Multiple(() =>
        {
            Assert.That(record.IsEnrolled, Is.True, "re-enrolling is idempotent, not an error");
            Assert.That(record.EnrollmentDate, Is.EqualTo(new DateTime(2020, 1, 15)),
                "the record keeps the date it was first opened");
            Assert.That(record.PrimaryDiagnosis, Is.EqualTo("Osteoporosis"),
                "the working diagnosis may be refined on re-enroll");
        });

        // The site-wide index must agree with the record: the workflow mirrors the date the
        // bone grain KEPT (the original), not the re-enroll argument. IBoneHealthIndexGrain
        // only exposes membership/count — the stored date is not readable through the grain
        // interface, so the no-drift behavior is pinned by the workflow code, and membership
        // is asserted here.
        List<string> enrolled = await _cluster.GrainFactory
            .GetGrain<IBoneHealthIndexGrain>("BONE-HEALTH-IDX").GetEnrolledAsync();
        Assert.That(enrolled, Contains.Item(patient));
    }

    // ── Fracture / FRAX / secondary workup ──────────────────────────────────

    [Test]
    public async Task BoneHealth_FractureFraxAndWorkup_ReachTheRecordAndTheSnapshot()
    {
        string patient = await NewPatientAsync("F", new DateTime(1948, 2, 10));

        string fractureId = await Wf(patient).RecordBoneFractureAsync(new BoneFracture
        {
            Site = "L1 vertebral body",
            FractureDate = new DateTime(2023, 5, 4),
            Mechanism = FractureMechanism.Fragility,
            ImagingConfirmed = true,
            VertebralGrade = 2,
            IsHipOrVertebral = true,
        });
        string fraxId = await Wf(patient).RecordFraxAssessmentAsync(new FraxAssessment
        {
            AssessmentDate = new DateTime(2023, 6, 1),
            MajorOsteoporoticFracturePercent = 24.0m,
            HipFracturePercent = 6.1m,
            IncludedFemoralNeckBmd = false,
            CountryCalibration = "US",
            RiskFactorsUsed = { "Prior fragility fracture", "Current smoker" },
        });
        string workupId = await Wf(patient).RecordBoneSecondaryWorkupAsync(new SecondaryCauseWorkup
        {
            WorkupDate = new DateTime(2023, 6, 8),
            Results = { ["25-OH vitamin D"] = "14 ng/mL" },
            IdentifiedCauses = { "Vitamin D deficiency" },
            OrderedByName = "Dr. Bone",
        });

        BoneHealthState record = await Wf(patient).GetBoneHealthRecordAsync();
        Assert.Multiple(() =>
        {
            Assert.That(record.IsEnrolled, Is.True, "first write should open the record");
            Assert.That(record.EnrollmentDate, Is.EqualTo(new DateTime(2023, 5, 4)),
                "auto-enrollment is dated from the first observation, not from 'now'");
            Assert.That(record.Fractures.Single().FractureId, Is.EqualTo(fractureId));
            Assert.That(record.FraxAssessments.Single().AssessmentId, Is.EqualTo(fraxId));
            Assert.That(record.SecondaryWorkups.Single().WorkupId, Is.EqualTo(workupId));
        });

        BoneHealthSnapshot snap = await Wf(patient).GetBoneHealthSnapshotAsync();
        Assert.Multiple(() =>
        {
            Assert.That(snap.FragilityFractureCount, Is.EqualTo(1));
            Assert.That(snap.OverallCategory, Is.EqualTo(BoneDensityCategory.ClinicalOsteoporosis),
                "a vertebral fragility fracture is diagnostic with no DXA on file");
            Assert.That(snap.LatestFrax?.AssessmentId, Is.EqualTo(fraxId));
            Assert.That(snap.IdentifiedSecondaryCauses, Contains.Item("Vitamin D deficiency"));
        });
    }

    [Test]
    public async Task BoneHealth_MinimalFracture_SiteAloneIsEnough()
    {
        string patient = await NewPatientAsync("M", new DateTime(1970, 1, 1));

        string id = await Wf(patient).RecordBoneFractureAsync(new BoneFracture { Site = "Distal radius" });

        BoneFracture stored = (await Wf(patient).GetBoneHealthRecordAsync()).Fractures.Single();
        Assert.Multiple(() =>
        {
            Assert.That(stored.FractureId, Is.EqualTo(id));
            Assert.That(stored.Site, Is.EqualTo("Distal radius"));
            Assert.That(stored.Mechanism, Is.EqualTo(FractureMechanism.Unknown));
        });

        BoneHealthSnapshot snap = await Wf(patient).GetBoneHealthSnapshotAsync();
        Assert.That(snap.FragilityFractureCount, Is.Zero,
            "an Unknown-mechanism fracture must not count as a fragility fracture");
    }

    [Test]
    public async Task BoneHealth_FeatureOff_RejectsEnrollFractureFraxAndWorkup()
    {
        string patient = await NewPatientAsync("F", new DateTime(1955, 9, 9));

        await SiteParams().DisableFeatureAsync(Feature);
        try
        {
            // The gate throws before anything reaches the bone-health grain.
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await Wf(patient).EnrollInBoneHealthAsync("Osteoporosis", DateTime.UtcNow));
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await Wf(patient).RecordBoneFractureAsync(new BoneFracture { Site = "Femoral neck" }));
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await Wf(patient).RecordFraxAssessmentAsync(new FraxAssessment { AssessmentDate = DateTime.UtcNow }));
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await Wf(patient).RecordBoneSecondaryWorkupAsync(new SecondaryCauseWorkup { WorkupDate = DateTime.UtcNow }));
        }
        finally
        {
            await SiteParams().EnableFeatureAsync(Feature);
        }

        BoneHealthState record = await Wf(patient).GetBoneHealthRecordAsync();
        Assert.That(record.IsEnrolled, Is.False, "the gated writes must not have touched the record");
    }

    [Test]
    public async Task BoneHealth_FeatureOff_ClosesTheSurface()
    {
        string patient = await NewPatientAsync("M", new DateTime(1958, 3, 4));
        await Wf(patient).RecordDxaScanAsync(Scan(new DateTime(2024, 11, 26), "DXA-A", 0.753m, -3.1m));

        await SiteParams().DisableFeatureAsync(Feature);
        try
        {
            BoneHealthSnapshot snap = await Wf(patient).GetBoneHealthSnapshotAsync();
            Assert.That(snap.LatestDensities, Is.Empty, "reads should go quiet when the feature is off");

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await Wf(patient).RecordBoneTurnoverMarkerAsync(new BoneTurnoverMarkerResult
                {
                    MarkerType = BoneTurnoverMarkerType.SerumCtx,
                    Value = 300m,
                    Units = "pg/mL",
                    CollectedAt = DateTime.UtcNow,
                }));
        }
        finally
        {
            await SiteParams().EnableFeatureAsync(Feature);
        }

        // Re-enabling restores the previously recorded data — the gate hides, it does not delete.
        BoneHealthSnapshot restored = await Wf(patient).GetBoneHealthSnapshotAsync();
        Assert.That(restored.LatestDensities, Is.Not.Empty);
    }
}
