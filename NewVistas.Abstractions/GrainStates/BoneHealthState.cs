// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

// ── Enums ───────────────────────────────────────────────────────────────────

/// <summary>
/// Skeletal site measured by DXA. Sites are NOT interchangeable — diagnosis is made
/// from the lowest of lumbar spine, femoral neck and total hip; the 33% radius is
/// reserved for cases where spine/hip are unusable (hardware, severe degenerative
/// change) or for hyperparathyroidism, where cortical bone is preferentially lost.
/// </summary>
[GenerateSerializer]
public enum BoneDensitySite
{
    Unknown = 0,
    LumbarSpine = 1,
    FemoralNeck = 2,
    TotalHip = 3,
    ForearmRadius33 = 4,
    TotalBody = 5,
}

/// <summary>
/// Diagnostic category derived from a DXA measurement.
///
/// The WHO T-score thresholds are only valid for postmenopausal women and for men
/// aged 50 and over. For premenopausal women and men under 50 the Z-score is used and
/// the answer is expressed as within/below the expected range for age — applying the
/// T-score categories to those groups is a diagnostic error, not a display preference.
/// See <c>BoneDensityClassifier</c>.
/// </summary>
[GenerateSerializer]
public enum BoneDensityCategory
{
    NoData = 0,

    /// <summary>T-score ≥ −1.0.</summary>
    Normal = 1,

    /// <summary>T-score between −1.0 and −2.5 (formerly "osteopenia").</summary>
    LowBoneMass = 2,

    /// <summary>T-score ≤ −2.5.</summary>
    Osteoporosis = 3,

    /// <summary>T-score ≤ −2.5 with one or more fragility fractures.</summary>
    SevereOsteoporosis = 4,

    /// <summary>Z-score ≤ −2.0 in a premenopausal woman or a man under 50.</summary>
    BelowExpectedRangeForAge = 5,

    /// <summary>Z-score &gt; −2.0 in a premenopausal woman or a man under 50.</summary>
    WithinExpectedRangeForAge = 6,

    /// <summary>
    /// Osteoporosis established clinically by a hip or vertebral fragility fracture,
    /// which is diagnostic irrespective of the measured BMD.
    /// </summary>
    ClinicalOsteoporosis = 7,
}

/// <summary>
/// Bone turnover marker analyte. s-CTX (resorption) and P1NP (formation) are the
/// IOF/IFCC reference markers and are the two that drive treatment monitoring.
/// </summary>
[GenerateSerializer]
public enum BoneTurnoverMarkerType
{
    Unknown = 0,

    /// <summary>Serum C-terminal telopeptide of type I collagen — resorption marker.</summary>
    SerumCtx = 1,

    /// <summary>Procollagen type 1 N-terminal propeptide — formation marker.</summary>
    P1np = 2,

    /// <summary>Bone-specific alkaline phosphatase — formation marker.</summary>
    BoneSpecificAlkalinePhosphatase = 3,

    /// <summary>Osteocalcin — formation marker.</summary>
    Osteocalcin = 4,

    /// <summary>Urine N-terminal telopeptide — resorption marker.</summary>
    UrineNtx = 5,
}

/// <summary>
/// Whether a bone turnover result can be compared against others.
///
/// CTX in particular has large circadian variation and is markedly suppressed by
/// food, so a non-fasting or afternoon draw is not comparable with a fasting morning
/// one. Trending such a result silently produces a confident wrong answer, so the
/// system marks it instead of plotting it as if it were equivalent.
/// </summary>
[GenerateSerializer]
public enum BoneTurnoverInterpretability
{
    Unknown = 0,

    /// <summary>Fasting morning draw — comparable with other interpretable results.</summary>
    Interpretable = 1,

    /// <summary>Patient was not fasting; value is suppressed by an unknown amount.</summary>
    NotFasting = 2,

    /// <summary>Drawn outside the morning window; circadian variation makes it non-comparable.</summary>
    OutsideMorningWindow = 3,

    /// <summary>Collection conditions were not recorded, so comparability is unknown.</summary>
    CollectionConditionsUnknown = 4,

    /// <summary>Run on a different assay or platform than the comparator.</summary>
    AssayChanged = 5,
}

