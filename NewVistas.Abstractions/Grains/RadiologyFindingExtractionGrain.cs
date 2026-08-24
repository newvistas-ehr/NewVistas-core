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
/// Per-report radiology-finding extraction grain. Keyed by report id. Grounds against the
/// report (every finding cites a sentence and is verified), flags material findings, and
/// runs the acknowledge/reject-with-reason gate.
/// </summary>
public class RadiologyFindingExtractionGrain : Grain, IRadiologyFindingExtractionGrain
{
    private readonly IPersistentState<RadiologyExtractionState> _state;

    public RadiologyFindingExtractionGrain(
        [PersistentState("radiologyFindingState", "radiologyFindingStore")]
        IPersistentState<RadiologyExtractionState> state)
    {
        _state = state;
    }

    public Task<RadiologyExtractionState> GetAsync() => Task.FromResult(_state.State);

    /// <summary>
    /// Extracts findings from the report, verifies each source quote, and flags material findings.
    /// Safe to re-run: a disposition, once recorded, survives re-extraction — either carried onto
    /// the matching finding or retained verbatim. Only undispositioned findings are replaced.
    /// </summary>
    public async Task<RadiologyExtractionState> ExtractAsync(string reportText, string patientId, string extractedBy)
    {
        // 1. EXTRACT off the per-report grain (stateless worker isolates a slow model call).
        RadiologyExtractionResult result = await GrainFactory
            .GetGrain<IRadiologyExtractionWorkerGrain>(RadiologyExtractionWorkerGrain.Key)
            .ExtractAsync(reportText);

        // 2. VERIFY: every finding must quote a sentence actually in the report.
        RadiologyFindingVerifier.Verify(reportText, result.Findings);

        // 3. Flag material findings (≥ Moderate) as requiring an acknowledge/reject decision.
        //    Scoping the forcing function to material findings is what avoids alert fatigue.
        foreach (RadiologyFinding finding in result.Findings)
            finding.RequiresAcknowledgment = finding.Severity >= FindingSeverity.Moderate;

        // 4. Invariant: a disposition, once recorded, survives re-extraction — either carried
        //    onto the matching new finding or retained verbatim. A rejection is a patient-visible
        //    record; re-running extraction must never erase it.
        List<RadiologyFinding> dispositioned = _state.State.Findings
            .Where(f => f.Acknowledgment != FindingAcknowledgment.Pending)
            .ToList();

        foreach (RadiologyFinding fresh in result.Findings)
        {
            RadiologyFinding? old = MatchDispositioned(dispositioned, fresh);
            if (old is null)
                continue;

            fresh.Acknowledgment = old.Acknowledgment;
            fresh.DispositionedBy = old.DispositionedBy;
            fresh.DispositionedDate = old.DispositionedDate;
            fresh.RejectionReason = old.RejectionReason;
            fresh.PatientVisible = old.PatientVisible;
            dispositioned.Remove(old);   // one old disposition transfers to at most one new finding
        }

        // Any dispositioned finding the fresh extraction no longer produced is retained
        // unchanged (keeping its FindingId) so a recorded acknowledge/reject cannot vanish.
        result.Findings.AddRange(dispositioned);

        _state.State.ReportId = this.GetPrimaryKeyString();
        _state.State.PatientId = patientId;
        _state.State.ReportText = reportText;
        _state.State.ExtractedBy = extractedBy;
        _state.State.ExtractedDate = DateTime.UtcNow;
        _state.State.ModelProvider = result.ProviderName;
        _state.State.Findings = result.Findings;

        await _state.WriteStateAsync();
        return _state.State;
    }

    public async Task AcknowledgeAsync(string findingId, string clinicianId)
    {
        RadiologyFinding finding = Require(findingId);
        finding.Acknowledgment = FindingAcknowledgment.Acknowledged;
        finding.DispositionedBy = clinicianId;
        finding.DispositionedDate = DateTime.UtcNow;
        finding.RejectionReason = null;
        await _state.WriteStateAsync();
    }

    public async Task RejectAsync(string findingId, string clinicianId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A reason is required to reject a finding.", nameof(reason));

        RadiologyFinding finding = Require(findingId);
        finding.Acknowledgment = FindingAcknowledgment.Rejected;
        finding.DispositionedBy = clinicianId;
        finding.DispositionedDate = DateTime.UtcNow;
        finding.RejectionReason = reason;
        finding.PatientVisible = true;   // a rejection is recorded and visible to the patient
        await _state.WriteStateAsync();
    }

    /// <summary>
    /// Finds the previously dispositioned finding a freshly extracted one corresponds to.
    /// Primary match on (FindingType, Level, Laterality), strings case-insensitive; if several
    /// candidates remain, prefer the one whose source quote matches (whitespace-normalized).
    /// </summary>
    private static RadiologyFinding? MatchDispositioned(List<RadiologyFinding> dispositioned, RadiologyFinding fresh)
    {
        List<RadiologyFinding> candidates = dispositioned
            .Where(old => string.Equals(old.FindingType, fresh.FindingType, StringComparison.OrdinalIgnoreCase)
                       && string.Equals(old.Level, fresh.Level, StringComparison.OrdinalIgnoreCase)
                       && old.Laterality == fresh.Laterality)
            .ToList();

        if (candidates.Count <= 1)
            return candidates.FirstOrDefault();

        return candidates.FirstOrDefault(old =>
                   NormalizeWhitespace(old.SourceQuote) == NormalizeWhitespace(fresh.SourceQuote))
               ?? candidates[0];
    }

    private static string NormalizeWhitespace(string text) =>
        string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private RadiologyFinding Require(string findingId) =>
        _state.State.Findings.FirstOrDefault(f => f.FindingId == findingId)
            ?? throw new InvalidOperationException($"Finding '{findingId}' not found on report '{_state.State.ReportId}'.");
}
