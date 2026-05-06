// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

public class IncomeThresholdGrain : Grain, IIncomeThresholdGrain
{
    private readonly IPersistentState<IncomeThresholdState> _state;

    public IncomeThresholdGrain(
        [PersistentState("incomeThresholdState", "incomeThresholdStore")]
        IPersistentState<IncomeThresholdState> state)
    {
        _state = state;
    }

    public Task<IncomeThresholdState> GetAsync()
        => Task.FromResult(_state.State);

    public Task<List<IncomeThresholdEntry>> GetByYearAsync(int year)
        => Task.FromResult(_state.State.Entries.Where(e => e.FiscalYear == year).ToList());

    public async Task SeedDefaultsAsync(int year)
    {
        // Idempotent: skip if this year is already seeded
        if (_state.State.Entries.Any(e => e.FiscalYear == year))
            return;

        // FY 2024 VA representative income threshold values
        // Source: VA Health Benefits — Income Thresholds (VistA File #408.15)
        // 4 categories × 8 household sizes = 32 entries per fiscal year

        decimal[] gmtLow  = [ 16_037m, 18_792m, 21_547m, 24_302m, 27_057m, 29_812m, 32_567m, 35_322m ];
        decimal[] gmtMed  = [ 19_244m, 22_550m, 25_856m, 29_162m, 32_468m, 35_774m, 39_080m, 42_386m ];
        decimal[] gmtHigh = [ 32_280m, 35_116m, 37_952m, 40_788m, 43_624m, 46_460m, 49_296m, 52_132m ];
        decimal[] hecCopay= [ 36_433m, 39_620m, 42_807m, 45_994m, 49_181m, 52_368m, 55_555m, 58_742m ];

        string[] categories = [ "GMT_LOW", "GMT_MED", "GMT_HIGH", "HEC_COPAY" ];
        decimal[][] amounts  = [ gmtLow, gmtMed, gmtHigh, hecCopay ];

        for (int c = 0; c < categories.Length; c++)
        {
            for (int h = 1; h <= 8; h++)
            {
                _state.State.Entries.Add(new IncomeThresholdEntry
                {
                    EntryId         = $"{year}-{categories[c]}-{h}",
                    FiscalYear      = year,
                    Category        = categories[c],
                    HouseholdSize   = h,
                    ThresholdAmount = amounts[c][h - 1],
                });
            }
        }

        _state.State.LastSeededYear  = year;
        _state.State.LastUpdatedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<decimal?> LookupThresholdAsync(int year, string category, int householdSize)
    {
        string entryId = $"{year}-{category}-{householdSize}";
        IncomeThresholdEntry? entry = _state.State.Entries.FirstOrDefault(e => e.EntryId == entryId);
        return Task.FromResult(entry?.ThresholdAmount);
    }

    public async Task SetThresholdAsync(int year, string category, int householdSize, decimal amount)
    {
        string entryId = $"{year}-{category}-{householdSize}";
        int idx = _state.State.Entries.FindIndex(e => e.EntryId == entryId);

        IncomeThresholdEntry entry = new()
        {
            EntryId         = entryId,
            FiscalYear      = year,
            Category        = category,
            HouseholdSize   = householdSize,
            ThresholdAmount = amount,
        };

        if (idx >= 0)
            _state.State.Entries[idx] = entry;
        else
            _state.State.Entries.Add(entry);

        _state.State.LastUpdatedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
