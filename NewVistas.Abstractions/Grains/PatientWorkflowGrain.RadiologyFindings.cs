// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// The AI radiology-findings façade. The extraction pipeline (stateless worker → verbatim
/// quote verification → materiality flagging → acknowledge/reject gate) has existed since the
/// extractor shipped, but nothing outside tests ever called it — this partial is what makes it
/// reachable from the product. The workflow interface entries carry the security-key and audit
/// attributes; grain-internal calls bypass the filters, so this boundary is where enforcement
/// lives.
/// </summary>
public partial class PatientWorkflowGrain
{
    private IRadiologyFindingExtractionGrain GetFindingExtractionGrain(string radiologyId)
        => GrainFactory.GetGrain<IRadiologyFindingExtractionGrain>(radiologyId);

    public async Task<RadiologyExtractionState> ExtractRadiologyFindingsAsync(
        string radiologyId, string extractedBy)
    {
        RadiologyState study = await GetRadiologyStudyAsync(radiologyId);
        if (string.IsNullOrWhiteSpace(study.RadiologyId))
            throw new InvalidOperationException($"Radiology study '{radiologyId}' not found.");
        if (string.IsNullOrWhiteSpace(study.ReportText))
            throw new InvalidOperationException(
                "This study has no filed report yet — there is nothing to extract findings from.");

        return await GetFindingExtractionGrain(radiologyId)
            .ExtractAsync(study.ReportText, this.GetPrimaryKeyString(), extractedBy);
    }

    public async Task<RadiologyExtractionState?> GetRadiologyFindingsAsync(string radiologyId)
    {
        RadiologyExtractionState state = await GetFindingExtractionGrain(radiologyId).GetAsync();
        return string.IsNullOrEmpty(state.ReportId) ? null : state;
    }

    public Task AcknowledgeRadiologyFindingAsync(string radiologyId, string findingId, string clinicianId)
        => GetFindingExtractionGrain(radiologyId).AcknowledgeAsync(findingId, clinicianId);

    public Task RejectRadiologyFindingAsync(
        string radiologyId, string findingId, string clinicianId, string reason)
        => GetFindingExtractionGrain(radiologyId).RejectAsync(findingId, clinicianId, reason);
}