/// <summary>Drug class of an osteoporosis treatment course.</summary>
[GenerateSerializer]
public enum OsteoporosisTherapyClass
{
    Unknown = 0,

    /// <summary>Oral or IV bisphosphonate (alendronate, risedronate, ibandronate, zoledronic acid).</summary>
    Bisphosphonate = 1,

    /// <summary>RANK-ligand inhibitor (denosumab). Discontinuation requires a transition plan.</summary>
    RankLigandInhibitor = 2,

    /// <summary>PTH analogue anabolic (teriparatide, abaloparatide). Duration-limited.</summary>
    AnabolicPthAnalogue = 3,

    /// <summary>Sclerostin inhibitor (romosozumab). Carries a cardiovascular boxed warning.</summary>
    SclerostinInhibitor = 4,

    /// <summary>Selective oestrogen receptor modulator (raloxifene).</summary>
    Serm = 5,

    /// <summary>Menopausal hormone therapy.</summary>
    HormoneTherapy = 6,

    /// <summary>Testosterone replacement in hypogonadal men.</summary>
    TestosteroneReplacement = 7,

    /// <summary>Calcium and/or vitamin D supplementation.</summary>
    Supplement = 8,
}

/// <summary>How a fracture was sustained. Fragility fractures carry diagnostic weight.</summary>
[GenerateSerializer]
public enum FractureMechanism
{
    Unknown = 0,

    /// <summary>Fall from standing height or less, or no identifiable trauma — a fragility fracture.</summary>
    Fragility = 1,

    /// <summary>Significant trauma (road traffic, fall from height).</summary>
    Trauma = 2,

    /// <summary>Through a focal lesion (metastasis, myeloma) — not an osteoporotic fracture.</summary>
    Pathologic = 3,
}

// ── Records held in state ───────────────────────────────────────────────────

/// <summary>
/// One site measurement within a DXA study.
/// </summary>
[GenerateSerializer]
public class DxaSiteMeasurement
{
    /// <summary>Skeletal site measured.</summary>
    [Id(0)] public BoneDensitySite Site { get; set; }

    /// <summary>Areal bone mineral density in g/cm².</summary>
    [Id(1)] public decimal BmdGramsPerCm2 { get; set; }

    /// <summary>T-score — standard deviations from the young-adult reference mean.</summary>
    [Id(2)] public decimal? TScore { get; set; }

    /// <summary>Z-score — standard deviations from an age- and sex-matched reference mean.</summary>
    [Id(3)] public decimal? ZScore { get; set; }

    /// <summary>Which vertebrae or sub-region was actually included (e.g. "L1-L4", "L2-L4 (L1 excluded)").</summary>
    [Id(4)] public string? RegionDetail { get; set; }
}

/// <summary>
/// One DXA study. BMD is only comparable across studies performed on the SAME scanner;
/// a change smaller than that scanner's least significant change is not a real change.
/// Both facts are recorded here so trending can enforce them rather than assume them.
/// </summary>
[GenerateSerializer]
public class DxaScan
{
    /// <summary>Unique id for this scan.</summary>
    [Id(0)] public string ScanId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Date the study was performed.</summary>
    [Id(1)] public DateTime ScanDate { get; set; }

    /// <summary>Per-site measurements from this study.</summary>
    [Id(2)] public List<DxaSiteMeasurement> Measurements { get; set; } = new();

    /// <summary>
    /// Identifier of the physical scanner. Serial trending is only valid within one
    /// scanner — cross-machine comparison of absolute BMD is not interpretable.
    /// </summary>
    [Id(3)] public string? ScannerId { get; set; }

    /// <summary>Scanner manufacturer and model (e.g. "Hologic Horizon A").</summary>
    [Id(4)] public string? ScannerModel { get; set; }

    /// <summary>Facility where the study was performed.</summary>
    [Id(5)] public string? FacilityName { get; set; }

    /// <summary>
    /// Least significant change for this scanner, in g/cm² — the smallest BMD difference
    /// that exceeds measurement precision error. Changes below it are noise.
    /// </summary>
    [Id(6)] public decimal? LeastSignificantChangeGramsPerCm2 { get; set; }

    /// <summary>Reference database used to derive T-scores (e.g. "NHANES III female").</summary>
    [Id(7)] public string? ReferenceDatabase { get; set; }

