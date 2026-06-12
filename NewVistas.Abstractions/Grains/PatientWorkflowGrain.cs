// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Concurrency;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Patient Workflow Grain — orchestrates VistA CPRS-style patient workflows.
///
/// This grain coordinates across multiple child grains to provide the same
/// patient-centric workflow that CPRS delivers via its MUMPS routines:
///
///   Cover Sheet  → ORWCV.m (background build polling PROB/CWAD/MEDS/RMND/LABS/VITL/VSIT)
///   Patient Info → ORWPT.m SELECT (demographics + CWAD + SC + admission status)
///   Order Entry  → ORWDX.m SAVE / ORWDXA.m (actions: sign, DC, hold, unhold)
///   Order List   → ORWORR.m AGET (filter: 1=All, 2=Current, 5=Pending, 7=Pending, etc.)
///   Problem List → GMPLSAVE.m / GMPLEDIT.m (save with audit, field-level tracking)
///   Check-In     → SDAM2.m ONE / SDAMEVT.m (BEFORE/AFTER event capture, CI/CO/Cancel/NoShow)
///   Vitals       → GMRVED*.m / GMRVFILE.m (enter/edit with qualifiers)
///   TIU Notes    → TIUSRVN.m / TIUSRVL.m / TIUSRVP.m (create/list/get notes)
///
/// The grain key is the patient ID (e.g., "PATIENT-{guid}").
///
/// This class is split across partial files by clinical domain:
///   PatientWorkflowGrain.CprsWorkflow.cs  — Orders, Problems, Appointments, Vitals, Meds, Allergies
///   PatientWorkflowGrain.Demographics.cs  — Demographics, Labs
///   PatientWorkflowGrain.ClinicalDocs.cs  — TIU Notes, Consults, Surgery, Radiology
///   PatientWorkflowGrain.Bcma.cs          — BCMA, MAR, Imaging
///   PatientWorkflowGrain.AncillaryServices.cs — Reminders, Immunizations, Health Factors, etc.
///   PatientWorkflowGrain.AdtAdmin.cs      — Means Test, SC, ADT, PCE, Audit, Notifications
///   PatientWorkflowGrain.Financial.cs     — Billing, Insurance, Registration, AR, Fee Basis
///   PatientWorkflowGrain.Specialties.cs   — Blood Bank, AP, Nursing, Dental, Social Work, etc.
///   PatientWorkflowGrain.Procedures.cs    — Medicine, Clinical Procedures, Radiation Therapy, IV, C&amp;P
/// </summary>
[Reentrant]
public partial class PatientWorkflowGrain : Grain, IPatientWorkflowGrain
{
    private string PatientId => this.GetPrimaryKeyString();

    private IPatientGrain GetPatientGrain() => GrainFactory.GetGrain<IPatientGrain>(PatientId);
    private IPatientIndexGrain GetPatientIndexGrain() => GrainFactory.GetGrain<IPatientIndexGrain>("PATIENT-INDEX");

    // ─── Capped Domain ID Lists (recent window + full history index) ──────

    private IPatientHistoryIndexGrain GetHistoryIndex(string domain)
        => GrainFactory.GetGrain<IPatientHistoryIndexGrain>($"{PatientId}:{domain}");

    /// <summary>
    /// Records a new clinical item ID for a domain: full-history index first
    /// (so no crash can lose the ID), lazy one-time migration of the legacy
    /// unbounded list, then the capped recent-window append on PatientState.
    /// Allergies must never go through this path.
    /// </summary>
    private async Task AppendCappedIdAsync(string domain, string id, DateTime? date)
    {
        IPatientHistoryIndexGrain history = GetHistoryIndex(domain);
        IPatientGrain patient = GetPatientGrain();

        // 1. History index first — crash after this point can duplicate work
        //    on retry (idempotent) but never lose the ID.
        await history.AddEntryAsync(new HistoryRef { ItemId = id, Date = date });

        int cap = await GetSiteParams().GetRecentItemsDisplayCountAsync();

        // 2. Lazy migration: flush the full legacy list to the history index
        //    BEFORE the first trim. Crash before the flag write leaves the
        //    full list intact — safe to retry.
        if (!await patient.IsDomainMigratedAsync(domain))
        {
            List<string> existing = await patient.GetDomainIdsAsync(domain);
            if (existing.Count > 0)
                await history.AddRangeAsync(
                    existing.Select(x => new HistoryRef { ItemId = x, Date = null }).ToList());

            await patient.MarkDomainMigratedAndTrimAsync(domain, cap);
        }

        // 3. Recent-window append (trims only because the domain is migrated).
        await patient.AddDomainIdCappedAsync(domain, id, cap);
    }

