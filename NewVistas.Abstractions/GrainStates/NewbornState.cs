// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

/// <summary>Newborn sex (separate from the OB infant-sex free-text field).</summary>
public enum NewbornSex
{
    Unknown = 0,
    Male = 1,
    Female = 2,
    Ambiguous = 3
}

/// <summary>Gestational-age classification at birth (AAP/WHO bands).</summary>
public enum GestationalAgeClassification
{
    Unknown = 0,
    ExtremelyPreterm = 1,  // < 28 wks
    VeryPreterm = 2,       // 28 to < 32 wks
    Preterm = 3,           // 32 to < 34 wks
    LatePreterm = 4,       // 34 to < 37 wks
    Term = 5,              // 37 to < 42 wks
    PostTerm = 6           // >= 42 wks
}

/// <summary>Birth-weight magnitude category.</summary>
public enum BirthWeightCategory
{
    Unknown = 0,
    ExtremelyLowBirthWeight = 1,  // < 1000 g
    VeryLowBirthWeight = 2,       // < 1500 g
    LowBirthWeight = 3,           // < 2500 g
    Normal = 4,                   // 2500 to < 4000 g
    Macrosomia = 5                // >= 4000 g
}

/// <summary>Weight-for-gestational-age (growth) classification.</summary>
public enum SizeForGestationalAge
{
    Unknown = 0,
    SmallForGestationalAge = 1,    // < 10th percentile
    AppropriateForGestationalAge = 2,
    LargeForGestationalAge = 3     // > 90th percentile
}

/// <summary>Nursery level of care — AAP levels of newborn care I–IV.</summary>
public enum NurseryLevelOfCare
{
    WellNewborn = 0,         // Level I — well-baby nursery
    SpecialCareLevelII = 1,  // Level II — special care
    NicuLevelIII = 2,        // Level III — NICU
    NicuRegionalLevelIV = 3  // Level IV — regional NICU
}

/// <summary>Lifecycle status of a newborn record.</summary>
public enum NewbornStatus
{
    Admitted = 0,
    Discharged = 1,
    Transferred = 2,
    Deceased = 3
}

/// <summary>Universal / routine newborn screens.</summary>
public enum NewbornScreeningType
{
    MetabolicBloodSpot = 0,            // state newborn-screening panel (RUSP), heel-stick
    CriticalCongenitalHeartDisease = 1, // CCHD pulse-oximetry
    Hearing = 2,                        // OAE / ABR
    Bilirubin = 3,                      // transcutaneous / serum bilirubin
    Glucose = 4
}

/// <summary>Outcome of a newborn screen.</summary>
public enum NewbornScreeningResult
{
    Pending = 0,
    Pass = 1,
    ReferOrFail = 2,
    Inconclusive = 3,
    NotDone = 4
}

/// <summary>How the newborn is being fed.</summary>
public enum NewbornFeedingType
{
    Breast = 0,
    Formula = 1,
    Mixed = 2,
    IvTpn = 3,
    Npo = 4
}

/// <summary>Newborn physical examination at/after birth.</summary>
[GenerateSerializer]
public class NewbornExam
{
    [Id(0)] public string General { get; set; } = string.Empty;
    [Id(1)] public string Heent { get; set; } = string.Empty;
    [Id(2)] public string Cardiac { get; set; } = string.Empty;
    [Id(3)] public string Respiratory { get; set; } = string.Empty;
    [Id(4)] public string Abdomen { get; set; } = string.Empty;
    [Id(5)] public string Genitourinary { get; set; } = string.Empty;
    [Id(6)] public string Musculoskeletal { get; set; } = string.Empty;
    [Id(7)] public string Neurologic { get; set; } = string.Empty;
    [Id(8)] public string Skin { get; set; } = string.Empty;
    [Id(9)] public string Impression { get; set; } = string.Empty;
    [Id(10)] public string ExaminerName { get; set; } = string.Empty;
    [Id(11)] public DateTime? ExamDate { get; set; }
}

/// <summary>One newborn screen result.</summary>
[GenerateSerializer]
public class NewbornScreeningEntry
{
    [Id(0)] public NewbornScreeningType ScreeningType { get; set; }
    [Id(1)] public NewbornScreeningResult Result { get; set; } = NewbornScreeningResult.Pending;
    /// <summary>Free-text value, e.g. "pre-ductal 99% / post-ductal 98%" or "TSB 7.2 mg/dL, low-risk zone".</summary>
    [Id(2)] public string ValueText { get; set; } = string.Empty;
    [Id(3)] public DateTime? PerformedDate { get; set; }
    [Id(4)] public string PerformedBy { get; set; } = string.Empty;
    [Id(5)] public string Notes { get; set; } = string.Empty;
}

/// <summary>An interval measurement during the birth stay (daily weight, feeding, bilirubin).</summary>
[GenerateSerializer]
public class NewbornMeasurement
{
    [Id(0)] public DateTime MeasuredAt { get; set; }
    [Id(1)] public int? WeightGrams { get; set; }
    [Id(2)] public NewbornFeedingType FeedingType { get; set; }
    [Id(3)] public string FeedingNotes { get; set; } = string.Empty;
    [Id(4)] public decimal? BilirubinMgDl { get; set; }
    [Id(5)] public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// A newborn's neonatal chart — birth through nursery discharge. Registered from the mother's
/// delivery and linked back to her pregnancy (supports multiples). Key pattern: "NEONATE:{guid}".
/// </summary>
[GenerateSerializer]
public class NewbornState
{
    [Id(0)] public string NewbornId { get; set; } = string.Empty;

