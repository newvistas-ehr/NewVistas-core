// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Helpers;

/// <summary>
/// Turns raw bone-health observations into the derived answers a clinician reads.
///
/// Centralised for the same reason <c>DiabetesRegistryRules</c> is: the snapshot, the
/// UI, and any future reporting must all apply identical rules. Three of those rules
/// are correctness issues rather than presentation choices:
///
/// <list type="number">
/// <item>
/// <b>T-score vs Z-score.</b> The WHO T-score thresholds are validated only for
/// postmenopausal women and men aged 50 and over. For premenopausal women and men under
/// 50, diagnosis uses the Z-score and is expressed as within or below the expected range
/// for age. Reporting "osteoporosis" from a T-score in a 35-year-old is simply wrong.
/// </item>
/// <item>
/// <b>Scanner comparability.</b> Absolute BMD is only comparable within one scanner, and
/// a difference smaller than that scanner's least significant change is measurement noise,
/// not bone loss.
/// </item>
/// <item>
/// <b>Turnover marker collection conditions.</b> CTX has marked circadian variation and is
/// suppressed by food, so a non-fasting or afternoon draw cannot be compared with a fasting
/// morning one. Such results are flagged rather than trended.
/// </item>
/// </list>
/// </summary>
public static class BoneDensityClassifier
{
    /// <summary>Latest hour (exclusive) that counts as a morning draw for turnover markers.</summary>
    private const int MorningDrawCutoffHour = 10;

    /// <summary>Sites used for diagnosis. The 33% radius is included only when spine and hip are unusable.</summary>
    private static readonly BoneDensitySite[] DiagnosticSites =
    {
        BoneDensitySite.LumbarSpine,
        BoneDensitySite.FemoralNeck,
        BoneDensitySite.TotalHip,
    };

    /// <summary>
    /// Classify a single site measurement for a patient of the given sex and age.
    /// </summary>
    /// <param name="measurement">The site measurement to classify.</param>
    /// <param name="scanDate">Date of the study the measurement came from.</param>
    /// <param name="sex">Patient sex — "M" or "F" (VistA File #2 field .02 convention).</param>
    /// <param name="ageYears">Patient age at the time of the scan.</param>
    /// <param name="isPostmenopausal">
    /// Menopausal status for female patients. Null means unknown, in which case age 50 is
    /// used as a conservative proxy so the classification is stated with that caveat.
    /// </param>
    public static ClassifiedBoneDensity ClassifySite(
        DxaSiteMeasurement measurement,
        DateTime scanDate,
        string? sex,
        int? ageYears,
        bool? isPostmenopausal)
    {
        var result = new ClassifiedBoneDensity
        {
            Site = measurement.Site,
            ScanDate = scanDate,
            BmdGramsPerCm2 = measurement.BmdGramsPerCm2,
            TScore = measurement.TScore,
            ZScore = measurement.ZScore,
        };

        bool isMale = string.Equals(sex, "M", StringComparison.OrdinalIgnoreCase);
        bool isFemale = string.Equals(sex, "F", StringComparison.OrdinalIgnoreCase);

        // Whether the WHO T-score criteria apply at all.
        bool menopauseKnown = isPostmenopausal.HasValue;
        bool postmenopausal = isPostmenopausal ?? (ageYears >= 50);
        bool tScoreApplies =
            (isFemale && postmenopausal) ||
            (isMale && ageYears >= 50) ||
            // Sex or age unknown: fall back to T-score, but say so.
            (!isMale && !isFemale) || ageYears is null;

        if (tScoreApplies)
        {
            if (measurement.TScore is null)
            {
                result.Category = BoneDensityCategory.NoData;
                result.ScoreUsed = "none";
                result.Rationale = "No T-score reported for this site.";
                return result;
            }

            decimal t = measurement.TScore.Value;
            result.ScoreUsed = "T-score";
            result.Category =
                t <= -2.5m ? BoneDensityCategory.Osteoporosis :
                t < -1.0m ? BoneDensityCategory.LowBoneMass :
                BoneDensityCategory.Normal;

            string who = isFemale
                ? (menopauseKnown ? "postmenopausal woman" : $"woman aged {ageYears} (menopausal status not recorded)")
                : isMale ? $"man aged {ageYears}"
                : "patient of unrecorded sex";

            result.Rationale =
                $"WHO T-score criteria applied ({who}): T = {t:0.0} → {Describe(result.Category)}.";

            if (isFemale && !menopauseKnown)
                result.Rationale += " Menopausal status was assumed from age; record it to confirm the criteria apply.";
            if (ageYears is null)
                result.Rationale += " Age unknown — T-score used by default; confirm the criteria apply.";

            return result;
        }

        // Premenopausal women and men under 50: Z-score, expressed relative to age.
        if (measurement.ZScore is null)
        {
            result.Category = BoneDensityCategory.NoData;
            result.ScoreUsed = "none";
            result.Rationale =
                $"Z-score is required for this patient ({(isMale ? $"man aged {ageYears}" : $"premenopausal woman aged {ageYears}")}) " +
                "but none was reported. The WHO T-score categories do not apply here.";
            return result;
        }

        decimal z = measurement.ZScore.Value;
        result.ScoreUsed = "Z-score";
        result.Category = z <= -2.0m
            ? BoneDensityCategory.BelowExpectedRangeForAge
            : BoneDensityCategory.WithinExpectedRangeForAge;

        string who2 = isMale ? $"man aged {ageYears} (under 50)" : $"premenopausal woman aged {ageYears}";
        result.Rationale =
            $"WHO T-score criteria do NOT apply to a {who2}; classified on Z-score instead: " +
            $"Z = {z:0.0} → {Describe(result.Category)}." +
            (measurement.TScore is not null
                ? $" (A T-score of {measurement.TScore.Value:0.0} was reported but is not diagnostic in this group.)"
                : string.Empty);

        return result;
    }

