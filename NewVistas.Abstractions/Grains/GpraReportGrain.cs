// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class GpraReportGrain : Grain, IGpraReportGrain
{
    private readonly IPersistentState<GpraReportState> _state;

    public GpraReportGrain(
        [PersistentState("gpraReportState", "gpraReportStore")]
        IPersistentState<GpraReportState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.ReportId))
        {
            _state.State.ReportId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<GpraReportState> GetAsync() => Task.FromResult(_state.State);

    public async Task CreateAsync(
        int fiscalYear,
        GpraReportingPeriod reportingPeriod,
        DateTime currentPeriodStart, DateTime currentPeriodEnd,
        DateTime baselinePeriodStart, DateTime baselinePeriodEnd,
        string facilityId, string facilityName,
        string? communityTaxonomy,
        int activeUserPopulation,
        string? generatedById, string? generatedByName)
    {
        _state.State.Status = GpraReportStatus.Draft;
        _state.State.FiscalYear = fiscalYear;
        _state.State.ReportingPeriod = reportingPeriod;
        _state.State.CurrentPeriodStart = currentPeriodStart;
        _state.State.CurrentPeriodEnd = currentPeriodEnd;
        _state.State.BaselinePeriodStart = baselinePeriodStart;
        _state.State.BaselinePeriodEnd = baselinePeriodEnd;
        _state.State.FacilityId = facilityId;
        _state.State.FacilityName = facilityName;
        _state.State.CommunityTaxonomy = communityTaxonomy;
        _state.State.ActiveUserPopulation = activeUserPopulation;
        _state.State.GeneratedById = generatedById;
        _state.State.GeneratedByName = generatedByName;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddIndicatorResultAsync(GpraIndicatorResult result)
    {
        _state.State.Indicators.Add(result);
        _state.State.Status = GpraReportStatus.Evaluating;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CompleteAsync()
    {
        _state.State.Status = GpraReportStatus.Completed;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkErrorAsync(string errorMessage)
    {
        _state.State.Status = GpraReportStatus.Error;
        _state.State.ErrorMessage = errorMessage;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddCqmReportLinkAsync(string cqmReportId)
    {
        if (!_state.State.CqmReportIds.Contains(cqmReportId))
            _state.State.CqmReportIds.Add(cqmReportId);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
