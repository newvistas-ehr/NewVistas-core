// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Optional feature grain for iCare-style unified provider dashboard.
/// Aggregates clinical reminders, quality measure gaps, and disease registry
/// data across a provider's patient panel.
///
/// Maps to IHS RPMS iCare / BQI dashboard.
/// Keyed by "ICARE:{providerId}".
/// </summary>
public class iCareDashboardGrain : Grain, IiCareDashboardGrain
{
    private readonly IPersistentState<iCareDashboardState> _state;

    public iCareDashboardGrain(
        [PersistentState("iCareDashboardState", "iCareDashboardStore")]
        IPersistentState<iCareDashboardState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.ProviderId))
        {
            _state.State.ProviderId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<iCareDashboardState> GetDashboardStateAsync()
        => Task.FromResult(_state.State);

    public Task<List<PanelPatient>> GetPanelAsync()
        => Task.FromResult(_state.State.Panel);

    public async Task AddPatientToPanelAsync(string patientId, string patientName)
    {
        if (!_state.State.Panel.Any(p => p.PatientId == patientId))
        {
            _state.State.Panel.Add(new PanelPatient
            {
                PatientId = patientId,
                PatientName = patientName,
                AddedDate = DateTime.UtcNow
            });
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task RemovePatientFromPanelAsync(string patientId)
    {
        int removed = _state.State.Panel.RemoveAll(p => p.PatientId == patientId);
        if (removed > 0)
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task<iCareDashboardResult> GenerateDashboardAsync()
    {
        if (_state.State.Panel.Count == 0)
        {
            return new iCareDashboardResult
            {
                Success = true,
                GeneratedDate = DateTime.UtcNow,
                TotalPatients = 0
            };
        }

        var summaries = new List<iCarePatientSummary>();

        foreach (PanelPatient panelPatient in _state.State.Panel)
        {
            iCarePatientSummary summary = await BuildPatientSummaryAsync(panelPatient.PatientId);
            summaries.Add(summary);
        }

        _state.State.PatientSummaries = summaries;
        _state.State.LastGeneratedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();

        return new iCareDashboardResult
        {
            Success = true,
            PatientSummaries = summaries,
            GeneratedDate = DateTime.UtcNow,
            TotalPatients = summaries.Count,
            PatientsWithGaps = summaries.Count(s => s.OverallStatus != "GREEN"),
            TotalDueReminders = summaries.Sum(s => s.DueReminderCount),
            TotalQualityGaps = summaries.Sum(s => s.QualityGapCount)
        };
    }

    public async Task<iCarePatientSummary> GetPatientSummaryAsync(string patientId)
    {
        iCarePatientSummary? cached = _state.State.PatientSummaries
            .FirstOrDefault(s => s.PatientId == patientId);
        if (cached != null) return cached;

        return await BuildPatientSummaryAsync(patientId);
    }

    // ── Build patient summary by aggregating reminders + registries ──────

    private async Task<iCarePatientSummary> BuildPatientSummaryAsync(string patientId)
    {
        IPatientGrain patientGrain = GrainFactory.GetGrain<IPatientGrain>(patientId);
        PatientState patient = await patientGrain.GetPatientAsync();

        var summary = new iCarePatientSummary
        {
            PatientId = patientId,
            PatientName = patient.Name,
            DateOfBirth = patient.DateOfBirth,
            Sex = patient.Sex
        };

        // ── 1. Clinical Reminders ───────────────────────────────────
        List<string> reminderIds = await patientGrain.GetClinicalReminderIdsAsync();
        foreach (string reminderId in reminderIds)
        {
            IClinicalReminderGrain reminderGrain =
                GrainFactory.GetGrain<IClinicalReminderGrain>(reminderId);
            ClinicalReminderState reminder = await reminderGrain.GetReminderAsync();

            if (reminder.Status == "DUE")
            {
                summary.DueReminders.Add(new iCareReminderItem
                {
                    ReminderName = reminder.ReminderName,
                    Category = reminder.Category ?? string.Empty,
                    Priority = reminder.Priority ?? "NORMAL",
                    DueDate = reminder.DueDate
                });
            }
        }
        summary.DueReminderCount = summary.DueReminders.Count;

        // ── 2. Quality Measure Gaps ─────────────────────────────────
        // Check active CQM measures for gaps (patient in denominator but not numerator)
        ICqmMeasureIndexGrain cqmIndex =
            GrainFactory.GetGrain<ICqmMeasureIndexGrain>("CQM-INDEX");
        List<CqmMeasureSummary> measures = await cqmIndex.GetActiveMeasuresAsync();

        // For each active measure, check if patient has problems matching the clinical domain
        // This is a lightweight heuristic — full CQM evaluation is done by the CQM report grain
        List<ProblemEntry> problems = await patientGrain.GetProblemsAsync();
        foreach (CqmMeasureSummary measure in measures)
        {
            bool inDomain = IsDomainMatch(measure.ClinicalDomain, problems);
            if (inDomain)
            {
                // Patient has condition matching the measure domain — potential gap
                summary.QualityGaps.Add(new iCareQualityGap
                {
                    MeasureId = measure.MeasureId,
                    MeasureTitle = measure.Title,
                    ClinicalDomain = measure.ClinicalDomain,
                    GapDescription = $"Review needed: {measure.Title}"
                });
            }
        }
        summary.QualityGapCount = summary.QualityGaps.Count;

        // ── 3. Registry Enrollment ──────────────────────────────────
        IClinicalRegistryIndexGrain registryIndex =
            GrainFactory.GetGrain<IClinicalRegistryIndexGrain>("CCR-INDEX");
        List<CCREntrySummary> allEntries = await registryIndex.GetAllEntriesAsync();
        List<CCREntrySummary> patientEntries = allEntries
            .Where(e => e.PatientId == patientId && e.Status == CCREnrollmentStatus.Active)
            .ToList();

        foreach (CCREntrySummary entry in patientEntries)
        {
            summary.Registries.Add(new iCareRegistryEntry
            {
                RegistryType = entry.RegistryType.ToString(),
                EnrollmentStatus = entry.Status.ToString(),
                KeyIndicator = $"Enrolled since {entry.EnrollmentDate:yyyy-MM-dd}",
                LastIndicatorDate = entry.LastModifiedDate
            });
        }

        // ── 4. Overall Status ───────────────────────────────────────
        bool hasHighPriorityReminders = summary.DueReminders.Any(r => r.Priority == "HIGH");
        summary.OverallStatus = hasHighPriorityReminders || summary.QualityGapCount > 2
            ? "RED"
            : summary.DueReminderCount > 0 || summary.QualityGapCount > 0
                ? "YELLOW"
                : "GREEN";

        return summary;
    }

    /// <summary>
    /// Lightweight heuristic: does the patient have problems matching a CQM clinical domain?
    /// </summary>
    private static bool IsDomainMatch(string clinicalDomain, List<ProblemEntry> problems)
    {
        if (problems.Count == 0) return false;

        string domainLower = clinicalDomain.ToLowerInvariant();
        return problems.Any(p =>
            (p.Diagnosis?.Contains(domainLower, StringComparison.OrdinalIgnoreCase) ?? false) ||
            (p.DiagnosisCode?.StartsWith(GetDomainIcdPrefix(domainLower)) ?? false));
    }

    private static string GetDomainIcdPrefix(string domain) => domain switch
    {
        "diabetes" => "E11",
        "hypertension" => "I10",
        "depression" => "F32",
        "asthma" => "J45",
        "copd" => "J44",
        _ => "NONE"
    };
}