    /// <summary>
    /// Compare the same site between two studies, stating explicitly whether the
    /// comparison is valid and whether any change exceeds measurement error.
    /// </summary>
    public static BoneDensityChange CompareSite(
        DxaScan earlier,
        DxaScan later,
        BoneDensitySite site)
    {
        DxaSiteMeasurement? a = earlier.Measurements.FirstOrDefault(m => m.Site == site);
        DxaSiteMeasurement? b = later.Measurements.FirstOrDefault(m => m.Site == site);

        var change = new BoneDensityChange
        {
            Site = site,
            FromDate = earlier.ScanDate,
            ToDate = later.ScanDate,
        };

        if (a is null || b is null)
        {
            change.Caveat = "Site not measured in both studies.";
            return change;
        }

        change.ChangeGramsPerCm2 = b.BmdGramsPerCm2 - a.BmdGramsPerCm2;
        change.PercentChange = a.BmdGramsPerCm2 == 0
            ? 0
            : Math.Round((b.BmdGramsPerCm2 - a.BmdGramsPerCm2) / a.BmdGramsPerCm2 * 100m, 1);

        change.SameScanner =
            !string.IsNullOrWhiteSpace(earlier.ScannerId) &&
            string.Equals(earlier.ScannerId, later.ScannerId, StringComparison.OrdinalIgnoreCase);

        // Prefer the later scan's LSC; fall back to the earlier one.
        decimal? lsc = later.LeastSignificantChangeGramsPerCm2 ?? earlier.LeastSignificantChangeGramsPerCm2;

        if (!change.SameScanner)
        {
            change.ExceedsLeastSignificantChange = false;
            change.Caveat = string.IsNullOrWhiteSpace(earlier.ScannerId) || string.IsNullOrWhiteSpace(later.ScannerId)
                ? "Scanner not recorded for at least one study — serial comparison cannot be validated."
                : "Studies were performed on different scanners; absolute BMD is not comparable between machines.";
            return change;
        }

        if (lsc is null)
        {
            change.ExceedsLeastSignificantChange = false;
            change.Caveat = "No least significant change recorded for this scanner — cannot say whether the difference exceeds measurement error.";
            return change;
        }

        change.ExceedsLeastSignificantChange = Math.Abs(change.ChangeGramsPerCm2) > lsc.Value;
        if (!change.ExceedsLeastSignificantChange)
            change.Caveat = $"Difference is within the scanner's least significant change ({lsc.Value:0.000} g/cm²) — not a real change.";

        return change;
    }

