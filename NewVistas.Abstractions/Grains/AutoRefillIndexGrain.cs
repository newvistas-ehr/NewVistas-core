// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Singleton index for auto-refill enrollments. Keyed by "RX-AUTOREFILL-IDX".
/// </summary>
public class AutoRefillIndexGrain : Grain, IAutoRefillIndexGrain
{
    private readonly IPersistentState<AutoRefillIndexState> _state;

    public AutoRefillIndexGrain(
        [PersistentState("autoRefillIndexState", "autoRefillIndexStore")]
        IPersistentState<AutoRefillIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(AutoRefillIndexEntry entry)
    {
        _state.State.Entries[entry.EnrollmentId] = entry;
        await _state.WriteStateAsync();
    }

    public async Task RemoveAsync(string enrollmentId)
    {
        _state.State.Entries.Remove(enrollmentId);
        await _state.WriteStateAsync();
    }

    public Task<List<AutoRefillIndexEntry>> GetByPatientAsync(string patientId, int maxResults = 50) =>
        Task.FromResult(_state.State.Entries.Values
            .Where(e => e.PatientId == patientId)
            .OrderBy(e => e.NextRefillDate).Take(maxResults).ToList());

    public Task<List<AutoRefillIndexEntry>> GetByStatusAsync(string status, int maxResults = 50) =>
        Task.FromResult(_state.State.Entries.Values
            .Where(e => e.Status == status)
            .OrderBy(e => e.NextRefillDate).Take(maxResults).ToList());

    public Task<List<AutoRefillIndexEntry>> GetByPharmacyAsync(string pharmacyId, int maxResults = 50) =>
        Task.FromResult(_state.State.Entries.Values
            .Where(e => e.PharmacyId == pharmacyId)
            .OrderBy(e => e.NextRefillDate).Take(maxResults).ToList());

    public Task<List<AutoRefillIndexEntry>> GetDueForRefillAsync(DateTime asOfDate, int maxResults = 50) =>
        Task.FromResult(_state.State.Entries.Values
            .Where(e => e.Status == "ACTIVE" && e.NextRefillDate <= asOfDate && e.RefillsRemaining > 0)
            .OrderBy(e => e.NextRefillDate).Take(maxResults).ToList());

    public Task<List<AutoRefillIndexEntry>> SearchAsync(
        string? patientId, string? status, string? pharmacyId, int maxResults = 50)
    {
        IEnumerable<AutoRefillIndexEntry> query = _state.State.Entries.Values;
        if (!string.IsNullOrWhiteSpace(patientId)) query = query.Where(e => e.PatientId == patientId);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(e => e.Status == status);
        if (!string.IsNullOrWhiteSpace(pharmacyId)) query = query.Where(e => e.PharmacyId == pharmacyId);
        return Task.FromResult(query.OrderBy(e => e.NextRefillDate).Take(maxResults).ToList());
    }

    public Task<int> GetCountAsync() => Task.FromResult(_state.State.Entries.Count);
}
