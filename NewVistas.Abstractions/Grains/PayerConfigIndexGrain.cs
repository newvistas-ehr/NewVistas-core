// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class PayerConfigIndexGrain : Grain, IPayerConfigIndexGrain
{
    private readonly IPersistentState<PayerConfigIndexState> _state;

    public PayerConfigIndexGrain(
        [PersistentState("payerConfigIndexState", "payerConfigIndexStore")]
        IPersistentState<PayerConfigIndexState> state)
    {
        _state = state;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (_state.State.Entries.Count == 0)
        {
            // Self-seed with demo payer configurations
            _state.State.Entries = new List<PayerConfigIndexEntry>
            {
                new() { PayerId = "PAYER-MEDICARE", PayerName = "Medicare Part A/B", SupportsRealTimeEligibility = true, IsActive = true },
                new() { PayerId = "PAYER-MEDICAID", PayerName = "Medicaid (State)", SupportsRealTimeEligibility = true, IsActive = true },
                new() { PayerId = "PAYER-TRICARE", PayerName = "TRICARE", SupportsRealTimeEligibility = true, IsActive = true },
                new() { PayerId = "PAYER-CHAMPVA", PayerName = "CHAMPVA", SupportsRealTimeEligibility = true, IsActive = true },
                new() { PayerId = "PAYER-BCBS", PayerName = "Blue Cross Blue Shield", SupportsRealTimeEligibility = true, IsActive = true },
                new() { PayerId = "PAYER-AETNA", PayerName = "Aetna", SupportsRealTimeEligibility = true, IsActive = true },
                new() { PayerId = "PAYER-CIGNA", PayerName = "Cigna Healthcare", SupportsRealTimeEligibility = false, IsActive = true },
                new() { PayerId = "PAYER-UNITED", PayerName = "UnitedHealthcare", SupportsRealTimeEligibility = true, IsActive = true },
            };
            await _state.WriteStateAsync();
        }
        await base.OnActivateAsync(cancellationToken);
    }

    public Task<List<PayerConfigIndexEntry>> GetAllAsync()
        => Task.FromResult(_state.State.Entries);

    public Task<List<PayerConfigIndexEntry>> SearchAsync(string query)
        => Task.FromResult(
            _state.State.Entries
                .Where(e => e.PayerName.Contains(query, StringComparison.OrdinalIgnoreCase)
                         || e.PayerId.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList());

    public Task<List<PayerConfigIndexEntry>> GetRealTimePayersAsync()
        => Task.FromResult(
            _state.State.Entries
                .Where(e => e.SupportsRealTimeEligibility && e.IsActive)
                .ToList());

    public async Task AddOrUpdateAsync(PayerConfigIndexEntry entry)
    {
        _state.State.Entries.RemoveAll(e => e.PayerId == entry.PayerId);
        _state.State.Entries.Add(entry);
        await _state.WriteStateAsync();
    }

    public async Task RemoveAsync(string payerId)
    {
        _state.State.Entries.RemoveAll(e => e.PayerId == payerId);
        await _state.WriteStateAsync();
    }
}
