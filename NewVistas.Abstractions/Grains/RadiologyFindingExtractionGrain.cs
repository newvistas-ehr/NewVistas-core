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

    private RadiologyFinding Require(string findingId) =>
        _state.State.Findings.FirstOrDefault(f => f.FindingId == findingId)
            ?? throw new InvalidOperationException($"Finding '{findingId}' not found on report '{_state.State.ReportId}'.");
}
