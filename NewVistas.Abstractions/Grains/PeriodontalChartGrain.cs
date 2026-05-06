// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class PeriodontalChartGrain : Grain, IPeriodontalChartGrain
{
    private readonly IPersistentState<PeriodontalChartState> _state;

    public PeriodontalChartGrain(
        [PersistentState("periodontalChartState", "periodontalChartStore")]
        IPersistentState<PeriodontalChartState> state) { _state = state; }

    public Task<PeriodontalChartState> GetChartAsync() => Task.FromResult(_state.State);

    public async Task<PeriodontalChartState> CreateChartAsync(
        string patientId, string patientName,
        string providerId, string providerName, string? notes)
    {
        _state.State.ChartId = this.GetPrimaryKeyString();
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.ProviderId = providerId;
        _state.State.ProviderName = providerName;
        _state.State.Status = "DRAFT";
        _state.State.Notes = notes;
        _state.State.ExamDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
        await UpdateIndexAsync();
        return _state.State;
    }

    public async Task RecordToothDataAsync(int toothNumber, PeriodontalToothData data)
    {
        if (_state.State.Status == "FINALIZED")
            throw new InvalidOperationException("Cannot modify a finalized chart. Use addendum instead.");
        if (toothNumber < 1 || toothNumber > 32)
            throw new ArgumentOutOfRangeException(nameof(toothNumber), "Tooth number must be 1-32.");

        _state.State.TeethData[toothNumber] = data;
        RecalculateSummary();
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task RecordMultipleTeethAsync(List<PeriodontalToothEntry> entries)
    {
        if (_state.State.Status == "FINALIZED")
            throw new InvalidOperationException("Cannot modify a finalized chart.");

        foreach (var entry in entries)
        {
            if (entry.ToothNumber < 1 || entry.ToothNumber > 32) continue;
            _state.State.TeethData[entry.ToothNumber] = entry.Data;
        }

        RecalculateSummary();
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task MarkToothMissingAsync(int toothNumber, string reason)
    {
        if (toothNumber < 1 || toothNumber > 32)
            throw new ArgumentOutOfRangeException(nameof(toothNumber));

        _state.State.MissingTeeth[toothNumber] = reason;
        _state.State.TeethData.Remove(toothNumber);
        RecalculateSummary();
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task SetOverallAssessmentAsync(string classification, string? treatmentPlan, string assessedByName)
    {
        _state.State.Classification = classification;
        _state.State.TreatmentPlan = treatmentPlan;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task FinalizeChartAsync(string finalizedByName)
    {
        _state.State.Status = "FINALIZED";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    public async Task AddendChartAsync(string addendumNote, string addendedByName)
    {
        if (_state.State.Status != "FINALIZED")
            throw new InvalidOperationException("Only finalized charts can be addended.");

        _state.State.Status = "ADDENDED";
        _state.State.AddendumNotes = string.IsNullOrEmpty(_state.State.AddendumNotes)
            ? $"[{DateTime.UtcNow:g} {addendedByName}] {addendumNote}"
            : $"{_state.State.AddendumNotes}\n[{DateTime.UtcNow:g} {addendedByName}] {addendumNote}";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await UpdateIndexAsync();
    }

    private void RecalculateSummary()
    {
        _state.State.TeethCharted = _state.State.TeethData.Count;
        int deepPockets = 0;
        int bleedingSites = 0;

        foreach (var tooth in _state.State.TeethData.Values)
        {
            for (int i = 0; i < 6; i++)
            {
                if (tooth.ProbingDepths[i] >= 4) deepPockets++;
                if (tooth.BleedingOnProbing[i]) bleedingSites++;
            }
        }

        _state.State.DeepPocketCount = deepPockets;
        _state.State.BleedingSiteCount = bleedingSites;
    }

    private async Task UpdateIndexAsync()
    {
        var index = GrainFactory.GetGrain<IPeriodontalChartIndexGrain>("PERIO-IDX");
        await index.AddOrUpdateAsync(new PeriodontalChartIndexEntry
        {
            ChartId = _state.State.ChartId, PatientId = _state.State.PatientId,
            PatientName = _state.State.PatientName, ProviderId = _state.State.ProviderId,
            ProviderName = _state.State.ProviderName, Status = _state.State.Status,
            Classification = _state.State.Classification, TeethCharted = _state.State.TeethCharted,
            DeepPocketCount = _state.State.DeepPocketCount, BleedingSiteCount = _state.State.BleedingSiteCount,
            ExamDate = _state.State.ExamDate
        });
    }
}
