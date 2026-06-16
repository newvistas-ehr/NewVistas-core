// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Women's Health Notification Grain — VistA File #790.
/// Key: "WH-NOTE:{guid}"
/// </summary>
public class WomensHealthNotificationGrain : Grain, IWomensHealthNotificationGrain
{
    private readonly IPersistentState<WomensHealthNotificationState> _state;

    public WomensHealthNotificationGrain(
        [PersistentState("womensHealthNotificationState", "womensHealthNotificationStore")]
        IPersistentState<WomensHealthNotificationState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.NotificationId))
        {
            _state.State.NotificationId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<WomensHealthNotificationState> GetAsync() => Task.FromResult(_state.State);

    public async Task CreateAsync(
        string patientId,
        WomensHealthNotificationType notificationType,
        DateTime procedureDate,
        string? providerId,
        string? providerName,
        string? locationId,
        string? locationName,
        MammographyResult? mammographyResult,
        int? biRadsScore,
        PapSmearResult? papSmearResult,
        string? contraceptiveMethod,
        int? gestationalAgeWeeks,
        DateTime? estimatedDueDate,
        string? pregnancyOutcome,
        bool followUpRequired,
        DateTime? nextDueDate,
        bool isRefusal,
        string? notes)
    {
        _state.State.PatientId = patientId;
        _state.State.NotificationType = notificationType;
        _state.State.ProcedureDate = procedureDate;
        _state.State.ProviderId = providerId;
        _state.State.ProviderName = providerName;
        _state.State.LocationId = locationId;
        _state.State.LocationName = locationName;
        _state.State.MammographyResult = mammographyResult;
        _state.State.BiRadsScore = biRadsScore;
        _state.State.PapSmearResult = papSmearResult;
        _state.State.ContraceptiveMethod = contraceptiveMethod;
        _state.State.GestationalAgeWeeks = gestationalAgeWeeks;
        _state.State.EstimatedDueDate = estimatedDueDate;
        _state.State.PregnancyOutcome = pregnancyOutcome;
        _state.State.FollowUpRequired = followUpRequired;
        _state.State.NextDueDate = nextDueDate;
        _state.State.IsRefusal = isRefusal;
        _state.State.Notes = notes;
        _state.State.Status = followUpRequired
            ? WomensHealthNotificationStatus.FollowUpRequired
            : WomensHealthNotificationStatus.Active;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CompleteAsync(DateTime? followUpCompletedDate, string? notes)
    {
        _state.State.Status = WomensHealthNotificationStatus.Completed;
        _state.State.FollowUpRequired = false;
        if (followUpCompletedDate.HasValue)
            _state.State.FollowUpCompletedDate = followUpCompletedDate;
        if (notes != null)
            _state.State.Notes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetFollowUpRequiredAsync(bool required, DateTime? nextDueDate)
    {
        _state.State.FollowUpRequired = required;
        _state.State.Status = required
            ? WomensHealthNotificationStatus.FollowUpRequired
            : WomensHealthNotificationStatus.Active;
        if (nextDueDate.HasValue)
            _state.State.NextDueDate = nextDueDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CancelAsync()
    {
        _state.State.Status = WomensHealthNotificationStatus.Cancelled;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
