// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Services;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Per-patient clinical summary generator. Keyed by patient id. Grounds against the
/// patient's discrete grain data, narrates via the injected (swappable) narrative
/// service, verifies the result, and persists a draft for clinician sign-off.
/// </summary>
public class PatientSummaryGrain : Grain, IPatientSummaryGrain
{
    private readonly IPersistentState<PatientSummaryState> _state;

    public PatientSummaryGrain(
        [PersistentState("patientSummaryState", "patientSummaryStore")]
        IPersistentState<PatientSummaryState> state)
    {
        _state = state;
    }

    private IPatientWorkflowGrain Workflow() =>
        GrainFactory.GetGrain<IPatientWorkflowGrain>(this.GetPrimaryKeyString());

    public async Task<ClinicalSummaryDraft> GenerateAsync(string purpose)
    {
        string patientId = this.GetPrimaryKeyString();

        // 1. RETRIEVE + GROUND: pull discrete facts from the chart, each with provenance.
        ClinicalSummaryContext context = await BuildContextAsync(patientId, purpose);

        // 2. NARRATE: composition runs in a stateless-worker grain so a slow/external
        //    model call doesn't pin this per-patient grain. The provider is swappable
        //    behind the seam; the model composes prose from the supplied facts only.
        NarrativeResult result = await GrainFactory
            .GetGrain<IClinicalNarrativeWorkerGrain>(ClinicalNarrativeWorkerGrain.Key)
            .ComposeAsync(context);

        // 3. VERIFY: no claim is trusted unless it traces back to a real source fact.
        int flagged = ClinicalSummaryVerifier.Verify(context, result.Claims);

        // 4. DRAFT: persist pending clinician sign-off.
        ClinicalSummaryDraft draft = new()
        {
            PatientId = patientId,
            Purpose = purpose,
            Narrative = result.Narrative,
            Claims = result.Claims,
            GroundingFacts = context.Facts,
            ModelProvider = result.ProviderName,
            Status = SummaryStatus.DraftPendingSignoff,
            GeneratedDate = DateTime.UtcNow,
            UnverifiedClaimCount = flagged,
            ConfigurationNotice = result.ConfigurationNotice,
        };

        _state.State.PatientId = patientId;
        _state.State.CurrentDraft = draft;
        await _state.WriteStateAsync();
        return draft;
    }

    public Task<ClinicalSummaryDraft?> GetCurrentDraftAsync() =>
        Task.FromResult(_state.State.CurrentDraft);

    public async Task SignOffAsync(string clinicianId)
    {
        if (_state.State.CurrentDraft is null)
            throw new InvalidOperationException("No summary draft to sign off.");
        if (_state.State.CurrentDraft.Status == SummaryStatus.Signed)
            throw new InvalidOperationException("Summary draft is already signed.");

        _state.State.CurrentDraft.Status = SummaryStatus.Signed;
        _state.State.CurrentDraft.SignedBy = clinicianId;
        _state.State.CurrentDraft.SignedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    /// <summary>
    /// Assembles the grounded context from the patient's discrete grain data. Each fact
    /// carries the grain and record id it came from so it can be verified against — and
    /// linked back to — the source of truth.
    /// </summary>
    private async Task<ClinicalSummaryContext> BuildContextAsync(string patientId, string purpose)
    {
        IPatientWorkflowGrain wf = Workflow();

        List<ProblemSummary> problems = await wf.GetActiveProblemsAsync();
        List<MedicationSummary> meds = await wf.GetActiveMedicationsAsync();
        List<AllergySummary> allergies = await wf.GetAllergiesAsync();
        List<LabTestSummaryEntry> labs = await wf.GetLabSummaryAsync();

        List<ClinicalFact> facts = new();
        int n = 0;

        foreach (ProblemSummary p in problems)
            facts.Add(new ClinicalFact
            {
                FactId = $"F{++n}",
                Category = ClinicalFactCategory.Problem,
                Text = string.IsNullOrWhiteSpace(p.DiagnosisCode) ? p.Diagnosis : $"{p.Diagnosis} ({p.DiagnosisCode})",
                SourceGrain = "ProblemGrain",
                SourceId = p.ProblemId,
            });

        foreach (MedicationSummary m in meds)
            facts.Add(new ClinicalFact
            {
                FactId = $"F{++n}",
                Category = ClinicalFactCategory.Medication,
                Text = string.IsNullOrWhiteSpace(m.Sig) ? m.DrugName : $"{m.DrugName} — {m.Sig}",
                SourceGrain = "PharmacyGrain",
                SourceId = m.PrescriptionId,
            });

        foreach (AllergySummary a in allergies)
            facts.Add(new ClinicalFact
            {
                FactId = $"F{++n}",
                Category = ClinicalFactCategory.Allergy,
                Text = string.IsNullOrWhiteSpace(a.Severity) ? a.Allergen : $"{a.Allergen} ({a.Severity})",
                SourceGrain = "AllergyGrain",
                SourceId = a.AllergyId,
            });

        foreach (LabTestSummaryEntry l in labs)
            facts.Add(new ClinicalFact
            {
                FactId = $"F{++n}",
                Category = ClinicalFactCategory.Lab,
                Text = $"{l.TestName} {l.Value} {l.Units}".Trim(),
                SourceGrain = "LabSummaryGrain",
                SourceId = l.LoincCode,
            });

        return new ClinicalSummaryContext { PatientId = patientId, Purpose = purpose, Facts = facts };
    }
}
