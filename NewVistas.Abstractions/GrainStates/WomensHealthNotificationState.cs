// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// Lifecycle status of a Women's Health notification record.
/// </summary>
[GenerateSerializer]
public enum WomensHealthNotificationStatus
{
    Active = 0,
    Completed = 1,
    Cancelled = 2,
    FollowUpRequired = 3,
}

/// <summary>
/// Type of Women's Health clinical notification — VistA File #790.
/// Governs which result fields are relevant for the record.
/// </summary>
[GenerateSerializer]
public enum WomensHealthNotificationType
{
    Mammography = 0,
    PapSmear = 1,
    Contraception = 2,
    Pregnancy = 3,
    BreastHealth = 4,
    MenopauseHormoneTherapy = 5,
    FollowUp = 6,
    Other = 7,
}

/// <summary>
/// Mammography / imaging result — ACR BI-RADS assessment categories 0–6.
/// Applicable when NotificationType is Mammography or BreastHealth.
/// </summary>
[GenerateSerializer]
public enum MammographyResult
{
    Unknown = 0,
    Incomplete = 1,              // BI-RADS 0 — needs additional imaging
    Normal = 2,                  // BI-RADS 1 — negative
    BenignFinding = 3,           // BI-RADS 2 — benign
    ProbablyBenign = 4,          // BI-RADS 3 — short-interval follow-up
    SuspiciousAbnormality = 5,   // BI-RADS 4 — biopsy should be considered
    HighlySuspicious = 6,        // BI-RADS 5 — biopsy recommended
    MalignancyKnown = 7,         // BI-RADS 6 — known biopsy-proven malignancy
}

/// <summary>
/// Pap smear / cervical cytology result — Bethesda System terminology.
/// Applicable when NotificationType is PapSmear.
/// </summary>
[GenerateSerializer]
public enum PapSmearResult
{
    Unknown = 0,
    Satisfactory = 1,
    Unsatisfactory = 2,
    Normal = 3,
    Ascus = 4,                  // Atypical Squamous Cells of Undetermined Significance
    AscH = 5,                   // Atypical Squamous Cells, cannot exclude HSIL
    Lsil = 6,                   // Low-Grade Squamous Intraepithelial Lesion
    Hsil = 7,                   // High-Grade Squamous Intraepithelial Lesion
    AdenocarcinomaInSitu = 8,
    Malignant = 9,
}

/// <summary>
/// Persistent state for a Women's Health notification grain.
/// VistA File #790 — WOMEN'S HEALTH (WH package — WOCT.m, WOCPAT.m).
/// </summary>
[GenerateSerializer]
public class WomensHealthNotificationState
{
    /// <summary>
    /// Unique grain key (WH-NOTE:{guid}).
    /// </summary>
    [Id(0)]
    public string NotificationId { get; set; } = string.Empty;

    /// <summary>
    /// Patient identifier (.01). Links to VistA PATIENT file #2.
    /// </summary>
    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    /// <summary>
    /// Type of clinical notification (.02). Governs which result fields are relevant.
    /// </summary>
    [Id(2)]
    public WomensHealthNotificationType NotificationType { get; set; }

    /// <summary>
    /// Date/time of procedure or encounter (.03).
    /// </summary>
    [Id(3)]
    public DateTime ProcedureDate { get; set; }

    /// <summary>
    /// Lifecycle status (.04) — Active, Completed, Cancelled, FollowUpRequired.
    /// </summary>
    [Id(4)]
    public WomensHealthNotificationStatus Status { get; set; }

    // ── Provider / Location ──────────────────────────────────────────────────

    /// <summary>
    /// Ordering/performing provider DUZ (.05).
    /// </summary>
    [Id(5)]
    public string? ProviderId { get; set; }

    /// <summary>
    /// Provider name (.06).
    /// </summary>
    [Id(6)]
    public string? ProviderName { get; set; }

    /// <summary>
    /// Clinic/location identifier (.07).
    /// </summary>
    [Id(7)]
    public string? LocationId { get; set; }

    /// <summary>
    /// Clinic/location name (.08).
    /// </summary>
    [Id(8)]
    public string? LocationName { get; set; }

