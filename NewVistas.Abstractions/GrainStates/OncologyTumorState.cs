// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>Oncology status — tracks the current disease state of a registered tumor.</summary>
[GenerateSerializer]
public enum OncologyStatus
{
    Active = 0,
    InRemission = 1,
    Recurrence = 2,
    Deceased = 3,
    LostToFollowUp = 4,
    Unknown = 5
}

/// <summary>Tumor laterality (NAACCR Item #410).</summary>
[GenerateSerializer]
public enum TumorLaterality
{
    NotApplicable = 0,
    Right = 1,
    Left = 2,
    Bilateral = 3,
    Unknown = 4
}

/// <summary>Basis of diagnosis (NAACCR Item #490).</summary>
[GenerateSerializer]
public enum DiagnosisBasis
{
    Unknown = 0,
    ClinicalOnly = 1,
    ClinicalInvestigation = 2,
    LabMarkerOnly = 3,
    Cytology = 4,
    HistologyOfMetastasis = 5,
    HistologyOfPrimary = 6,
    Autopsy = 7
}

/// <summary>
/// State for an individual tumor registry entry.
/// Maps to VistA Oncology files #160–#165 (ONC PRIMARY, ONC STAGING).
/// MUMPS routines: ONCRP.m, ONCS.m, ONCTREAT.m
/// </summary>
[GenerateSerializer]
public class OncologyTumorState
{
    /// <summary>Unique tumor identifier (grain key). (.01)</summary>
    [Id(0)] public string TumorId { get; set; } = string.Empty;

    /// <summary>Owning patient identifier. (.02)</summary>
    [Id(1)] public string PatientId { get; set; } = string.Empty;

    /// <summary>Primary site of cancer (ICD-O-3 topography code, e.g. "C34.1"). (.03)</summary>
    [Id(2)] public string PrimarySite { get; set; } = string.Empty;

    /// <summary>Primary site descriptive text. (.04)</summary>
    [Id(3)] public string PrimarySiteText { get; set; } = string.Empty;

    /// <summary>Histology/morphology code (ICD-O-3, e.g. "8140/3"). (.05)</summary>
    [Id(4)] public string Histology { get; set; } = string.Empty;

    /// <summary>Histology descriptive text (e.g. "Adenocarcinoma, NOS"). (.06)</summary>
    [Id(5)] public string HistologyText { get; set; } = string.Empty;

    /// <summary>Tumor laterality. (.07)</summary>
    [Id(6)] public TumorLaterality Laterality { get; set; } = TumorLaterality.NotApplicable;

    /// <summary>Date of initial diagnosis. (.08)</summary>
    [Id(7)] public DateTime DateOfDiagnosis { get; set; }

    /// <summary>Basis/method by which diagnosis was established. (.09)</summary>
    [Id(8)] public DiagnosisBasis DiagnosisBasis { get; set; } = DiagnosisBasis.Unknown;

    /// <summary>Sequence number for multiple primaries (1 = first, 2 = second, etc.). (.10)</summary>
    [Id(9)] public int SequenceNumber { get; set; } = 1;

    /// <summary>Responsible oncologist identifier. (.11)</summary>
    [Id(10)] public string? OncologistId { get; set; }

    /// <summary>Responsible oncologist name. (.12)</summary>
    [Id(11)] public string? OncologistName { get; set; }

    // ─── Staging (TNM 8th Edition) ─────────────────────────────────────

    /// <summary>Clinical T category (cT). (.20)</summary>
    [Id(12)] public string? ClinicalT { get; set; }

    /// <summary>Clinical N category (cN). (.21)</summary>
    [Id(13)] public string? ClinicalN { get; set; }

    /// <summary>Clinical M category (cM). (.22)</summary>
    [Id(14)] public string? ClinicalM { get; set; }

    /// <summary>Pathologic T category (pT). (.23)</summary>
    [Id(15)] public string? PathologicT { get; set; }

    /// <summary>Pathologic N category (pN). (.24)</summary>
    [Id(16)] public string? PathologicN { get; set; }

    /// <summary>Pathologic M category (pM). (.25)</summary>
    [Id(17)] public string? PathologicM { get; set; }

    /// <summary>Overall stage group (e.g. "IIA", "IIIB", "IV"). (.26)</summary>
    [Id(18)] public string? StageGroup { get; set; }

    /// <summary>SEER Summary Stage 2018 code (0–9). (.27)</summary>
    [Id(19)] public string? SeerSummaryStage { get; set; }

    /// <summary>Date staging was recorded. (.28)</summary>
    [Id(20)] public DateTime? StagingDate { get; set; }

    // ─── Follow-up ──────────────────────────────────────────────────────

    /// <summary>Current disease status. (.30)</summary>
    [Id(21)] public OncologyStatus Status { get; set; } = OncologyStatus.Active;

    /// <summary>Date of disease status change (remission, recurrence, etc.). (.31)</summary>
    [Id(22)] public DateTime? StatusChangeDate { get; set; }

    /// <summary>Date of recurrence (if applicable). (.32)</summary>
    [Id(23)] public DateTime? RecurrenceDate { get; set; }

    /// <summary>Site of recurrence. (.33)</summary>
    [Id(24)] public string? RecurrenceSite { get; set; }

    /// <summary>Date of last patient contact. (.34)</summary>
    [Id(25)] public DateTime? DateOfLastContact { get; set; }

    /// <summary>IDs of associated treatment records. (.40)</summary>
    [Id(26)] public List<string> TreatmentIds { get; set; } = new();

    /// <summary>Free-text comments. (.50)</summary>
    [Id(27)] public string? Comments { get; set; }

    /// <summary>Date the tumor record was created. (.90)</summary>
    [Id(28)] public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>Date the tumor record was last modified. (.91)</summary>
    [Id(29)] public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

/// <summary>Summary entry stored in the per-patient tumor index.</summary>
[GenerateSerializer]
public class OncologyTumorIndexEntry
{
    [Id(0)] public string TumorId { get; set; } = string.Empty;
    [Id(1)] public string PrimarySite { get; set; } = string.Empty;
    [Id(2)] public string PrimarySiteText { get; set; } = string.Empty;
    [Id(3)] public string Histology { get; set; } = string.Empty;
    [Id(4)] public string HistologyText { get; set; } = string.Empty;
    [Id(5)] public DateTime DateOfDiagnosis { get; set; }
    [Id(6)] public OncologyStatus Status { get; set; }
    [Id(7)] public string? StageGroup { get; set; }
    [Id(8)] public int SequenceNumber { get; set; }
    [Id(9)] public string? OncologistName { get; set; }
}