    /// <summary>
    /// Determine whether a turnover marker result can be compared with others, based on
    /// the recorded collection conditions.
    /// </summary>
    public static (BoneTurnoverInterpretability Interpretability, string? Caveat) ClassifyTurnoverMarker(
        BoneTurnoverMarkerResult result)
    {
        // Collection conditions matter most for CTX, but recording them is good practice
        // for every marker, so the same rule is applied uniformly.
        if (result.Fasting is null && !result.CollectionTimeKnown)
        {
            return (BoneTurnoverInterpretability.CollectionConditionsUnknown,
                "Fasting status and collection time were not recorded, so this value cannot be reliably compared with others.");
        }

        if (result.Fasting == false)
        {
            return (BoneTurnoverInterpretability.NotFasting,
                "Collected non-fasting. Food markedly suppresses CTX, so this value understates turnover by an unknown amount.");
        }

        if (result.CollectionTimeKnown && result.CollectedAt.Hour >= MorningDrawCutoffHour)
        {
            return (BoneTurnoverInterpretability.OutsideMorningWindow,
                $"Collected at {result.CollectedAt:HH:mm}. CTX falls through the day, so a draw after " +
                $"{MorningDrawCutoffHour:00}:00 is not comparable with fasting morning samples.");
        }

        if (result.Fasting is null)
        {
            return (BoneTurnoverInterpretability.CollectionConditionsUnknown,
                "Fasting status was not recorded; comparability with other results is uncertain.");
        }

        if (!result.CollectionTimeKnown)
        {
            return (BoneTurnoverInterpretability.CollectionConditionsUnknown,
                "Collection time of day was not recorded; comparability with other results is uncertain.");
        }

        return (BoneTurnoverInterpretability.Interpretable, null);
    }

