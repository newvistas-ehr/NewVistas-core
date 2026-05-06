// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Implements <see cref="IDiabetesRegistryGrain"/>. See that interface for
/// behaviour. The grain holds raw observations; status/due-date computation
/// is centralised in <see cref="DiabetesRegistryRules"/> so the same rules
/// drive both the snapshot and the pre-visit plan.
/// </summary>
public class DiabetesRegistryGrain : Grain, IDiabetesRegistryGrain
{
    private const int MaxHbA1cHistory = 24;     // ~2 years of quarterly tests; adjust if a deployment wants more

    private readonly IPersistentState<DiabetesRegistryState> _state;

    public DiabetesRegistryGrain(
        [PersistentState("diabetesRegistryState", "diabetesRegistryStore")]
        IPersistentState<DiabetesRegistryState> state)
    {
        _state = state;
    }

    public Task<DiabetesRegistryState> GetAsync() => Task.FromResult(_state.State);

    public Task<DiabetesRegistrySnapshot> GetSnapshotAsync() =>
        Task.FromResult(DiabetesRegistryRules.BuildSnapshot(_state.State, DateTime.UtcNow));

    public Task<DiabetesPreVisitPlan> GetPreVisitPlanAsync(DateTime visitDate) =>
        Task.FromResult(DiabetesRegistryRules.BuildPreVisitPlan(_state.State, visitDate));

    public async Task EnrollAsync(string diabetesType, DateTime enrollmentDate)
    {
        if (string.IsNullOrWhiteSpace(diabetesType))
            throw new ArgumentException("diabetesType is required.", nameof(diabetesType));

        // Set ICN from grain key on first activation (key format "DM-REG:{icn}")
        if (string.IsNullOrEmpty(_state.State.Icn))
        {
            string key = this.GetPrimaryKeyString();
            _state.State.Icn = key.StartsWith("DM-REG:", StringComparison.Ordinal)
                ? key.Substring("DM-REG:".Length)
                : key;
        }

        if (!_state.State.IsEnrolled)
        {
            _state.State.IsEnrolled = true;
            _state.State.EnrollmentDate = enrollmentDate;
        }
        _state.State.DiabetesType = diabetesType;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordHbA1cAsync(decimal value, DateTime dateOfTest)
    {
        if (value < 0 || value > 25)
            throw new ArgumentOutOfRangeException(nameof(value), value,
                "HbA1c value must be between 0 and 25 (percent).");

        _state.State.HbA1cHistory.Add(new HbA1cReading
        {
            Value = value,
            DateOfTest = dateOfTest,
        });

        // Keep history bounded; oldest-first ordering preserved by removing from the front.
        while (_state.State.HbA1cHistory.Count > MaxHbA1cHistory)
            _state.State.HbA1cHistory.RemoveAt(0);

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordFootExamAsync(DateTime dateOfExam, string? providerName)
    {
        // Only update if newer than what's on file (out-of-order recording is allowed
        // but doesn't move the most-recent date backwards).
        if (_state.State.LastFootExamDate is null || dateOfExam > _state.State.LastFootExamDate.Value)
        {
            _state.State.LastFootExamDate = dateOfExam;
            _state.State.LastFootExamProviderName = providerName;
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task RecordEyeExamAsync(DateTime dateOfExam, string? providerName)
    {
        if (_state.State.LastEyeExamDate is null || dateOfExam > _state.State.LastEyeExamDate.Value)
        {
            _state.State.LastEyeExamDate = dateOfExam;
            _state.State.LastEyeExamProviderName = providerName;
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task RecordEgfrAsync(decimal eGfrValue, DateTime dateOfTest)
    {
        if (_state.State.LastEgfrDate is null || dateOfTest > _state.State.LastEgfrDate.Value)
        {
            _state.State.LastEgfr = eGfrValue;
            _state.State.LastEgfrDate = dateOfTest;
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task RecordAcrAsync(decimal acrValue, DateTime dateOfTest)
    {
        if (_state.State.LastAcrDate is null || dateOfTest > _state.State.LastAcrDate.Value)
        {
            _state.State.LastAcrMgPerGram = acrValue;
            _state.State.LastAcrDate = dateOfTest;
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }
}

/// <summary>
/// Singleton index of diabetes-registry enrollees. Cohort enumeration for
/// population-health workflows and GPRA aggregation.
/// </summary>
public class DiabetesRegistryIndexGrain : Grain, IDiabetesRegistryIndexGrain
{
    private readonly IPersistentState<DiabetesRegistryIndexState> _state;

    public DiabetesRegistryIndexGrain(
        [PersistentState("diabetesRegistryIndexState", "diabetesRegistryIndexStore")]
        IPersistentState<DiabetesRegistryIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(string icn, DateTime enrollmentDate)
    {
        if (string.IsNullOrWhiteSpace(icn))
            throw new ArgumentException("icn is required.", nameof(icn));
        // Only set the enrollment date on first add — subsequent calls preserve it.
        if (!_state.State.EnrolledIcns.ContainsKey(icn))
        {
            _state.State.EnrolledIcns[icn] = enrollmentDate;
            await _state.WriteStateAsync();
        }
    }

    public Task<List<string>> GetEnrolledIcnsAsync() =>
        Task.FromResult(_state.State.EnrolledIcns.Keys.ToList());

    public Task<int> GetCountAsync() =>
        Task.FromResult(_state.State.EnrolledIcns.Count);
}
