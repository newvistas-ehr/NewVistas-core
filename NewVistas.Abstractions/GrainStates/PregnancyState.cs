// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

// ── Enums ────────────────────────────────────────────────────────────────────

/// <summary>
/// Lifecycle status of a pregnancy record.
/// Maps to IHS Prenatal Care Module status field (.08).
/// </summary>
[GenerateSerializer]
public enum PregnancyStatus
{
    Active = 0,
    Delivered = 1,
    Miscarriage = 2,
    Stillbirth = 3,
    Ectopic = 4,
    Termination = 5,
    Postpartum = 6,
    Cancelled = 7,
}

/// <summary>
/// Risk level for a pregnancy — IHS Prenatal Care risk stratification.
/// </summary>
[GenerateSerializer]
public enum PregnancyRiskLevel
{
    Low = 0,
    Moderate = 1,
    High = 2,
}

/// <summary>
/// Method of delivery — VistA/RPMS birth registry (BWBR* routines).
/// </summary>
[GenerateSerializer]
public enum DeliveryMethod
{
    Unknown = 0,
    SpontaneousVaginal = 1,
    AssistedVaginal = 2,
    VacuumExtraction = 3,
    Forceps = 4,
    CesareanPrimary = 5,
    CesareanRepeat = 6,
    VaginalBirthAfterCesarean = 7,
}

/// <summary>
/// Pregnancy outcome — RPMS Women's Health (.43) / IHS Prenatal Care Module.
/// </summary>
[GenerateSerializer]
public enum PregnancyOutcome
{
    Ongoing = 0,
    LiveBirth = 1,
    LiveBirthMultiple = 2,
    Miscarriage = 3,
    Stillbirth = 4,
    Ectopic = 5,
    Termination = 6,
    MolarPregnancy = 7,
}

/// <summary>
/// Fetal presentation at delivery or last assessment.
/// </summary>
[GenerateSerializer]
public enum FetalPresentation
{
    Unknown = 0,
    Cephalic = 1,
    Breech = 2,
    Transverse = 3,
    Oblique = 4,
}

/// <summary>
/// Priority of a prenatal problem — IHS Prenatal Care Module field (.06).
/// </summary>
[GenerateSerializer]
public enum PrenatalProblemPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
}

/// <summary>
/// Scope of a prenatal problem — IHS Prenatal Care Module field (.07).
/// </summary>
[GenerateSerializer]
public enum PrenatalProblemScope
{
    CurrentPregnancy = 0,
    AllPregnancies = 1,
}

// ── Nested Entry Types ───────────────────────────────────────────────────────

/// <summary>
/// A prenatal problem/condition tracked during pregnancy.
/// Maps to IHS Prenatal Care Module File #90680.01 (BJPN PRENATAL PROBLEMS).
/// </summary>
[GenerateSerializer]
public class PrenatalProblemEntry
{
    /// <summary>Unique problem identifier.</summary>
    [Id(0)]
    public string ProblemId { get; set; } = string.Empty;

