// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.GrainStates;

// ── Specialty cover sheet (PROTOTYPE) — the cover sheet as a composition of sections ──
// A layout declares which sections appear, in what order, and how prominently; the assembler reads
// ONLY those sections (+ a non-suppressible safety spine). See Docs/Domain/SPECIALTY_COVERSHEET_PROTOTYPE.md.

/// <summary>Well-known section keys a layout can declare.</summary>
public static class CoverSheetSections
{
    public const string Oncology = "oncology";
    public const string Procedures = "procedures";
    public const string Imaging = "imaging";
    public const string Pgx = "pgx";
    public const string Problems = "problems";
    public const string Medications = "medications";
    public const string Reminders = "reminders";
    public const string Labs = "labs";
    public const string Vitals = "vitals";
    public const string Visits = "visits";
    public const string Orders = "orders";
    public const string Consults = "consults";
}

/// <summary>One section a layout asks for — key + display title + prominence + item cap.</summary>
[GenerateSerializer]
public class CoverSheetSectionSpec
{
    [Id(0)] public string SectionKey { get; set; } = string.Empty;
    [Id(1)] public string Title { get; set; } = string.Empty;
    /// <summary>Rendered larger / first — the sections this specialty leads with.</summary>
    [Id(2)] public bool Prominent { get; set; }
    /// <summary>Max items to show in the overview (full history is a separate drill-down).</summary>
    [Id(3)] public int MaxItems { get; set; } = 5;
}

/// <summary>A compact oncology card for the cover sheet — tumor + staging + treatment + precision matches.</summary>
[GenerateSerializer]
public class OncologyTumorCard
{
    [Id(0)] public string TumorId { get; set; } = string.Empty;
    [Id(1)] public string PrimarySite { get; set; } = string.Empty;
    [Id(2)] public string Histology { get; set; } = string.Empty;
    [Id(3)] public string? StageGroup { get; set; }
    [Id(4)] public string Status { get; set; } = string.Empty;
    [Id(5)] public string? OncologistName { get; set; }
    [Id(6)] public DateTime DateOfDiagnosis { get; set; }
    /// <summary>Current/most-recent treatment, e.g. "Immunotherapy — pembrolizumab (Active)".</summary>
    [Id(7)] public string CurrentTreatment { get; set; } = string.Empty;
    /// <summary>Molecular profile, e.g. "BRAF: V600E (Positive)".</summary>
    [Id(8)] public List<string> Biomarkers { get; set; } = new();
    /// <summary>Precision-oncology therapy matches, e.g. "BRAF → BRAF + MEK inhibitor".</summary>
    [Id(9)] public List<string> PrecisionMatches { get; set; } = new();
}

/// <summary>A compact imaging card for the cover sheet — the study + its impression.</summary>
[GenerateSerializer]
public class ImagingCard
{
    [Id(0)] public string ProcedureName { get; set; } = string.Empty;
    [Id(1)] public string? ImagingType { get; set; }
    [Id(2)] public DateTime? ExamDateTime { get; set; }
    [Id(3)] public string? RequestingProviderName { get; set; }
    [Id(4)] public string? InterpretingPhysicianName { get; set; }
    [Id(5)] public string? Impression { get; set; }
    [Id(6)] public string Status { get; set; } = string.Empty;
}

/// <summary>
/// A composed, specialty-shaped cover sheet. Only the sections named in <see cref="Sections"/> are
/// populated; the rest stay empty (not read). Demographics/CWAD/Allergies are the always-present spine.
/// </summary>
[GenerateSerializer]
public class SpecialtyCoverSheet
{
    [Id(0)] public string PatientId { get; set; } = string.Empty;
    [Id(1)] public string LayoutId { get; set; } = string.Empty;
    [Id(2)] public string LayoutName { get; set; } = string.Empty;
    /// <summary>Why this layout was chosen (for the auto path).</summary>
    [Id(3)] public string LayoutReason { get; set; } = string.Empty;
    /// <summary>Optional "loudest problem" caution (e.g. active cancer on a procedural view).</summary>
    [Id(4)] public string ContextBanner { get; set; } = string.Empty;
    /// <summary>Ordered sections to render (drives the UI; excludes the spine).</summary>
    [Id(5)] public List<CoverSheetSectionSpec> Sections { get; set; } = new();

    // ── Safety spine (always populated) ──
    [Id(6)] public PatientDemographicsSummary Demographics { get; set; } = new();
    [Id(7)] public CwadFlags Cwad { get; set; } = new();
    [Id(8)] public List<AllergySummary> Allergies { get; set; } = new();

    // ── Section payloads (populated only when the layout declares them) ──
    [Id(9)] public List<ProblemSummary> ActiveProblems { get; set; } = new();
    [Id(10)] public List<MedicationSummary> ActiveMedications { get; set; } = new();
    [Id(11)] public List<ReminderSummary> ClinicalReminders { get; set; } = new();
    [Id(12)] public List<LabResultSummary> RecentLabs { get; set; } = new();
    [Id(13)] public List<VitalSummary> RecentVitals { get; set; } = new();
    [Id(14)] public List<VisitSummary> RecentVisits { get; set; } = new();
    [Id(15)] public List<OrderSummary> ActiveOrders { get; set; } = new();
    [Id(16)] public List<ConsultSummary> ActiveConsults { get; set; } = new();
    [Id(17)] public List<OncologyTumorCard> OncologyTumors { get; set; } = new();
    [Id(18)] public List<SurgerySummary> UpcomingProcedures { get; set; } = new();
    [Id(19)] public List<ImagingCard> LatestImaging { get; set; } = new();
    /// <summary>Actionable pharmacogenomic alerts, e.g. "fluorouracil — AdjustDose (DPYD Intermediate metabolizer)".</summary>
    [Id(20)] public List<string> PgxAlerts { get; set; } = new();

    [Id(21)] public DateTime LastRefreshed { get; set; }
    /// <summary>How many section loaders actually ran (demonstrates selective fan-out).</summary>
    [Id(22)] public int SectionsLoaded { get; set; }

    /// <summary>
    /// Emerging-condition precaution banners — part of the always-present safety spine (never
    /// suppressed by the layout). One per confirmed proto-condition membership.
    /// </summary>
    [Id(23)] public List<PrecautionBanner> PrecautionBanners { get; set; } = new();
}
