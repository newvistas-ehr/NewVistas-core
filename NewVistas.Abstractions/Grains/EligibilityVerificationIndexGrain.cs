// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class EligibilityVerificationIndexGrain : Grain, IEligibilityVerificationIndexGrain
{
    private readonly IPersistentState<EligibilityVerificationIndexState> _state;

    public EligibilityVerificationIndexGrain(
        [PersistentState("eligibilityVerificationIndexState", "eligibilityVerificationIndexStore")]
        IPersistentState<EligibilityVerificationIndexState> state)
    {
        _state = state;
    }

    public Task<EligibilityVerificationIndexState> GetAsync()
        => Task.FromResult(_state.State);

    public Task<List<EligibilityVerificationIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public async Task AddOrUpdateAsync(EligibilityVerificationIndexEntry entry)
    {
        _state.State.Entries.RemoveAll(e => e.InquiryId == entry.InquiryId);
        _state.State.Entries.Insert(0, entry); // newest first
        await _state.WriteStateAsync();
    }

    public Task<List<EligibilityVerificationIndexEntry>> GetEligibleAsync()
        => Task.FromResult(
            _state.State.Entries
                .Where(e => e.Status == EligibilityInquiryStatus.Eligible)
                .ToList());

    public Task<EligibilityVerificationIndexEntry?> GetLatestForPayerAsync(string payerName)
        => Task.FromResult(
            _state.State.Entries
                .FirstOrDefault(e => e.PayerName.Equals(payerName, StringComparison.OrdinalIgnoreCase)));
}