    /// <summary>
    /// Returns the COMPLETE ID set for a domain — history index once migrated,
    /// legacy PatientState list before. For clinical complete-set reads (due
    /// reminders, etc.); display paths should use the recent window instead.
    /// </summary>
    private async Task<List<string>> GetCompleteIdsAsync(string domain)
    {
        IPatientGrain patient = GetPatientGrain();
        return await patient.IsDomainMigratedAsync(domain)
            ? await GetHistoryIndex(domain).GetAllIdsAsync()
            : await patient.GetDomainIdsAsync(domain);
    }

    /// <summary>
    /// Returns one newest-first page of a domain's FULL ID history — history
    /// index once migrated, legacy PatientState list (reversed) before. Backs
    /// the paged Get{Domain}HistoryAsync readers so only the requested page
    /// fans out to item grains.
    /// </summary>
    private async Task<List<string>> GetHistoryPageIdsAsync(string domain, int offset, int maxResults)
    {
        IPatientGrain patient = GetPatientGrain();
        if (await patient.IsDomainMigratedAsync(domain))
            return await GetHistoryIndex(domain).GetPageAsync(offset, maxResults);

        List<string> all = await patient.GetDomainIdsAsync(domain);
        all.Reverse(); // append-chronological -> newest first
        return all.Skip(offset).Take(maxResults).ToList();
    }

    // ─── Cover Sheet (ORWCV.m START/BUILD/POLL) ──────────────────────────

    public async Task<CoverSheetState> GetCoverSheetAsync()
    {
        // Mirrors ORWCV BUILD: gather all sections concurrently,
        // equivalent to the background task that CPRS spawns via %ZTLOAD
        var patientGrain = GetPatientGrain();
        var patientState = await patientGrain.GetPatientAsync();

        // Fire all section loads in parallel, like CPRS background build
        var problemsTask = GetActiveProblemsAsync();
        var medsTask = GetActiveMedicationsAsync();
        var allergiesTask = GetAllergiesAsync();
        var remindersTask = LoadRemindersAsync(patientGrain);
        var labsTask = LoadRecentLabsAsync(patientGrain);
        var vitalsTask = GetLatestVitalsAsync();
        var visitsTask = GetAllAppointmentsAsync(20);
        var ordersTask = GetOrdersByFilterAsync(2); // 2 = Current/Active
        var notesTask = GetRecentNotesAsync(); // Hot cache — zero fan-out
        var consultsTask = GetConsultsAsync(null, 10); // Recent consults

        await Task.WhenAll(problemsTask, medsTask, allergiesTask, remindersTask,
            labsTask, vitalsTask, visitsTask, ordersTask, notesTask, consultsTask);

        return new CoverSheetState
        {
            PatientId = PatientId,
            Demographics = BuildDemographicsSummary(patientState),
            Cwad = await BuildCwadFlagsAsync(patientState),
            ActiveProblems = problemsTask.Result,
            ActiveMedications = medsTask.Result,
            Allergies = allergiesTask.Result,
            ClinicalReminders = remindersTask.Result,
            RecentLabs = labsTask.Result,
            RecentVitals = vitalsTask.Result,
            RecentVisits = visitsTask.Result.Select(e => new VisitSummary
            {
                AppointmentId = e.AppointmentId,
                ClinicName = e.ClinicName,
                AppointmentDateTime = e.AppointmentDateTime,
                Status = e.Status,
                ProviderName = e.ProviderName
            }).ToList(),
            ActiveOrders = ordersTask.Result,
            RecentNotes = notesTask.Result,
            ActiveConsults = consultsTask.Result,
            LastRefreshed = DateTime.UtcNow
        };
    }

    // ─── Patient Selection (ORWPT.m SELECT/IDINFO) ───────────────────────

    public async Task<PatientDemographicsSummary> GetPatientInfoAsync()
    {
        var patientState = await GetPatientGrain().GetPatientAsync();
        return BuildDemographicsSummary(patientState);
    }
}
