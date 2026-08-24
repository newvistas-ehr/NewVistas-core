// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Incomplete Record Grain — manages a single chart deficiency.
/// </summary>
public class IncompleteRecordGrain : Grain, IIncompleteRecordGrain
{
    private readonly IPersistentState<IncompleteRecordState> _state;

    public IncompleteRecordGrain(
        [PersistentState("incompleteRecord", "incompleteRecordStore")] IPersistentState<IncompleteRecordState> state)
    {
        _state = state;
    }

    public Task<IncompleteRecordState> GetDeficiencyAsync() => Task.FromResult(_state.State);

    public async Task CreateDeficiencyAsync(
        string patientId, string patientName,
        string providerId, string providerName,
        string deficiencyType, string description,
        string? documentId, string? admissionId,
        DateTime? dischargeDate, int delinquentAfterDays)
    {
        _state.State.DeficiencyId = this.GetPrimaryKeyString().Replace("DGPT:", "");
        _state.State.PatientId = patientId;
        _state.State.PatientName = patientName;
        _state.State.ProviderId = providerId;
        _state.State.ProviderName = providerName;
        _state.State.DeficiencyType = deficiencyType;
        _state.State.Description = description;
        _state.State.DocumentId = documentId;
        _state.State.AdmissionId = admissionId;
        _state.State.DischargeDate = dischargeDate;
        _state.State.DelinquentAfterDays = delinquentAfterDays;
        _state.State.Status = "OPEN";

        DateTime refDate = dischargeDate ?? DateTime.UtcNow;
        _state.State.DaysOutstanding = (int)(DateTime.UtcNow - refDate).TotalDays;
        if (_state.State.DaysOutstanding > delinquentAfterDays)
            _state.State.Status = "DELINQUENT";

        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CompleteAsync(string completedBy)
    {
        _state.State.Status = "COMPLETED";
        _state.State.CompletedDate = DateTime.UtcNow;
        _state.State.CompletedBy = completedBy;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task WaiveAsync(string reason, string authorizedBy)
    {
        _state.State.Status = "WAIVED";
        _state.State.WaiverReason = reason;
        _state.State.CompletedBy = authorizedBy;
        _state.State.CompletedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateDaysOutstandingAsync()
    {
        DateTime refDate = _state.State.DischargeDate ?? _state.State.CreatedDate;
        _state.State.DaysOutstanding = (int)(DateTime.UtcNow - refDate).TotalDays;
        if (_state.State.Status == "OPEN" && _state.State.DaysOutstanding > _state.State.DelinquentAfterDays)
            _state.State.Status = "DELINQUENT";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}

/// <summary>
/// Incomplete Record Index Grain — per-provider deficiency tracker.
/// </summary>
public class IncompleteRecordIndexGrain : Grain, IIncompleteRecordIndexGrain
{
    /// <inheritdoc />
    /// <remarks>Moved here from IncompleteRecordsController so every client seeds via one grain call.</remarks>
    public async Task SeedDemoDataAsync()
    {
        // The index is keyed "DGPT-INDEX:{providerId}" - seed for whichever provider this is.
        string providerId = this.GetPrimaryKeyString().Replace("DGPT-INDEX:", string.Empty);

        (string PatId, string PatName, string Type, string Desc, int DaysAgo, int DelinqDays)[] demo =
        {
            ("PATIENT-001", "JONES,MICHAEL R", "UNSIGNED_NOTE", "Unsigned progress note from 02/15", 5, 30),
            ("PATIENT-002", "SMITH,SARAH L", "MISSING_DISCHARGE_SUMMARY", "Discharge summary not dictated", 25, 30),
            ("PATIENT-003", "WILLIAMS,JAMES T", "MISSING_HP", "History and Physical not completed within 24h", 35, 30),
            ("PATIENT-001", "JONES,MICHAEL R", "UNSIGNED_ORDER", "Verbal order not cosigned within 48h", 3, 2),
            ("PATIENT-004", "BROWN,PATRICIA A", "MISSING_OP_NOTE", "Operative note not dictated within 24h", 10, 1),
        };

        foreach (var d in demo)
        {
            string defId = $"DGPT-DEMO-{Guid.NewGuid():N}";
            var grain = GrainFactory.GetGrain<IIncompleteRecordGrain>($"DGPT:{defId}");
            await grain.CreateDeficiencyAsync(
                d.PatId, d.PatName, providerId, "DR. MARTINEZ", d.Type, d.Desc,
                null, null, DateTime.UtcNow.AddDays(-d.DaysAgo), d.DelinqDays);

            IncompleteRecordState state = await grain.GetDeficiencyAsync();
            await AddOrUpdateAsync(new IncompleteRecordEntry
            {
                DeficiencyId = state.DeficiencyId,
                PatientName = state.PatientName,
                ProviderName = state.ProviderName,
                DeficiencyType = state.DeficiencyType,
                Status = state.Status,
                DaysOutstanding = state.DaysOutstanding,
                IsDelinquent = state.Status == "DELINQUENT" || state.DaysOutstanding > state.DelinquentAfterDays,
                DischargeDate = state.DischargeDate,
            });
        }
    }

    private readonly IPersistentState<IncompleteRecordIndexState> _state;

    public IncompleteRecordIndexGrain(
        [PersistentState("incompleteRecordIndex", "incompleteRecordIndexStore")] IPersistentState<IncompleteRecordIndexState> state)
    {
        _state = state;
    }

    public Task<List<IncompleteRecordEntry>> GetAllDeficienciesAsync()
        => Task.FromResult(_state.State.Deficiencies);

    public Task<List<IncompleteRecordEntry>> GetOpenDeficienciesAsync()
        => Task.FromResult(_state.State.Deficiencies.Where(d => d.Status == "OPEN" || d.Status == "DELINQUENT").ToList());

    public Task<List<IncompleteRecordEntry>> GetDelinquentDeficienciesAsync()
        => Task.FromResult(_state.State.Deficiencies.Where(d => d.IsDelinquent).ToList());

    public async Task AddOrUpdateAsync(IncompleteRecordEntry entry)
    {
        int idx = _state.State.Deficiencies.FindIndex(d => d.DeficiencyId == entry.DeficiencyId);
        if (idx >= 0) _state.State.Deficiencies[idx] = entry;
        else _state.State.Deficiencies.Add(entry);
        await _state.WriteStateAsync();
    }

    public async Task RemoveAsync(string deficiencyId)
    {
        _state.State.Deficiencies.RemoveAll(d => d.DeficiencyId == deficiencyId);
        await _state.WriteStateAsync();
    }
}