    // ── Mammography / Breast Imaging Fields (#790.1) ─────────────────────────

    /// <summary>
    /// Mammography result (.11) — ACR BI-RADS category.
    /// Only applicable when NotificationType is Mammography or BreastHealth.
    /// </summary>
    [Id(9)]
    public MammographyResult? MammographyResult { get; set; }

    /// <summary>
    /// Numeric ACR BI-RADS score (.12) — 0 to 6.
    /// </summary>
    [Id(10)]
    public int? BiRadsScore { get; set; }

    // ── Pap Smear / Cervical Health Fields (#790.2) ──────────────────────────

    /// <summary>
    /// Pap smear cytology result (.21) — Bethesda System.
    /// Only applicable when NotificationType is PapSmear.
    /// </summary>
    [Id(11)]
    public PapSmearResult? PapSmearResult { get; set; }

    // ── Contraception Fields (#790.3) ────────────────────────────────────────

    /// <summary>
    /// Contraceptive method documented (.31).
    /// Examples: IUD, Oral Contraceptives, Barrier, Implant, Sterilization, Natural Family Planning, None.
    /// </summary>
    [Id(12)]
    public string? ContraceptiveMethod { get; set; }

    // ── Pregnancy / OB Fields (#790.4) ───────────────────────────────────────

    /// <summary>
    /// Gestational age in weeks at time of encounter (.41).
    /// Only applicable when NotificationType is Pregnancy.
    /// </summary>
    [Id(13)]
    public int? GestationalAgeWeeks { get; set; }

    /// <summary>
    /// Estimated due date (EDD) (.42).
    /// </summary>
    [Id(14)]
    public DateTime? EstimatedDueDate { get; set; }

    /// <summary>
    /// Pregnancy outcome (.43).
    /// Examples: LIVE BIRTH, MISCARRIAGE, STILLBIRTH, TERMINATION, ONGOING.
    /// </summary>
    [Id(15)]
    public string? PregnancyOutcome { get; set; }

    // ── Follow-Up ────────────────────────────────────────────────────────────

    /// <summary>
    /// Follow-up required flag (.51). True when results require action or repeat screening.
    /// </summary>
    [Id(16)]
    public bool FollowUpRequired { get; set; }

    /// <summary>
    /// Next scheduled screening or follow-up date (.52).
    /// </summary>
    [Id(17)]
    public DateTime? NextDueDate { get; set; }

    /// <summary>
    /// Date follow-up was completed (.53).
    /// </summary>
    [Id(18)]
    public DateTime? FollowUpCompletedDate { get; set; }

    // ── Refusal & Notes ──────────────────────────────────────────────────────

    /// <summary>
    /// Patient refused the screening or treatment (.61).
    /// </summary>
    [Id(19)]
    public bool IsRefusal { get; set; }

    /// <summary>
    /// Clinical notes / free-text findings (.62).
    /// </summary>
    [Id(20)]
    public string? Notes { get; set; }

    // ── Audit ────────────────────────────────────────────────────────────────

    [Id(21)]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [Id(22)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Summary entry stored in the per-patient Women's Health index grain.
/// </summary>
[GenerateSerializer]
public class WomensHealthIndexEntry
{
    [Id(0)]
    public string NotificationId { get; set; } = string.Empty;

    [Id(1)]
    public string PatientId { get; set; } = string.Empty;

    [Id(2)]
    public WomensHealthNotificationType NotificationType { get; set; }

    [Id(3)]
    public DateTime ProcedureDate { get; set; }

    [Id(4)]
    public WomensHealthNotificationStatus Status { get; set; }

    [Id(5)]
    public string? ProviderName { get; set; }

    [Id(6)]
    public bool FollowUpRequired { get; set; }

    [Id(7)]
    public DateTime? NextDueDate { get; set; }
}

/// <summary>
/// State for the per-patient Women's Health index grain (WH-IDX:{patientId}).
/// </summary>
[GenerateSerializer]
public class WomensHealthIndexState
{
    /// <summary>
    /// Ordered list of notification summaries (newest first).
    /// </summary>
    [Id(0)]
    public List<WomensHealthIndexEntry> Entries { get; set; } = new();
}