    /// <summary>SNOMED or descriptive term (.03) — from File #90680.02.</summary>
    [Id(1)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Priority (.06) — Low, Medium, High.</summary>
    [Id(2)]
    public PrenatalProblemPriority Priority { get; set; }

    /// <summary>Scope (.07) — Current Pregnancy or All Pregnancies.</summary>
    [Id(3)]
    public PrenatalProblemScope Scope { get; set; }

    /// <summary>Active or Inactive (.08).</summary>
    [Id(4)]
    public bool IsActive { get; set; } = true;

    /// <summary>Provider notes (.05).</summary>
    [Id(5)]
    public string? Notes { get; set; }

    /// <summary>Date problem was entered.</summary>
    [Id(6)]
    public DateTime EntryDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Delivery information recorded at birth — RPMS birth registry (BWBR* routines).
/// </summary>
[GenerateSerializer]
public class DeliveryInfo
{
    /// <summary>Date and time of delivery.</summary>
    [Id(0)]
    public DateTime? DeliveryDate { get; set; }

    /// <summary>Method of delivery.</summary>
    [Id(1)]
    public DeliveryMethod DeliveryMethod { get; set; }

    /// <summary>Gestational age at delivery in weeks.</summary>
    [Id(2)]
    public int? GestationalAgeAtDeliveryWeeks { get; set; }

    /// <summary>Birth weight in grams.</summary>
    [Id(3)]
    public int? BirthWeightGrams { get; set; }

    /// <summary>Apgar score at 1 minute.</summary>
    [Id(4)]
    public int? Apgar1Min { get; set; }

    /// <summary>Apgar score at 5 minutes.</summary>
    [Id(5)]
    public int? Apgar5Min { get; set; }

    /// <summary>Fetal presentation at delivery.</summary>
    [Id(6)]
    public FetalPresentation Presentation { get; set; }

    /// <summary>Type of anesthesia used (e.g., Epidural, Spinal, General, None).</summary>
    [Id(7)]
    public string? AnesthesiaType { get; set; }

    /// <summary>Perineal status (e.g., Intact, 1st Degree, 2nd Degree, 3rd Degree, 4th Degree, Episiotomy).</summary>
    [Id(8)]
    public string? PerinealStatus { get; set; }

    /// <summary>Estimated blood loss in mL.</summary>
    [Id(9)]
    public int? EstimatedBloodLossMl { get; set; }

    /// <summary>Placenta delivery: spontaneous or manual.</summary>
    [Id(10)]
    public string? PlacentaDelivery { get; set; }

    /// <summary>Infant sex (M/F/Unknown).</summary>
    [Id(11)]
    public string? InfantSex { get; set; }

    /// <summary>Complications noted during delivery.</summary>
    [Id(12)]
    public string? Complications { get; set; }

    /// <summary>Delivery notes / narrative.</summary>
    [Id(13)]
    public string? Notes { get; set; }
}

/// <summary>
/// Postpartum follow-up information — RPMS postpartum care tracking.
/// </summary>
[GenerateSerializer]
public class PostpartumInfo
{
    /// <summary>Date of postpartum visit.</summary>
    [Id(0)]
    public DateTime? PostpartumVisitDate { get; set; }

    /// <summary>Breastfeeding status (e.g., Exclusive, Partial, None).</summary>
    [Id(1)]
    public string? BreastfeedingStatus { get; set; }

    /// <summary>Contraceptive method selected postpartum.</summary>
    [Id(2)]
    public string? ContraceptiveMethod { get; set; }

    /// <summary>Postpartum depression screening result.</summary>
    [Id(3)]
    public string? DepressionScreeningResult { get; set; }

    /// <summary>Edinburgh Postnatal Depression Scale (EPDS) score (0-30).</summary>
    [Id(4)]
    public int? EpdsScore { get; set; }

    /// <summary>Postpartum complications.</summary>
    [Id(5)]
    public string? Complications { get; set; }

    /// <summary>Postpartum notes.</summary>
    [Id(6)]
    public string? Notes { get; set; }
}

// ── Main State Classes ───────────────────────────────────────────────────────

/// <summary>
/// Persistent state for a Pregnancy grain (OB-PREG:{id}).
/// Combines IHS Prenatal Care Module (File #90680.01) and RPMS Women's Health
/// pregnancy tracking (BJPNAPI.m, BWGRVL.m — gravity/parity routines).
/// </summary>
[GenerateSerializer]
public class PregnancyState
{
    /// <summary>Unique grain key (OB-PREG:{guid}).</summary>
    [Id(0)]
    public string PregnancyId { get; set; } = string.Empty;

    /// <summary>Patient identifier — links to VistA PATIENT file #2.</summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Pregnancy status — Active, Delivered, Miscarriage, etc.</summary>
    [Id(2)]
    public PregnancyStatus Status { get; set; }

    // ── Dates & EDD ─────────────────────────────────────────────────────────

    /// <summary>Last menstrual period date — primary basis for EDD calculation.</summary>
    [Id(3)]
    public DateTime? LastMenstrualPeriod { get; set; }

    /// <summary>EDD calculated from LMP.</summary>
    [Id(4)]
    public DateTime? EddByLmp { get; set; }

    /// <summary>EDD determined by ultrasound dating.</summary>
    [Id(5)]
    public DateTime? EddByUltrasound { get; set; }

    /// <summary>Definitive EDD used for clinical management — IHS field (.09).</summary>
    [Id(6)]
    public DateTime DefinitiveEdd { get; set; }

    // ── Gravida / Para / Abortions / Living (GPAL) — BWGRVL.m ──────────────

    /// <summary>Gravida — total number of pregnancies including this one.</summary>
    [Id(7)]
    public int Gravida { get; set; }

    /// <summary>Para — number of deliveries reaching viability.</summary>
    [Id(8)]
    public int Para { get; set; }

    /// <summary>Number of abortions (spontaneous + elective).</summary>
    [Id(9)]
    public int Abortions { get; set; }

    /// <summary>Number of living children.</summary>
    [Id(10)]
    public int Living { get; set; }

    // ── Risk Assessment ─────────────────────────────────────────────────────

    /// <summary>Overall risk level for this pregnancy.</summary>
    [Id(11)]
    public PregnancyRiskLevel RiskLevel { get; set; }

    /// <summary>Risk factors — from IHS pick lists 4 and 5 (medical & fetal conditions).</summary>
    [Id(12)]
    public List<string> RiskFactors { get; set; } = new();

    // ── Problems ────────────────────────────────────────────────────────────

    /// <summary>Prenatal problems tracked during this pregnancy — File #90680.01.</summary>
    [Id(13)]
    public List<PrenatalProblemEntry> Problems { get; set; } = new();

    // ── Provider / Location ─────────────────────────────────────────────────

    /// <summary>Primary OB provider identifier.</summary>
    [Id(14)]
    public string? ProviderId { get; set; }

    /// <summary>Primary OB provider name.</summary>
    [Id(15)]
    public string? ProviderName { get; set; }

    /// <summary>Clinic / location identifier.</summary>
    [Id(16)]
    public string? LocationId { get; set; }

    /// <summary>Clinic / location name.</summary>
    [Id(17)]
    public string? LocationName { get; set; }

    // ── Delivery & Postpartum ───────────────────────────────────────────────

    /// <summary>Pregnancy outcome.</summary>
    [Id(18)]
    public PregnancyOutcome Outcome { get; set; }

    /// <summary>Delivery details — populated when pregnancy reaches delivery.</summary>
    [Id(19)]
    public DeliveryInfo? Delivery { get; set; }

    /// <summary>Postpartum follow-up — populated after delivery.</summary>
    [Id(20)]
    public PostpartumInfo? Postpartum { get; set; }

    // ── Notes & Audit ───────────────────────────────────────────────────────

    /// <summary>General pregnancy notes.</summary>
    [Id(21)]
    public string? Notes { get; set; }

    /// <summary>Date pregnancy record was created.</summary>
    [Id(22)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date pregnancy record was last modified.</summary>
    [Id(23)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

// ── Prenatal Visit State ─────────────────────────────────────────────────────

/// <summary>
/// Persistent state for a Prenatal Visit grain (OB-VISIT:{id}).
/// Maps to IHS Prenatal Care Module V OB file (9000010.43) —
/// dynamic problem/vital tracking per encounter.
/// </summary>
[GenerateSerializer]
public class PrenatalVisitState
{
    /// <summary>Unique grain key (OB-VISIT:{guid}).</summary>
    [Id(0)]
    public string VisitId { get; set; } = string.Empty;

    /// <summary>Parent pregnancy grain key (OB-PREG:{guid}).</summary>
    [Id(1)]
    public string PregnancyId { get; set; } = string.Empty;

    /// <summary>Patient identifier.</summary>
    [Id(2)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>Date / time of the prenatal visit.</summary>
    [Id(3)]
    public DateTime VisitDate { get; set; }

    // ── Gestational Age ─────────────────────────────────────────────────────

    /// <summary>Gestational age in completed weeks at time of visit.</summary>
    [Id(4)]
    public int GestationalAgeWeeks { get; set; }

    /// <summary>Additional days beyond completed weeks.</summary>
    [Id(5)]
    public int GestationalAgeDays { get; set; }

    // ── Maternal Vitals ─────────────────────────────────────────────────────

    /// <summary>Maternal weight in pounds.</summary>
    [Id(6)]
    public decimal? Weight { get; set; }

    /// <summary>Systolic blood pressure (mmHg).</summary>
    [Id(7)]
    public int? BloodPressureSystolic { get; set; }

    /// <summary>Diastolic blood pressure (mmHg).</summary>
    [Id(8)]
    public int? BloodPressureDiastolic { get; set; }

    // ── Obstetric Exam ──────────────────────────────────────────────────────

    /// <summary>Fundal height in centimeters.</summary>
    [Id(9)]
    public decimal? FundalHeightCm { get; set; }

    /// <summary>Fetal heart rate in beats per minute.</summary>
    [Id(10)]
    public int? FetalHeartRate { get; set; }

    /// <summary>Fetal presentation at this visit.</summary>
    [Id(11)]
    public FetalPresentation FetalPresentation { get; set; }

    /// <summary>Fetal movement reported by patient (true = present).</summary>
    [Id(12)]
    public bool? FetalMovement { get; set; }

    // ── Dipstick / Screening ────────────────────────────────────────────────

    /// <summary>Urine protein result (e.g., Negative, Trace, 1+, 2+, 3+, 4+).</summary>
    [Id(13)]
    public string? UrineProtein { get; set; }

    /// <summary>Urine glucose result (e.g., Negative, Trace, 1+, 2+, 3+, 4+).</summary>
    [Id(14)]
    public string? UrineGlucose { get; set; }

    /// <summary>Edema status (e.g., None, Trace, 1+, 2+, 3+, 4+).</summary>
    [Id(15)]
    public string? Edema { get; set; }

    // ── Cervical Exam ───────────────────────────────────────────────────────

    /// <summary>Cervical dilation in centimeters (0-10).</summary>
    [Id(16)]
    public decimal? CervicalDilationCm { get; set; }

    /// <summary>Cervical effacement as percentage (0-100).</summary>
    [Id(17)]
    public int? CervicalEffacementPercent { get; set; }

    /// <summary>Fetal station (-5 to +5).</summary>
    [Id(18)]
    public int? FetalStation { get; set; }

    // ── Provider / Notes ────────────────────────────────────────────────────

    /// <summary>Provider identifier for this visit.</summary>
    [Id(19)]
    public string? ProviderId { get; set; }

    /// <summary>Provider name for this visit.</summary>
    [Id(20)]
    public string? ProviderName { get; set; }

    /// <summary>Clinical notes for this visit.</summary>
    [Id(21)]
    public string? Notes { get; set; }

    /// <summary>Next scheduled visit date.</summary>
    [Id(22)]
    public DateTime? NextVisitDate { get; set; }

    // ── Audit ───────────────────────────────────────────────────────────────

    /// <summary>Date visit record was created.</summary>
    [Id(23)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date visit record was last modified.</summary>
    [Id(24)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

// ── Index Entries ────────────────────────────────────────────────────────────

/// <summary>
/// Lightweight summary of a pregnancy for the per-patient index.
/// </summary>
[GenerateSerializer]
public class PregnancyIndexEntry
{
    [Id(0)]
    public string PregnancyId { get; set; } = string.Empty;

    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    [Id(2)]
    public PregnancyStatus Status { get; set; }

    [Id(3)]
    public DateTime DefinitiveEdd { get; set; }

    [Id(4)]
    public int Gravida { get; set; }

    [Id(5)]
    public int Para { get; set; }

    [Id(6)]
    public PregnancyRiskLevel RiskLevel { get; set; }

    [Id(7)]
    public PregnancyOutcome Outcome { get; set; }

    [Id(8)]
    public string? ProviderName { get; set; }

    [Id(9)]
    public DateTime CreatedDate { get; set; }
}

/// <summary>
/// State for the per-patient pregnancy index grain (OB-PREG-IDX:{patientId}).
/// </summary>
[GenerateSerializer]
public class PregnancyIndexState
{
    /// <summary>Ordered list of pregnancy summaries (newest first).</summary>
    [Id(0)]
    public List<PregnancyIndexEntry> Entries { get; set; } = new();
}

/// <summary>
/// Lightweight summary of a prenatal visit for the per-pregnancy index.
/// </summary>
[GenerateSerializer]
public class PrenatalVisitIndexEntry
{
    [Id(0)]
    public string VisitId { get; set; } = string.Empty;

    [Id(1)]
    public string PregnancyId { get; set; } = string.Empty;

    [Id(2)]
    public DateTime VisitDate { get; set; }

    [Id(3)]
    public int GestationalAgeWeeks { get; set; }

    [Id(4)]
    public int GestationalAgeDays { get; set; }

    [Id(5)]
    public int? FetalHeartRate { get; set; }

    [Id(6)]
    public decimal? FundalHeightCm { get; set; }

    [Id(7)]
    public decimal? Weight { get; set; }

    [Id(8)]
    public string? ProviderName { get; set; }
}

/// <summary>
/// State for the per-pregnancy prenatal visit index grain (OB-VISIT-IDX:{pregnancyId}).
/// </summary>
[GenerateSerializer]
public class PrenatalVisitIndexState
{
    /// <summary>Ordered list of visit summaries (newest first).</summary>
    [Id(0)]
    public List<PrenatalVisitIndexEntry> Entries { get; set; } = new();
}