    /// <summary>
    /// Build the full computed snapshot from raw state.
    /// </summary>
    /// <param name="state">Raw bone-health state.</param>
    /// <param name="sex">Patient sex — "M" or "F".</param>
    /// <param name="dateOfBirth">Patient date of birth, used to age the patient at each scan.</param>
    /// <param name="isPostmenopausal">Menopausal status where known.</param>
    /// <param name="asOf">Evaluation date, normally now.</param>
    public static BoneHealthSnapshot BuildSnapshot(
        BoneHealthState state,
        string? sex,
        DateTime? dateOfBirth,
        bool? isPostmenopausal,
        DateTime asOf)
    {
        var snapshot = new BoneHealthSnapshot
        {
            Icn = state.Icn,
            IsEnrolled = state.IsEnrolled,
            PrimaryDiagnosis = state.PrimaryDiagnosis,
        };

        List<DxaScan> scans = state.DxaScans.OrderBy(s => s.ScanDate).ToList();

        // ── Latest classified density per site ──────────────────────────────
        if (scans.Count > 0)
        {
            DxaScan latest = scans[^1];
            snapshot.LastDxaDate = latest.ScanDate;

            int? ageAtScan = AgeAt(dateOfBirth, latest.ScanDate);
            foreach (DxaSiteMeasurement m in latest.Measurements)
                snapshot.LatestDensities.Add(ClassifySite(m, latest.ScanDate, sex, ageAtScan, isPostmenopausal));

            // ── Change vs the previous study ────────────────────────────────
            if (scans.Count > 1)
            {
                DxaScan previous = scans[^2];
                IEnumerable<BoneDensitySite> sites = latest.Measurements
                    .Select(m => m.Site)
                    .Intersect(previous.Measurements.Select(m => m.Site));

                foreach (BoneDensitySite site in sites)
                {
                    BoneDensityChange change = CompareSite(previous, latest, site);
                    snapshot.DensityChanges.Add(change);
                    if (change.Caveat is not null && !change.SameScanner)
                        snapshot.Caveats.Add($"{Describe(site)}: {change.Caveat}");
                }
            }
        }

        // ── Fractures ───────────────────────────────────────────────────────
        List<BoneFracture> fragility = state.Fractures
            .Where(f => f.Mechanism == FractureMechanism.Fragility)
            .ToList();
        snapshot.FragilityFractureCount = fragility.Count;
        bool hipOrVertebralFragility = fragility.Any(f => f.IsHipOrVertebral);

        // ── Overall category ────────────────────────────────────────────────
        (snapshot.OverallCategory, snapshot.OverallRationale) =
            DeriveOverall(snapshot.LatestDensities, fragility.Count > 0, hipOrVertebralFragility);

        // ── Turnover markers, in order, with trend against the previous
        //    interpretable result of the same analyte ──────────────────────
        var previousInterpretable = new Dictionary<BoneTurnoverMarkerType, decimal>();
        foreach (BoneTurnoverMarkerResult r in state.TurnoverMarkers.OrderBy(r => r.CollectedAt))
        {
            (BoneTurnoverInterpretability interp, string? caveat) = ClassifyTurnoverMarker(r);
            var classified = new ClassifiedTurnoverMarker
            {
                Result = r,
                Interpretability = interp,
                Caveat = caveat,
                TherapyInForce = TherapyInForceOn(state.Therapies, r.CollectedAt),
            };

            if (interp == BoneTurnoverInterpretability.Interpretable &&
                previousInterpretable.TryGetValue(r.MarkerType, out decimal prev) && prev != 0)
            {
                classified.PercentChangeFromPrevious = Math.Round((r.Value - prev) / prev * 100m, 1);
            }

            if (interp == BoneTurnoverInterpretability.Interpretable)
                previousInterpretable[r.MarkerType] = r.Value;
            else if (caveat is not null)
                snapshot.Caveats.Add($"{Describe(r.MarkerType)} {r.CollectedAt:yyyy-MM-dd}: {caveat}");

            snapshot.TurnoverMarkers.Add(classified);
        }

        // ── Therapy and workups ─────────────────────────────────────────────
        snapshot.ActiveTherapies = state.Therapies
            .Where(t => t.StartDate <= asOf && (t.StopDate is null || t.StopDate > asOf))
            .OrderBy(t => t.StartDate)
            .ToList();

        snapshot.LatestFrax = state.FraxAssessments
            .OrderByDescending(f => f.AssessmentDate)
            .FirstOrDefault();

        snapshot.IdentifiedSecondaryCauses = state.SecondaryWorkups
            .SelectMany(w => w.IdentifiedCauses)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return snapshot;
    }

    // ── Internals ───────────────────────────────────────────────────────────