    /// <summary>Interpreting provider.</summary>
    [Id(8)] public string? InterpretedByName { get; set; }

    /// <summary>Free-text comment or impression.</summary>
    [Id(9)] public string? Comment { get; set; }

    /// <summary>Link back to the source radiology study, when the scan arrived that way.</summary>
    [Id(10)] public string? SourceRadiologyOrderId { get; set; }
}

/// <summary>
/// One bone turnover marker result, with the collection conditions that determine
/// whether it can be compared with any other result.
/// </summary>
[GenerateSerializer]
public class BoneTurnoverMarkerResult
{
    /// <summary>Unique id for this result.</summary>
    [Id(0)] public string ResultId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Which analyte was measured.</summary>
    [Id(1)] public BoneTurnoverMarkerType MarkerType { get; set; }

    /// <summary>Measured value.</summary>
    [Id(2)] public decimal Value { get; set; }

    /// <summary>Units as reported (CTX is commonly pg/mL, equivalently ng/L).</summary>
    [Id(3)] public string Units { get; set; } = string.Empty;

    /// <summary>Date and, where known, time of specimen collection.</summary>
    [Id(4)] public DateTime CollectedAt { get; set; }

    /// <summary>Whether the collection time-of-day was actually recorded (vs a date-only result).</summary>
    [Id(5)] public bool CollectionTimeKnown { get; set; }

    /// <summary>Whether the patient was fasting. Null when not recorded.</summary>
    [Id(6)] public bool? Fasting { get; set; }

    /// <summary>Assay or platform, since results are not comparable across assays.</summary>
    [Id(7)] public string? Assay { get; set; }

    /// <summary>Performing laboratory.</summary>
    [Id(8)] public string? PerformingLab { get; set; }

    /// <summary>Lower bound of the reference interval, when supplied.</summary>
    [Id(9)] public decimal? ReferenceLow { get; set; }

    /// <summary>Upper bound of the reference interval, when supplied.</summary>
    [Id(10)] public decimal? ReferenceHigh { get; set; }

    /// <summary>Free-text comment.</summary>
    [Id(11)] public string? Comment { get; set; }

    /// <summary>
    /// The lab test this result came from, when it was ordered through CPOE. Lets the
    /// bone-health trajectory point back at the order that produced each value instead of
    /// floating free of the rest of the chart.
    /// </summary>
    [Id(12)] public string? SourceLabTestId { get; set; }
}

/// <summary>A fracture recorded for bone-health purposes.</summary>
[GenerateSerializer]
public class BoneFracture
{
    /// <summary>Unique id for this fracture.</summary>
    [Id(0)] public string FractureId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Anatomic site (e.g. "Distal radius", "L1 vertebral body", "Femoral neck").</summary>
    [Id(1)] public string Site { get; set; } = string.Empty;

    /// <summary>Date of fracture.</summary>
    [Id(2)] public DateTime FractureDate { get; set; }

    /// <summary>How it was sustained.</summary>
    [Id(3)] public FractureMechanism Mechanism { get; set; }

    /// <summary>Whether imaging confirmed the fracture.</summary>
    [Id(4)] public bool ImagingConfirmed { get; set; }

    /// <summary>
    /// Genant semiquantitative grade for vertebral fractures (1 mild, 2 moderate, 3 severe).
    /// Null for non-vertebral fractures.
    /// </summary>
    [Id(5)] public int? VertebralGrade { get; set; }

    /// <summary>True when this is a hip or vertebral fracture, which are independently diagnostic.</summary>
    [Id(6)] public bool IsHipOrVertebral { get; set; }

    /// <summary>Free-text comment.</summary>
    [Id(7)] public string? Comment { get; set; }
}

/// <summary>One course of osteoporosis therapy.</summary>
[GenerateSerializer]
public class OsteoporosisTherapyCourse
{
    /// <summary>Unique id for this course.</summary>
    [Id(0)] public string CourseId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Agent name (e.g. "Teriparatide", "Denosumab", "Alendronate").</summary>
    [Id(1)] public string AgentName { get; set; } = string.Empty;

    /// <summary>Drug class, which drives the safety rules that apply to this course.</summary>
    [Id(2)] public OsteoporosisTherapyClass TherapyClass { get; set; }

