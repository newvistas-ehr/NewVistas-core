// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.PT.GrainInterfaces;
using NewVistas.PT.GrainStates;
using NewVistas.PT.Models;

namespace NewVistas.PT.Grains;

/// <summary>
/// Per-patient index of PT referral grain keys.
/// Maintains entries sorted by ReferralDate descending (most recent first).
/// </summary>
public class PTReferralIndexGrain : Grain, IPTReferralIndexGrain
{
    private readonly IPersistentState<PTReferralIndexState> _state;

    public PTReferralIndexGrain(
        [PersistentState("ptReferralIndexState", "physTherapyReferralIndexStore")]
        IPersistentState<PTReferralIndexState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId))
        {
            // Key format: "PTREF-IDX:{patientId}"
            string key = this.GetPrimaryKeyString();
            string[] parts = key.Split(':');
            if (parts.Length >= 2)
                _state.State.PatientId = parts[1];
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task AddOrUpdateAsync(PTReferralIndexEntry entry)
    {
        int existingIndex = _state.State.Entries.FindIndex(
            e => e.ReferralGrainKey == entry.ReferralGrainKey);

        if (existingIndex >= 0)
        {
            _state.State.Entries[existingIndex] = entry;
        }
        else
        {
            // Insert in sorted position (descending by ReferralDate)
            int insertIndex = _state.State.Entries.FindIndex(
                e => e.ReferralDate <= entry.ReferralDate);
            if (insertIndex < 0)
                _state.State.Entries.Add(entry);
            else
                _state.State.Entries.Insert(insertIndex, entry);
        }

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<List<PTReferralIndexEntry>> GetAllReferralsAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<PTReferralIndexEntry>> GetActiveReferralsAsync()
        => Task.FromResult(
            _state.State.Entries.Where(e => e.Status == PTReferralStatus.Active).ToList());

    public Task<int> GetCountAsync()
        => Task.FromResult(_state.State.Entries.Count);
}
