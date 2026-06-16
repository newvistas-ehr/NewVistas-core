// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class ClinicProcedureGrain : Grain, IClinicProcedureGrain
{
    private readonly IPersistentState<ClinicProcedureState> _state;

    public ClinicProcedureGrain(
        [PersistentState("cpProcedureState", "cpProcedureStore")] IPersistentState<ClinicProcedureState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.ProcedureId))
        {
            _state.State.ProcedureId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<ClinicProcedureState> GetProcedureAsync() => Task.FromResult(_state.State);

    public async Task OrderProcedureAsync(
        string patientId,
        ClinicProcedureCategory category,
        string procedureCode,
        string procedureDescription,
        DateTime orderedDate,
        string? providerId,
        string? providerName,
        string? locationId,
        string? locationName,
        string? indication)
    {
        _state.State.PatientId = patientId;
        _state.State.Category = category;
        _state.State.ProcedureCode = procedureCode;
        _state.State.ProcedureDescription = procedureDescription;
        _state.State.OrderedDate = orderedDate;
        _state.State.ProviderId = providerId;
        _state.State.ProviderName = providerName;
        _state.State.LocationId = locationId;
        _state.State.LocationName = locationName;
        _state.State.Indication = indication;
        _state.State.Status = ClinicProcedureStatus.Ordered;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ScheduleProcedureAsync(DateTime scheduledDate)
    {
        _state.State.ScheduledDate = scheduledDate;
        _state.State.Status = ClinicProcedureStatus.Scheduled;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task BeginProcedureAsync(DateTime performedDate)
    {
        _state.State.PerformedDate = performedDate;
        _state.State.Status = ClinicProcedureStatus.InProgress;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CompleteProcedureAsync(
        DateTime performedDate,
        string? findings,
        string? impression,
        string? notes)
    {
        _state.State.PerformedDate = performedDate;
        _state.State.Findings = findings;
        _state.State.Impression = impression;
        _state.State.Notes = notes;
        _state.State.Status = ClinicProcedureStatus.Completed;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CancelProcedureAsync(string? reason)
    {
        _state.State.Status = ClinicProcedureStatus.Cancelled;
        _state.State.CancellationReason = reason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    // ── EEG ──────────────────────────────────────────────────────────────────

    public async Task RecordEegResultsAsync(
        int? durationMinutes,
        string? background,
        EegAlertType? alertType,
        bool? seizureActivity,
        string? focalRegion,
        List<string>? activations)
    {
        _state.State.EegDurationMinutes = durationMinutes;
        _state.State.EegBackground = background;
        _state.State.EegAlertType = alertType;
        _state.State.EegSeizureActivity = seizureActivity;
        _state.State.EegFocalRegion = focalRegion;
        _state.State.EegActivations = activations ?? new();
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    // ── EMG ──────────────────────────────────────────────────────────────────

    public async Task RecordEmgResultsAsync(
        List<string>? musclesStudied,
        EmgFindingType? findingType,
        string? spontaneousActivity,
        string? mupDescription)
    {
        _state.State.EmgMusclesStudied = musclesStudied ?? new();
        _state.State.EmgFindingType = findingType;
        _state.State.EmgSpontaneousActivity = spontaneousActivity;
        _state.State.EmgMupDescription = mupDescription;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    // ── NCS ──────────────────────────────────────────────────────────────────

    public async Task RecordNcsResultsAsync(
        List<string>? nervesStudied,
        decimal? meanMotorVelocity,
        decimal? meanSensoryVelocity,
        bool? fWavesObtained,
        EmgFindingType? findingType)
    {
        _state.State.NcsNervesStudied = nervesStudied ?? new();
        _state.State.NcsMeanMotorVelocity = meanMotorVelocity;
        _state.State.NcsMeanSensoryVelocity = meanSensoryVelocity;
        _state.State.NcsFWavesObtained = fWavesObtained;
        _state.State.NcsFindingType = findingType;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    // ── Sleep Study ───────────────────────────────────────────────────────────

    public async Task RecordSleepStudyResultsAsync(
        SleepStudyType studyType,
        SleepApneaType? apneaType,
        decimal? apneaHypopneaIndex,
        decimal? cpapPressureCmH2O,
        decimal? sleepEfficiencyPct,
        int? totalSleepTimeMin,
        decimal? sleepLatencyMin,
        decimal? remLatencyMin)
    {
        _state.State.SleepStudyType = studyType;
        _state.State.SleepApneaType = apneaType;
        _state.State.ApneaHypopneaIndex = apneaHypopneaIndex;
        _state.State.CpapPressureCmH2O = cpapPressureCmH2O;
        _state.State.SleepEfficiencyPct = sleepEfficiencyPct;
        _state.State.TotalSleepTimeMin = totalSleepTimeMin;
        _state.State.SleepLatencyMin = sleepLatencyMin;
        _state.State.RemLatencyMin = remLatencyMin;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    // ── Audiometry ────────────────────────────────────────────────────────────

    public async Task RecordAudiometryResultsAsync(
        HearingLossType? hearingLossType,
        decimal? rightEarPta,
        decimal? leftEarPta,
        decimal? speechDiscriminationRight,
        decimal? speechDiscriminationLeft,
        string? tympanometryRight,
        string? tympanometryLeft)
    {
        _state.State.HearingLossType = hearingLossType;
        _state.State.RightEarPta = rightEarPta;
        _state.State.LeftEarPta = leftEarPta;
        _state.State.SpeechDiscriminationRight = speechDiscriminationRight;
        _state.State.SpeechDiscriminationLeft = speechDiscriminationLeft;
        _state.State.TympanometryRight = tympanometryRight;
        _state.State.TympanometryLeft = tympanometryLeft;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