    /// <summary>Date therapy started.</summary>
    [Id(3)] public DateTime StartDate { get; set; }

    /// <summary>Date therapy stopped, if it has.</summary>
    [Id(4)] public DateTime? StopDate { get; set; }

    /// <summary>Dose as prescribed (e.g. "20 mcg SC daily", "60 mg SC every 6 months").</summary>
    [Id(5)] public string? Dose { get; set; }

    /// <summary>Nominal interval between doses in days, where the agent is intermittently dosed.</summary>
    [Id(6)] public int? DosingIntervalDays { get; set; }

    /// <summary>When the next dose is due, for interval-dosed agents.</summary>
    [Id(7)] public DateTime? NextDoseDue { get; set; }

    /// <summary>Date a bisphosphonate drug holiday began, if applicable.</summary>
    [Id(8)] public DateTime? HolidayStartDate { get; set; }

    /// <summary>Why therapy was stopped.</summary>
    [Id(9)] public string? StopReason { get; set; }

    /// <summary>
    /// The agent this course was transitioned to on stopping. Recorded because stopping
    /// a RANK-ligand inhibitor without follow-on antiresorptive therapy causes rebound
    /// bone loss and multiple vertebral fractures.
    /// </summary>
    [Id(10)] public string? TransitionedToAgent { get; set; }

    /// <summary>Prescribing provider.</summary>
    [Id(11)] public string? PrescriberName { get; set; }
}

/// <summary>
/// A recorded FRAX fracture-risk assessment. The inputs are snapshotted alongside the
/// outputs so an old result stays interpretable after the chart has moved on.
/// </summary>
[GenerateSerializer]
public class FraxAssessment
{
    /// <summary>Unique id for this assessment.</summary>
    [Id(0)] public string AssessmentId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Date the assessment was performed.</summary>
    [Id(1)] public DateTime AssessmentDate { get; set; }

    /// <summary>10-year probability of major osteoporotic fracture, percent.</summary>
    [Id(2)] public decimal MajorOsteoporoticFracturePercent { get; set; }

    /// <summary>10-year probability of hip fracture, percent.</summary>
    [Id(3)] public decimal HipFracturePercent { get; set; }

    /// <summary>Whether femoral neck BMD was included in the calculation.</summary>
    [Id(4)] public bool IncludedFemoralNeckBmd { get; set; }

    /// <summary>Country calibration used — FRAX is country-specific.</summary>
    [Id(5)] public string? CountryCalibration { get; set; }

    /// <summary>Tool version, since thresholds and calibration change between releases.</summary>
    [Id(6)] public string? ToolVersion { get; set; }

    /// <summary>Snapshot of the clinical risk factors supplied, for later re-interpretation.</summary>
    [Id(7)] public List<string> RiskFactorsUsed { get; set; } = new();
}

/// <summary>
/// A secondary-cause workup. Roughly half of osteoporosis in men is secondary, which is
/// why this is a first-class part of the record rather than a note.
/// </summary>
[GenerateSerializer]
public class SecondaryCauseWorkup
{
    /// <summary>Unique id for this workup.</summary>
    [Id(0)] public string WorkupId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Date the workup was performed.</summary>
    [Id(1)] public DateTime WorkupDate { get; set; }

    /// <summary>Analyte name → result as reported (e.g. "25-OH vitamin D" → "18 ng/mL").</summary>
    [Id(2)] public Dictionary<string, string> Results { get; set; } = new();

    /// <summary>Causes identified by this workup.</summary>
    [Id(3)] public List<string> IdentifiedCauses { get; set; } = new();

    /// <summary>Ordering provider.</summary>
    [Id(4)] public string? OrderedByName { get; set; }

    /// <summary>Free-text comment.</summary>
    [Id(5)] public string? Comment { get; set; }
}

// ── Per-patient state ───────────────────────────────────────────────────────