    private static (BoneDensityCategory, string) DeriveOverall(
        List<ClassifiedBoneDensity> densities,
        bool anyFragility,
        bool hipOrVertebralFragility)
    {
        // A hip or vertebral fragility fracture is diagnostic on its own, whatever the BMD.
        if (hipOrVertebralFragility)
        {
            return (BoneDensityCategory.ClinicalOsteoporosis,
                "A hip or vertebral fragility fracture is diagnostic of osteoporosis irrespective of bone mineral density.");
        }

        List<ClassifiedBoneDensity> diagnostic = densities
            .Where(d => DiagnosticSites.Contains(d.Site) && d.Category != BoneDensityCategory.NoData)
            .ToList();

        if (diagnostic.Count == 0)
        {
            return (BoneDensityCategory.NoData,
                densities.Count == 0
                    ? "No DXA measurements recorded."
                    : "No measurement at a diagnostic site (lumbar spine, femoral neck or total hip).");
        }

        // Diagnosis is made from the worst diagnostic site.
        ClassifiedBoneDensity worst = diagnostic
            .OrderByDescending(d => Severity(d.Category))
            .First();

        BoneDensityCategory category = worst.Category;
        string basis = $"Lowest diagnostic site is {Describe(worst.Site)} ({worst.ScoreUsed} " +
                       $"{(worst.ScoreUsed == "T-score" ? worst.TScore : worst.ZScore):0.0}).";

        if (category == BoneDensityCategory.Osteoporosis && anyFragility)
        {
            return (BoneDensityCategory.SevereOsteoporosis,
                basis + " Escalated to severe (established) osteoporosis by a recorded fragility fracture.");
        }

        return (category, basis);
    }

    private static int Severity(BoneDensityCategory c) => c switch
    {
        BoneDensityCategory.SevereOsteoporosis => 6,
        BoneDensityCategory.ClinicalOsteoporosis => 5,
        BoneDensityCategory.Osteoporosis => 4,
        BoneDensityCategory.BelowExpectedRangeForAge => 3,
        BoneDensityCategory.LowBoneMass => 2,
        BoneDensityCategory.WithinExpectedRangeForAge => 1,
        BoneDensityCategory.Normal => 1,
        _ => 0,
    };

    private static string? TherapyInForceOn(List<OsteoporosisTherapyCourse> therapies, DateTime on)
    {
        List<string> active = therapies
            .Where(t => t.StartDate <= on && (t.StopDate is null || t.StopDate > on))
            .Select(t => t.AgentName)
            .ToList();
        return active.Count == 0 ? null : string.Join(", ", active);
    }

    private static int? AgeAt(DateTime? dateOfBirth, DateTime on)
    {
        if (dateOfBirth is null) return null;
        int age = on.Year - dateOfBirth.Value.Year;
        if (on < dateOfBirth.Value.AddYears(age)) age--;
        return age;
    }

    private static string Describe(BoneDensityCategory c) => c switch
    {
        BoneDensityCategory.Normal => "normal",
        BoneDensityCategory.LowBoneMass => "low bone mass",
        BoneDensityCategory.Osteoporosis => "osteoporosis",
        BoneDensityCategory.SevereOsteoporosis => "severe (established) osteoporosis",
        BoneDensityCategory.BelowExpectedRangeForAge => "below the expected range for age",
        BoneDensityCategory.WithinExpectedRangeForAge => "within the expected range for age",
        BoneDensityCategory.ClinicalOsteoporosis => "clinical osteoporosis",
        _ => "no data",
    };

    private static string Describe(BoneDensitySite s) => s switch
    {
        BoneDensitySite.LumbarSpine => "Lumbar spine",
        BoneDensitySite.FemoralNeck => "Femoral neck",
        BoneDensitySite.TotalHip => "Total hip",
        BoneDensitySite.ForearmRadius33 => "33% radius",
        BoneDensitySite.TotalBody => "Total body",
        _ => "Unknown site",
    };

    private static string Describe(BoneTurnoverMarkerType t) => t switch
    {
        BoneTurnoverMarkerType.SerumCtx => "CTX",
        BoneTurnoverMarkerType.P1np => "P1NP",
        BoneTurnoverMarkerType.BoneSpecificAlkalinePhosphatase => "Bone-specific ALP",
        BoneTurnoverMarkerType.Osteocalcin => "Osteocalcin",
        BoneTurnoverMarkerType.UrineNtx => "Urine NTX",
        _ => "Marker",
    };
}
