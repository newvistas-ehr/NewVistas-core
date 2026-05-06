// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// System-level singleton index for external referrals.
/// Keyed by "EXT-REF-IDX". Supports queries by patient, facility, and status.
/// </summary>
public class ExternalReferralIndexGrain : Grain, IExternalReferralIndexGrain
{
    private readonly IPersistentState<ExternalReferralIndexState> _state;

    public ExternalReferralIndexGrain(
        [PersistentState("externalReferralIndexState", "externalReferralIndexStore")]
        IPersistentState<ExternalReferralIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(ExternalReferralIndexEntry entry)
    {
        _state.State.Entries[entry.ReferralId] = entry;
        await _state.WriteStateAsync();
    }

    public Task<List<ExternalReferralIndexEntry>> GetByPatientAsync(string patientId)
    {
        List<ExternalReferralIndexEntry> results = _state.State.Entries.Values
            .Where(e => e.PatientId == patientId)
            .OrderByDescending(e => e.ReferralDate)
            .ToList();
        return Task.FromResult(results);
    }

    public Task<List<ExternalReferralIndexEntry>> GetByStatusAsync(string status, int maxResults = 50)
    {
        List<ExternalReferralIndexEntry> results = _state.State.Entries.Values
            .Where(e => e.Status == status)
            .OrderByDescending(e => e.ReferralDate)
            .Take(maxResults)
            .ToList();
        return Task.FromResult(results);
    }

    public Task<List<ExternalReferralIndexEntry>> GetByFacilityAsync(string facilityName, int maxResults = 50)
    {
        List<ExternalReferralIndexEntry> results = _state.State.Entries.Values
            .Where(e => e.ExternalFacilityName.Contains(facilityName, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.ReferralDate)
            .Take(maxResults)
            .ToList();
        return Task.FromResult(results);
    }

    public Task<List<ExternalReferralIndexEntry>> GetPendingFollowUpsAsync(int maxResults = 50)
    {
        List<ExternalReferralIndexEntry> results = _state.State.Entries.Values
            .Where(e => e.RequiresFollowUp)
            .OrderBy(e => e.ReferralDate)
            .Take(maxResults)
            .ToList();
        return Task.FromResult(results);
    }

    public Task<List<ExternalReferralIndexEntry>> SearchAsync(
        string? patientId, string? status, string? facility, int maxResults = 50)
    {
        IEnumerable<ExternalReferralIndexEntry> query = _state.State.Entries.Values;

        if (!string.IsNullOrWhiteSpace(patientId))
            query = query.Where(e => e.PatientId == patientId);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(e => e.Status == status);
        if (!string.IsNullOrWhiteSpace(facility))
            query = query.Where(e => e.ExternalFacilityName.Contains(facility, StringComparison.OrdinalIgnoreCase));

        List<ExternalReferralIndexEntry> results = query
            .OrderByDescending(e => e.ReferralDate)
            .Take(maxResults)
            .ToList();
        return Task.FromResult(results);
    }

    public Task<int> GetCountAsync()
        => Task.FromResult(_state.State.Entries.Count);
}

/// <summary>
/// Persistent state for the external referral index singleton.
/// </summary>
[GenerateSerializer]
public class ExternalReferralIndexState
{
    [Id(0)]
    public Dictionary<string, ExternalReferralIndexEntry> Entries { get; set; } = new();
}