/// <summary>
/// Persistent state for one patient's bone-health record. Grain key: <c>"BONE:{icn}"</c>.
///
/// Osteoporosis is managed over decades, so this is a longitudinal record rather than a
/// snapshot: serial DXA studies, serial bone turnover markers, the fracture history, and
/// the sequence of therapy courses. The point of structuring it is that none of these
/// mean anything in isolation — a bone turnover marker is interpreted against the therapy
/// in force when it was drawn, and a BMD is interpreted against the previous scan on the
/// same machine.
/// </summary>
[GenerateSerializer]
public class BoneHealthState
{
    /// <summary>The patient's ICN (also the grain key suffix).</summary>
    [Id(0)] public string Icn { get; set; } = string.Empty;

    /// <summary>True once the patient has a bone-health record open.</summary>
    [Id(1)] public bool IsEnrolled { get; set; }

    /// <summary>Date the record was opened.</summary>
    [Id(2)] public DateTime? EnrollmentDate { get; set; }

    /// <summary>Working diagnosis (e.g. "Osteoporosis", "Osteopenia", "Glucocorticoid-induced osteoporosis").</summary>
    [Id(3)] public string? PrimaryDiagnosis { get; set; }

    /// <summary>Serial DXA studies, oldest first.</summary>
    [Id(4)] public List<DxaScan> DxaScans { get; set; } = new();

    /// <summary>Serial bone turnover marker results, oldest first.</summary>
    [Id(5)] public List<BoneTurnoverMarkerResult> TurnoverMarkers { get; set; } = new();

    /// <summary>Fracture history relevant to bone health.</summary>
    [Id(6)] public List<BoneFracture> Fractures { get; set; } = new();

    /// <summary>Therapy courses, oldest first.</summary>
    [Id(7)] public List<OsteoporosisTherapyCourse> Therapies { get; set; } = new();

    /// <summary>Recorded FRAX assessments, oldest first.</summary>
    [Id(8)] public List<FraxAssessment> FraxAssessments { get; set; } = new();

    /// <summary>Secondary-cause workups, oldest first.</summary>
    [Id(9)] public List<SecondaryCauseWorkup> SecondaryWorkups { get; set; } = new();

