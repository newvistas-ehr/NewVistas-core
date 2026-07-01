// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// PROTOTYPE: the cover sheet as a composition. A layout (General / Oncology / Procedural) declares an
/// ordered set of sections; this assembler reads ONLY those (+ a non-suppressible demographics/CWAD/
/// allergies spine), in parallel. Auto-resolves a default layout from patient context (active cancer →
/// Oncology, else upcoming surgery → Procedural, else General). Read-only; does not touch the legacy
/// GetCoverSheetAsync. See Docs/Domain/SPECIALTY_COVERSHEET_PROTOTYPE.md.
/// </summary>
public partial class PatientWorkflowGrain
{
    public async Task<SpecialtyCoverSheet> GetSpecialtyCoverSheetAsync(string? layoutId, string? viewerRole)
    {
        var patientGrain = GetPatientGrain();
        PatientState patientState = await patientGrain.GetPatientAsync();

        // Context reads — needed to resolve the auto default + the "loudest problem" banner.
        // (Prototype: always read; production would resolve from a cheap patient-context flag.)
        var tumorsTask = GetOncologyTumorsAsync();
        var surgeriesTask = GetSurgeriesAsync(10);
        await Task.WhenAll(tumorsTask, surgeriesTask);
        List<OncologyTumorIndexEntry> tumors = tumorsTask.Result;
        List<SurgerySummary> upcoming = surgeriesTask.Result
            .Where(s => s.DateOfOperation.Date >= DateTime.UtcNow.Date
                && !string.Equals(s.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(s.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.DateOfOperation)
            .ToList();

        bool hasActiveCancer = tumors.Count > 0;
        bool hasUpcomingSurgery = upcoming.Count > 0;

        bool auto = string.IsNullOrWhiteSpace(layoutId) || string.Equals(layoutId, "auto", StringComparison.OrdinalIgnoreCase);
        string autoReason = string.Empty;
        CoverSheetLayout layout;
        if (auto)
        {
            (string autoId, autoReason) = CoverSheetLayouts.ResolveDefault(hasActiveCancer, hasUpcomingSurgery, viewerRole);
            layout = CoverSheetLayouts.Resolve(autoId);
        }
        else
        {
            layout = CoverSheetLayouts.Resolve(layoutId);
        }

        var keys = new HashSet<string>(layout.Sections.Select(s => s.SectionKey), StringComparer.OrdinalIgnoreCase);
        int MaxOf(string key) => layout.Sections.FirstOrDefault(s => s.SectionKey == key)?.MaxItems ?? 5;

        // ── Fire only the declared section loaders (+ the always-on allergy spine), in parallel ──
        var allergiesTask = GetAllergiesAsync();

        Task<List<ProblemSummary>>? problemsTask = keys.Contains(CoverSheetSections.Problems) ? GetActiveProblemsAsync() : null;
        Task<List<MedicationSummary>>? medsTask = keys.Contains(CoverSheetSections.Medications) ? GetActiveMedicationsAsync() : null;
        Task<List<ReminderSummary>>? remindersTask = keys.Contains(CoverSheetSections.Reminders) ? LoadRemindersAsync(patientGrain) : null;
        Task<List<LabResultSummary>>? labsTask = keys.Contains(CoverSheetSections.Labs) ? LoadRecentLabsAsync(patientGrain) : null;
        Task<List<VitalSummary>>? vitalsTask = keys.Contains(CoverSheetSections.Vitals) ? GetLatestVitalsAsync() : null;
        var visitsTask = keys.Contains(CoverSheetSections.Visits) ? GetAllAppointmentsAsync(20) : null;
        Task<List<OrderSummary>>? ordersTask = keys.Contains(CoverSheetSections.Orders) ? GetOrdersByFilterAsync(2) : null;
        Task<List<ConsultSummary>>? consultsTask = keys.Contains(CoverSheetSections.Consults) ? GetConsultsAsync(null, 10) : null;
        Task<List<ImagingCard>>? imagingTask = keys.Contains(CoverSheetSections.Imaging) ? BuildImagingCardsAsync(MaxOf(CoverSheetSections.Imaging)) : null;
        Task<List<OncologyTumorCard>>? oncoTask = keys.Contains(CoverSheetSections.Oncology) ? BuildOncologyCardsAsync(tumors, MaxOf(CoverSheetSections.Oncology)) : null;
        Task<List<string>>? pgxTask = keys.Contains(CoverSheetSections.Pgx) ? BuildPgxAlertsAsync(MaxOf(CoverSheetSections.Pgx)) : null;

        var pending = new List<Task> { allergiesTask };
        foreach (Task? t in new Task?[] { problemsTask, medsTask, remindersTask, labsTask, vitalsTask, visitsTask, ordersTask, consultsTask, imagingTask, oncoTask, pgxTask })
            if (t is not null) pending.Add(t);
        await Task.WhenAll(pending);

        var cwad = await BuildCwadFlagsAsync(patientState);

        string banner = string.Empty;
        if (hasActiveCancer && layout.Id == CoverSheetLayouts.Procedural)
        {
            OncologyTumorIndexEntry t = tumors[0];
            string site = string.IsNullOrWhiteSpace(t.PrimarySiteText) ? t.PrimarySite : t.PrimarySiteText;
            string stage = string.IsNullOrWhiteSpace(t.StageGroup) ? string.Empty : $", {t.StageGroup}";
            banner = $"Active oncology ({site}{stage}) — confirm this elective procedure is not deferred by the treatment plan.";
        }

        string reason = auto ? autoReason : "Manually selected";

        return new SpecialtyCoverSheet
        {
            PatientId = PatientId,
            LayoutId = layout.Id,
            LayoutName = layout.Name,
            LayoutReason = reason,
            ContextBanner = banner,
            Sections = layout.Sections.ToList(),
            Demographics = BuildDemographicsSummary(patientState),
            Cwad = cwad,
            Allergies = allergiesTask.Result,
            ActiveProblems = problemsTask is null ? new() : problemsTask.Result.Take(MaxOf(CoverSheetSections.Problems)).ToList(),
            ActiveMedications = medsTask is null ? new() : medsTask.Result.Take(MaxOf(CoverSheetSections.Medications)).ToList(),
            ClinicalReminders = remindersTask is null ? new() : remindersTask.Result.Take(MaxOf(CoverSheetSections.Reminders)).ToList(),
            RecentLabs = labsTask is null ? new() : labsTask.Result.Take(MaxOf(CoverSheetSections.Labs)).ToList(),
            RecentVitals = vitalsTask is null ? new() : vitalsTask.Result.Take(MaxOf(CoverSheetSections.Vitals)).ToList(),
            RecentVisits = visitsTask is null ? new() : visitsTask.Result.Take(MaxOf(CoverSheetSections.Visits)).Select(e => new VisitSummary
            {
                AppointmentId = e.AppointmentId,
                ClinicName = e.ClinicName,
                AppointmentDateTime = e.AppointmentDateTime,
                Status = e.Status,
                ProviderName = e.ProviderName
            }).ToList(),
            ActiveOrders = ordersTask is null ? new() : ordersTask.Result.Take(MaxOf(CoverSheetSections.Orders)).ToList(),
            ActiveConsults = consultsTask is null ? new() : consultsTask.Result.Take(MaxOf(CoverSheetSections.Consults)).ToList(),
            OncologyTumors = oncoTask is null ? new() : oncoTask.Result,
            UpcomingProcedures = keys.Contains(CoverSheetSections.Procedures) ? upcoming.Take(MaxOf(CoverSheetSections.Procedures)).ToList() : new(),
            LatestImaging = imagingTask is null ? new() : imagingTask.Result,
            PgxAlerts = pgxTask is null ? new() : pgxTask.Result,
            LastRefreshed = DateTime.UtcNow,
            SectionsLoaded = keys.Count
        };
    }

    private async Task<List<OncologyTumorCard>> BuildOncologyCardsAsync(List<OncologyTumorIndexEntry> tumors, int max)
    {
        if (tumors.Count == 0) return new();
        List<OncologyTreatmentIndexEntry> treatments = await GetOncologyTreatmentsAsync();
        var cards = new List<OncologyTumorCard>();
        foreach (OncologyTumorIndexEntry t in tumors.Take(max))
        {
            var biomarkers = await GetTumorBiomarkersAsync(t.TumorId);
            var matches = await GetPrecisionOncologyMatchesAsync(t.TumorId);
            OncologyTreatmentIndexEntry? tx = treatments
                .Where(x => x.TumorId == t.TumorId)
                .OrderByDescending(x => x.StartDate ?? DateTime.MinValue)
                .FirstOrDefault();
            cards.Add(new OncologyTumorCard
            {
                TumorId = t.TumorId,
                PrimarySite = string.IsNullOrWhiteSpace(t.PrimarySiteText) ? t.PrimarySite : t.PrimarySiteText,
                Histology = string.IsNullOrWhiteSpace(t.HistologyText) ? t.Histology : t.HistologyText,
                StageGroup = t.StageGroup,
                Status = t.Status.ToString(),
                OncologistName = t.OncologistName,
                DateOfDiagnosis = t.DateOfDiagnosis,
                CurrentTreatment = tx is null ? "—" : $"{tx.TreatmentType}: {tx.AgentName} ({tx.Status})",
                Biomarkers = biomarkers.Select(b => $"{b.Gene}: {b.Result} ({b.Status})").ToList(),
                PrecisionMatches = matches.Select(m => $"{m.Gene} → {m.TherapyClass} ({m.ExampleAgents})").ToList()
            });
        }
        return cards;
    }

    private async Task<List<ImagingCard>> BuildImagingCardsAsync(int max)
    {
        List<RadiologySummary> studies = await GetRadiologyStudiesAsync(max);
        var cards = new List<ImagingCard>();
        foreach (RadiologySummary s in studies.Take(max))
        {
            RadiologyState full = await GetRadiologyStudyAsync(s.RadiologyId);
            cards.Add(new ImagingCard
            {
                ProcedureName = string.IsNullOrWhiteSpace(full.ProcedureName) ? s.ProcedureName : full.ProcedureName,
                ImagingType = full.ImagingType ?? s.ImagingType,
                ExamDateTime = full.ExamDateTime ?? s.ExamDateTime,
                RequestingProviderName = full.RequestingProviderName ?? s.RequestingProviderName,
                InterpretingPhysicianName = full.InterpretingPhysicianName,
                Impression = full.Impression,
                Status = s.Status
            });
        }
        return cards;
    }

    private async Task<List<string>> BuildPgxAlertsAsync(int max)
    {
        var recs = await GetPharmacogenomicRecommendationsAsync();
        return recs
            .Where(r => r.Action != PgxActionCategory.Standard)
            .Take(max)
            .Select(r => $"{r.Drug} — {r.Action} ({r.Gene} {r.PhenotypeLabel})")
            .ToList();
    }
}
