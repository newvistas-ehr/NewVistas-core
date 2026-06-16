// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Events.Clinical.Labs;
using NewVistas.Abstractions.EventSourcing;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Lab Test Grain implementation based on VistA LAB DATA file (#63) and LABORATORY TEST file (#60)
/// </summary>
public class LabTestGrain : Grain, ILabTestGrain
{
    private readonly IPersistentState<LabTestState> _state;

    public LabTestGrain(
        [PersistentState("labTestState", "labTestStore")] IPersistentState<LabTestState> state)
    {
        _state = state;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.LabTestId))
        {
            _state.State.LabTestId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }

        await base.OnActivateAsync(cancellationToken);

        // Drain anything left in the clinical-event outbox from a prior crash.
        if (_state.State.PendingEvents.Count > 0)
            await this.DrainOutboxAsync(_state, GrainFactory);
    }

    private string? CurrentUserId => RequestContext.Get(RequestContextKeys.UserId) as string;
    private string? CurrentUserName => RequestContext.Get(RequestContextKeys.UserName) as string;

    public Task<LabTestState> GetLabTestAsync()
    {
        return Task.FromResult(_state.State);
    }

    public async Task OrderLabTestAsync(
        string patientId,
        string testId,
        string testName,
        string? testCode,
        string? orderId,
        string? orderingProviderId,
        string? orderingProviderName,
        string? specimenType,
        string? category)
    {
        // Idempotent: re-issued order on the same grain key is a no-op.
        if (!string.IsNullOrEmpty(_state.State.PatientId))
            return;

        _state.State.PatientId = patientId;
        _state.State.TestId = testId;
        _state.State.TestName = testName;
        _state.State.TestCode = testCode;
        _state.State.OrderId = orderId;
        _state.State.OrderingProviderId = orderingProviderId;
        _state.State.OrderingProviderName = orderingProviderName;
        _state.State.SpecimenType = specimenType;
        _state.State.Category = category;
        _state.State.Status = "Ordered";
        _state.State.LastModifiedDate = DateTime.UtcNow;

        var evt = new LabOrderedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = patientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            LabTestId = _state.State.LabTestId,
            Snapshot = _state.State.Clone()
        };
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await _state.WriteStateAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
    }

    public async Task CollectSpecimenAsync(
        DateTime collectionDateTime,
        string? collectionSample,
        string? performingLab)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId)) return;
        if (_state.State.CollectionDateTime.HasValue) return; // already collected

        _state.State.CollectionDateTime = collectionDateTime;
        _state.State.CollectionSample = collectionSample;
        _state.State.PerformingLab = performingLab;
        _state.State.Status = "Collected";
        _state.State.LastModifiedDate = DateTime.UtcNow;

        var evt = new SpecimenCollectedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = _state.State.PatientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            LabTestId = _state.State.LabTestId,
            CollectionDateTime = collectionDateTime,
            CollectionSample = collectionSample,
            PerformingLab = performingLab
        };
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await _state.WriteStateAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
    }

    public async Task RecordResultAsync(
        DateTime resultDateTime,
        string resultValue,
        string? resultUnit,
        string? referenceLow,
        string? referenceHigh,
        string? abnormalFlag)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId)) return;

        _state.State.ResultDateTime = resultDateTime;
        _state.State.ResultValue = resultValue;
        _state.State.ResultUnit = resultUnit;
        _state.State.ReferenceRangeLow = referenceLow;
        _state.State.ReferenceRangeHigh = referenceHigh;
        _state.State.AbnormalFlag = abnormalFlag;
        _state.State.Status = "Pending";
        _state.State.LastModifiedDate = DateTime.UtcNow;

        var evt = new LabResultRecordedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = _state.State.PatientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            LabTestId = _state.State.LabTestId,
            ResultDateTime = resultDateTime,
            ResultValue = resultValue,
            ResultUnit = resultUnit,
            ReferenceRangeLow = referenceLow,
            ReferenceRangeHigh = referenceHigh,
            AbnormalFlag = abnormalFlag
        };
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await _state.WriteStateAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
    }

    public async Task VerifyResultAsync(
        string verifyingProviderId,
        string verifyingProviderName,
        DateTime verifiedDateTime)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId)) return;
        if (_state.State.VerifiedDateTime.HasValue) return; // already verified

        _state.State.VerifyingProviderId = verifyingProviderId;
        _state.State.VerifyingProviderName = verifyingProviderName;
        _state.State.VerifiedDateTime = verifiedDateTime;
        _state.State.Status = "Completed";
        _state.State.LastModifiedDate = DateTime.UtcNow;

        var evt = new LabResultVerifiedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = _state.State.PatientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            LabTestId = _state.State.LabTestId,
            VerifyingProviderId = verifyingProviderId,
            VerifyingProviderName = verifyingProviderName,
            VerifiedDateTime = verifiedDateTime
        };
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await _state.WriteStateAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
    }

    public async Task UpdateStatusAsync(string status)
    {
        _state.State.Status = status;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task AddCommentsAsync(string comments)
    {
        _state.State.Comments = comments;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task MarkAsCriticalAsync()
    {
        _state.State.IsCritical = true;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task CancelLabTestAsync()
    {
        _state.State.Status = "Cancelled";
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public Task<string> GetPatientIdAsync()
    {
        return Task.FromResult(_state.State.PatientId);
    }

    public Task<string?> GetResultValueAsync()
    {
        return Task.FromResult(_state.State.ResultValue);
    }

    public Task<bool> IsAbnormalAsync()
    {
        var isAbnormal = !string.IsNullOrEmpty(_state.State.AbnormalFlag) && 
                        _state.State.AbnormalFlag != "Normal";
        return Task.FromResult(isAbnormal);
    }

    public Task<bool> IsCriticalAsync()
    {
        return Task.FromResult(_state.State.IsCritical);
    }
}