    /// <summary>Record creation timestamp.</summary>
    [Id(10)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Last modification timestamp.</summary>
    [Id(11)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

// ── Computed views ──────────────────────────────────────────────────────────

/// <summary>A DXA site measurement paired with the diagnostic category derived from it.</summary>
[GenerateSerializer]
public class ClassifiedBoneDensity
{
    /// <summary>Site measured.</summary>
    [Id(0)] public BoneDensitySite Site { get; set; }

    /// <summary>Date of the study this came from.</summary>
    [Id(1)] public DateTime ScanDate { get; set; }

    /// <summary>Areal BMD in g/cm².</summary>
    [Id(2)] public decimal BmdGramsPerCm2 { get; set; }

    /// <summary>T-score, when reported.</summary>
    [Id(3)] public decimal? TScore { get; set; }

    /// <summary>Z-score, when reported.</summary>
    [Id(4)] public decimal? ZScore { get; set; }

    /// <summary>Derived diagnostic category.</summary>
    [Id(5)] public BoneDensityCategory Category { get; set; }

    /// <summary>Which score the classification actually used — "T-score" or "Z-score".</summary>
    [Id(6)] public string ScoreUsed { get; set; } = string.Empty;

    /// <summary>Plain-language explanation of why that score and category were chosen.</summary>
    [Id(7)] public string Rationale { get; set; } = string.Empty;
}

/// <summary>
/// Change in BMD at one site between two studies, with an explicit statement of whether
/// the comparison is even valid.
/// </summary>
[GenerateSerializer]
public class BoneDensityChange
{
    /// <summary>Site compared.</summary>
    [Id(0)] public BoneDensitySite Site { get; set; }

    /// <summary>Date of the earlier study.</summary>
    [Id(1)] public DateTime FromDate { get; set; }

    /// <summary>Date of the later study.</summary>
    [Id(2)] public DateTime ToDate { get; set; }

    /// <summary>Absolute change in g/cm² (positive is a gain).</summary>
    [Id(3)] public decimal ChangeGramsPerCm2 { get; set; }

    /// <summary>Percent change relative to the earlier value.</summary>
    [Id(4)] public decimal PercentChange { get; set; }

    /// <summary>True when both studies came from the same scanner, so the comparison is valid.</summary>
    [Id(5)] public bool SameScanner { get; set; }

    /// <summary>
    /// True when the change exceeds the scanner's least significant change and can be
    /// called real. False means the difference is within measurement precision error.
    /// </summary>
    [Id(6)] public bool ExceedsLeastSignificantChange { get; set; }

    /// <summary>Plain-language caveat where the comparison is limited or invalid.</summary>
    [Id(7)] public string? Caveat { get; set; }
}

/// <summary>A turnover marker result paired with its computed interpretability.</summary>
[GenerateSerializer]
public class ClassifiedTurnoverMarker
{
    /// <summary>The underlying result.</summary>
    [Id(0)] public BoneTurnoverMarkerResult Result { get; set; } = new();

    /// <summary>Whether this value can be compared with others.</summary>
    [Id(1)] public BoneTurnoverInterpretability Interpretability { get; set; }

    /// <summary>Plain-language explanation when the value is not straightforwardly comparable.</summary>
    [Id(2)] public string? Caveat { get; set; }

    /// <summary>Percent change from the previous interpretable result of the same analyte.</summary>
    [Id(3)] public decimal? PercentChangeFromPrevious { get; set; }

    /// <summary>Name of the therapy in force on the collection date, if any.</summary>
    [Id(4)] public string? TherapyInForce { get; set; }
}

/// <summary>
/// Computed view of a bone-health record. Everything here is derived — the grain stores
/// observations only, and the rules live in <c>BoneDensityClassifier</c>.
/// </summary>
[GenerateSerializer]
public class BoneHealthSnapshot
{
    /// <summary>Patient ICN.</summary>
    [Id(0)] public string Icn { get; set; } = string.Empty;

    /// <summary>Whether a bone-health record is open for this patient.</summary>
    [Id(1)] public bool IsEnrolled { get; set; }

    /// <summary>Working diagnosis.</summary>
    [Id(2)] public string? PrimaryDiagnosis { get; set; }

    /// <summary>Most recent measurement at each site, classified.</summary>
    [Id(3)] public List<ClassifiedBoneDensity> LatestDensities { get; set; } = new();

    /// <summary>
    /// Overall diagnostic category — the worst of the classified sites, escalated to
    /// clinical osteoporosis by a hip or vertebral fragility fracture.
    /// </summary>
    [Id(4)] public BoneDensityCategory OverallCategory { get; set; }

    /// <summary>Explanation of how the overall category was reached.</summary>
    [Id(5)] public string OverallRationale { get; set; } = string.Empty;

    /// <summary>Date of the most recent DXA study.</summary>
    [Id(6)] public DateTime? LastDxaDate { get; set; }

    /// <summary>Site-by-site change between the two most recent studies.</summary>
    [Id(7)] public List<BoneDensityChange> DensityChanges { get; set; } = new();

    /// <summary>Turnover marker results with interpretability and trend, oldest first.</summary>
    [Id(8)] public List<ClassifiedTurnoverMarker> TurnoverMarkers { get; set; } = new();

    /// <summary>Therapy courses currently in force.</summary>
    [Id(9)] public List<OsteoporosisTherapyCourse> ActiveTherapies { get; set; } = new();

    /// <summary>Count of recorded fragility fractures.</summary>
    [Id(10)] public int FragilityFractureCount { get; set; }

    /// <summary>Most recent FRAX assessment, if any.</summary>
    [Id(11)] public FraxAssessment? LatestFrax { get; set; }

    /// <summary>Secondary causes identified across all workups.</summary>
    [Id(12)] public List<string> IdentifiedSecondaryCauses { get; set; } = new();

    /// <summary>
    /// Interpretation caveats that a clinician should see before reading the numbers —
    /// non-comparable scanners, uninterpretable marker draws, missing collection conditions.
    /// </summary>
    [Id(13)] public List<string> Caveats { get; set; } = new();
}

// ── Index ───────────────────────────────────────────────────────────────────

/// <summary>
/// Site-wide index of patients with an open bone-health record. Grain key:
/// <c>"BONE-HEALTH-IDX"</c>.
/// </summary>
[GenerateSerializer]
public class BoneHealthIndexState
{
    /// <summary>ICN → enrollment date, for cohort enumeration without per-patient fan-out.</summary>
    [Id(0)] public Dictionary<string, DateTime> EnrolledIcns { get; set; } = new();
}
