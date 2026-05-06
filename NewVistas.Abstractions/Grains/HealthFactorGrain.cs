// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Health Factor Grain implementation based on VistA V HEALTH FACTORS file (#9000010.23)
/// </summary>
public class HealthFactorGrain : Grain, IHealthFactorGrain
{
    private readonly IPersistentState<HealthFactorState> _state;

    public HealthFactorGrain(
        [PersistentState("healthFactorState", "healthFactorStore")] IPersistentState<HealthFactorState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.HealthFactorId))
        {
            _state.State.HealthFactorId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<HealthFactorState> GetHealthFactorAsync() => Task.FromResult(_state.State);

    public async Task RecordHealthFactorAsync(
        string patientId, string healthFactorName, string? healthFactorDefId,
        string? category, DateTime eventDateTime, string? levelSeverity,
        string? visitId, string? locationId, string? locationName,
        string? enteredById, string? enteredByName, string? comments)
    {
        _state.State.PatientId = patientId;
        _state.State.HealthFactorName = healthFactorName;
        _state.State.HealthFactorDefId = healthFactorDefId;
        _state.State.Category = category;
        _state.State.EventDateTime = eventDateTime;
        _state.State.LevelSeverity = levelSeverity;
        _state.State.VisitId = visitId;
        _state.State.LocationId = locationId;
        _state.State.LocationName = locationName;
        _state.State.EnteredById = enteredById;
        _state.State.EnteredByName = enteredByName;
        _state.State.Comments = comments;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateSeverityAsync(string severityLevel)
    {
        _state.State.LevelSeverity = severityLevel;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetCategoryAsync(string category, string? subcategory)
    {
        _state.State.Category = category;
        _state.State.Subcategory = subcategory;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetValueAsync(string value, string? magnitude)
    {
        _state.State.Value = value;
        _state.State.Magnitude = magnitude;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task LinkToVisitAsync(string visitId)
    {
        _state.State.VisitId = visitId;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddCommentAsync(string comment)
    {
        _state.State.Comments = comment;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ResolveAsync(string resolvedByName)
    {
        _state.State.EvaluationStatus = "RESOLVED";
        _state.State.ResolutionDate = DateTime.UtcNow;
        _state.State.ResolvedByName = resolvedByName;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ReactivateAsync()
    {
        _state.State.EvaluationStatus = "CURRENT";
        _state.State.ResolutionDate = null;
        _state.State.ResolvedByName = null;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddHistoryEntryAsync(string value, string? severityLevel, string? comment, string? recordedByName)
    {
        _state.State.History.Add(new HealthFactorHistoryEntry
        {
            EntryDate = DateTime.UtcNow,
            Value = value,
            SeverityLevel = severityLevel,
            Comment = comment,
            RecordedByName = recordedByName
        });
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<List<HealthFactorHistoryEntry>> GetHistoryAsync() => Task.FromResult(_state.State.History);
}
