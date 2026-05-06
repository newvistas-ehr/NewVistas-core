// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Singleton index for encounter form templates. Keyed by "EF-TPL-IDX".
/// </summary>
public class EncounterFormTemplateIndexGrain : Grain, IEncounterFormTemplateIndexGrain
{
    private readonly IPersistentState<EncounterFormTemplateIndexState> _state;

    public EncounterFormTemplateIndexGrain(
        [PersistentState("encounterFormTemplateIndexState", "encounterFormTemplateIndexStore")]
        IPersistentState<EncounterFormTemplateIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(EncounterFormTemplateIndexEntry entry)
    {
        _state.State.Entries[entry.TemplateId] = entry;
        await _state.WriteStateAsync();
    }

    public async Task RemoveAsync(string templateId)
    {
        _state.State.Entries.Remove(templateId);
        await _state.WriteStateAsync();
    }

    public Task<List<EncounterFormTemplateIndexEntry>> GetAllAsync(int maxResults = 50) =>
        Task.FromResult(_state.State.Entries.Values.OrderBy(e => e.Name).Take(maxResults).ToList());

    public Task<List<EncounterFormTemplateIndexEntry>> GetByFormTypeAsync(string formType, int maxResults = 50) =>
        Task.FromResult(_state.State.Entries.Values
            .Where(e => e.FormType == formType)
            .OrderBy(e => e.Name).Take(maxResults).ToList());

    public Task<List<EncounterFormTemplateIndexEntry>> GetPublishedAsync(int maxResults = 50) =>
        Task.FromResult(_state.State.Entries.Values
            .Where(e => e.Status == "PUBLISHED")
            .OrderBy(e => e.Name).Take(maxResults).ToList());

    public Task<List<EncounterFormTemplateIndexEntry>> SearchAsync(
        string? formType, string? status, string? clinicId, int maxResults = 50)
    {
        IEnumerable<EncounterFormTemplateIndexEntry> query = _state.State.Entries.Values;
        if (!string.IsNullOrWhiteSpace(formType)) query = query.Where(e => e.FormType == formType);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(e => e.Status == status);
        if (!string.IsNullOrWhiteSpace(clinicId)) query = query.Where(e => e.ClinicId == clinicId);
        return Task.FromResult(query.OrderBy(e => e.Name).Take(maxResults).ToList());
    }

    public Task<int> GetCountAsync() => Task.FromResult(_state.State.Entries.Count);
}

/// <summary>
/// Singleton index for encounter form instances. Keyed by "EF-INST-IDX".
/// </summary>
public class EncounterFormInstanceIndexGrain : Grain, IEncounterFormInstanceIndexGrain
{
    private readonly IPersistentState<EncounterFormInstanceIndexState> _state;

    public EncounterFormInstanceIndexGrain(
        [PersistentState("encounterFormInstanceIndexState", "encounterFormInstanceIndexStore")]
        IPersistentState<EncounterFormInstanceIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(EncounterFormInstanceIndexEntry entry)
    {
        _state.State.Entries[entry.InstanceId] = entry;
        await _state.WriteStateAsync();
    }

    public Task<List<EncounterFormInstanceIndexEntry>> GetByPatientAsync(string patientId) =>
        Task.FromResult(_state.State.Entries.Values
            .Where(e => e.PatientId == patientId)
            .OrderByDescending(e => e.CreatedDate).ToList());

    public Task<List<EncounterFormInstanceIndexEntry>> GetByTemplateAsync(string templateId, int maxResults = 50) =>
        Task.FromResult(_state.State.Entries.Values
            .Where(e => e.TemplateId == templateId)
            .OrderByDescending(e => e.CreatedDate).Take(maxResults).ToList());

    public Task<List<EncounterFormInstanceIndexEntry>> GetByStatusAsync(string status, int maxResults = 50) =>
        Task.FromResult(_state.State.Entries.Values
            .Where(e => e.Status == status)
            .OrderByDescending(e => e.CreatedDate).Take(maxResults).ToList());

    public Task<List<EncounterFormInstanceIndexEntry>> SearchAsync(
        string? patientId, string? templateId, string? status, int maxResults = 50)
    {
        IEnumerable<EncounterFormInstanceIndexEntry> query = _state.State.Entries.Values;
        if (!string.IsNullOrWhiteSpace(patientId)) query = query.Where(e => e.PatientId == patientId);
        if (!string.IsNullOrWhiteSpace(templateId)) query = query.Where(e => e.TemplateId == templateId);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(e => e.Status == status);
        return Task.FromResult(query.OrderByDescending(e => e.CreatedDate).Take(maxResults).ToList());
    }

    public Task<int> GetCountAsync() => Task.FromResult(_state.State.Entries.Count);
}
