// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class GenomicsGrain : Grain, IGenomicsGrain
{
    private readonly IPersistentState<GenomicsState> _state;

    public GenomicsGrain(
        [PersistentState("genomicsState", "genomicsStore")] IPersistentState<GenomicsState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId))
        {
            _state.State.PatientId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task<string> RecordReportAsync(GeneticTestReport report)
    {
        if (string.IsNullOrEmpty(report.ReportId))
            report.ReportId = Guid.NewGuid().ToString();
        foreach (GeneticVariant v in report.Variants)
            if (string.IsNullOrEmpty(v.VariantId))
                v.VariantId = Guid.NewGuid().ToString();
        if (report.RecordedDate == default)
            report.RecordedDate = DateTime.UtcNow;
        _state.State.Reports.Add(report);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        return report.ReportId;
    }

    public async Task AddVariantAsync(string reportId, GeneticVariant variant)
    {
        GeneticTestReport? report = _state.State.Reports.FirstOrDefault(r => r.ReportId == reportId);
        if (report is null) return;
        if (string.IsNullOrEmpty(variant.VariantId))
            variant.VariantId = Guid.NewGuid().ToString();
        report.Variants.Add(variant);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RemoveReportAsync(string reportId)
    {
        int removed = _state.State.Reports.RemoveAll(r => r.ReportId == reportId);
        if (removed == 0) return;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<GenomicsState> GetAsync() => Task.FromResult(_state.State);
}