    // ── Linkage ──────────────────────────────────────────────────────────────
    [Id(1)] public string MotherPatientId { get; set; } = string.Empty;
    [Id(2)] public string PregnancyId { get; set; } = string.Empty;
    /// <summary>Reserved — set when the newborn is promoted to a fully registered patient (own MRN).</summary>
    [Id(3)] public string NewbornPatientId { get; set; } = string.Empty;

    // ── Identity ─────────────────────────────────────────────────────────────
    [Id(4)] public string Name { get; set; } = string.Empty;  // e.g. "BABY GIRL SMITH"
    [Id(5)] public NewbornSex Sex { get; set; }
    [Id(6)] public DateTime BirthDateTime { get; set; }
    [Id(7)] public int MultipleBirthOrder { get; set; } = 1;
    [Id(8)] public int MultipleBirthTotal { get; set; } = 1;

    // ── Birth data ───────────────────────────────────────────────────────────
    [Id(9)] public DeliveryMethod DeliveryMethod { get; set; }
    [Id(10)] public int GestationalAgeWeeks { get; set; }
    [Id(11)] public int GestationalAgeDays { get; set; }
    [Id(12)] public int? BirthWeightGrams { get; set; }
    [Id(13)] public decimal? LengthCm { get; set; }
    [Id(14)] public decimal? HeadCircumferenceCm { get; set; }
    [Id(15)] public int? Apgar1Min { get; set; }
    [Id(16)] public int? Apgar5Min { get; set; }
    [Id(17)] public int? Apgar10Min { get; set; }
    [Id(18)] public bool ResuscitationProvided { get; set; }
    [Id(19)] public string ResuscitationDetail { get; set; } = string.Empty;
    [Id(20)] public bool CordBloodCollected { get; set; }
    [Id(21)] public string BloodType { get; set; } = string.Empty;

    // ── Classification (from Clinical.NeonatalClassifier) ────────────────────
    [Id(22)] public GestationalAgeClassification GestationalAgeClassification { get; set; }
    [Id(23)] public BirthWeightCategory BirthWeightCategory { get; set; }
    [Id(24)] public SizeForGestationalAge SizeForGestationalAge { get; set; }

    // ── Exam, screening, course ──────────────────────────────────────────────
    [Id(25)] public NewbornExam Exam { get; set; } = new();
    [Id(26)] public List<NewbornScreeningEntry> Screenings { get; set; } = new();
    [Id(27)] public List<NewbornMeasurement> Measurements { get; set; } = new();
    [Id(28)] public NurseryLevelOfCare NurseryLevel { get; set; } = NurseryLevelOfCare.WellNewborn;
    [Id(29)] public string NurseryLevelReason { get; set; } = string.Empty;

    // ── Providers / location ─────────────────────────────────────────────────
    [Id(30)] public string AttendingProviderId { get; set; } = string.Empty;
    [Id(31)] public string AttendingProviderName { get; set; } = string.Empty;
    [Id(32)] public string BirthLocationName { get; set; } = string.Empty;

    // ── Status / discharge ───────────────────────────────────────────────────
    [Id(33)] public NewbornStatus Status { get; set; } = NewbornStatus.Admitted;
    [Id(34)] public DateTime? DischargeDateTime { get; set; }
    [Id(35)] public int? DischargeWeightGrams { get; set; }
    [Id(36)] public NewbornFeedingType DischargeFeeding { get; set; }
    [Id(37)] public string DischargeDisposition { get; set; } = string.Empty;
    [Id(38)] public string FollowUpPlan { get; set; } = string.Empty;
    [Id(39)] public bool CarSeatTestPassed { get; set; }
    [Id(40)] public string TransferLocation { get; set; } = string.Empty;

    [Id(41)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    [Id(42)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    // ── NICU depth (Phase 2) ─────────────────────────────────────────────────
    [Id(43)] public List<RespiratorySupportEntry> RespiratorySupport { get; set; } = new();
    [Id(44)] public List<PhototherapyEntry> Phototherapy { get; set; } = new();
    [Id(45)] public List<NeonatalProblemEntry> Problems { get; set; } = new();
    [Id(46)] public List<NeonatalNutritionEntry> Nutrition { get; set; } = new();
    [Id(47)] public List<NeonatalProcedureEntry> Procedures { get; set; } = new();
}

/// <summary>Summary entry for the nursery census/board.</summary>
[GenerateSerializer]
public class NewbornNurseryEntry
{
    [Id(0)] public string NewbornId { get; set; } = string.Empty;
    [Id(1)] public string NewbornName { get; set; } = string.Empty;
    [Id(2)] public string MotherPatientId { get; set; } = string.Empty;
    [Id(3)] public NewbornSex Sex { get; set; }
    [Id(4)] public DateTime BirthDateTime { get; set; }
    [Id(5)] public int GestationalAgeWeeks { get; set; }
    [Id(6)] public int? BirthWeightGrams { get; set; }
    [Id(7)] public NurseryLevelOfCare NurseryLevel { get; set; }
    [Id(8)] public NewbornStatus Status { get; set; }
    [Id(9)] public string AttendingProviderName { get; set; } = string.Empty;
    /// <summary>Universal screens not yet resulted (Pending) — drives the nursery to-do.</summary>
    [Id(10)] public int PendingScreenCount { get; set; }
    /// <summary>True when on respiratory support beyond room air (NICU acuity indicator).</summary>
    [Id(11)] public bool OnRespiratorySupport { get; set; }
    /// <summary>Count of active neonatal problems.</summary>
    [Id(12)] public int ActiveProblemCount { get; set; }
}

/// <summary>Persistent state for the singleton nursery census grain.</summary>
[GenerateSerializer]
public class NewbornNurseryState
{
    [Id(0)] public string SiteId { get; set; } = string.Empty;
    [Id(1)] public List<NewbornNurseryEntry> Entries { get; set; } = new();
    [Id(2)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}
