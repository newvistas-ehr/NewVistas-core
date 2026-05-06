// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Inpatient pharmacy order grain — VistA File #55 inpatient sub-file.
/// </summary>
public class InpatientOrderGrain : Grain, IInpatientOrderGrain
{
    private readonly IPersistentState<InpatientOrderState> _state;

    public InpatientOrderGrain(
        [PersistentState("inpatientOrderState", "inpatientOrderStore")]
        IPersistentState<InpatientOrderState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.OrderId))
        {
            _state.State.OrderId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.LastModifiedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<InpatientOrderState> GetOrderAsync() => Task.FromResult(_state.State);

    public async Task CreateOrderAsync(
        string patientId, string wardId, string wardName, string? roomBed,
        string orderType, string drugName, string? drugId,
        string? dosage, string? doseUnit, string? route, string? schedule,
        string priority, DateTime? startDate, DateTime? stopDate, int? durationDays,
        int? quantityPerDose, string? providerId, string? providerName, string? comments,
        string? ivSolution, int? ivVolumeMl, string? infusionRateStr)
    {
        _state.State.PatientId = patientId;
        _state.State.WardId = wardId;
        _state.State.WardName = wardName;
        _state.State.RoomBed = roomBed;
        _state.State.OrderType = orderType;
        _state.State.DrugName = drugName;
        _state.State.DrugId = drugId;
        _state.State.Dosage = dosage;
        _state.State.DoseUnit = doseUnit;
        _state.State.Route = route;
        _state.State.Schedule = schedule;
        _state.State.Priority = priority;
        _state.State.StartDate = startDate ?? DateTime.UtcNow;
        _state.State.StopDate = stopDate;
        _state.State.DurationDays = durationDays;
        _state.State.QuantityPerDose = quantityPerDose;
        _state.State.ProviderId = providerId;
        _state.State.ProviderName = providerName;
        _state.State.Comments = comments;
        _state.State.IvSolution = ivSolution;
        _state.State.IvVolumeMl = ivVolumeMl;
        _state.State.InfusionRateStr = infusionRateStr;
        _state.State.Status = "PENDING";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task VerifyAsync(string pharmacistId, string? pharmacistName)
    {
        _state.State.IsVerified = true;
        _state.State.VerifiedBy = pharmacistId;
        _state.State.VerifiedDate = DateTime.UtcNow;
        _state.State.PharmacistId = pharmacistId;
        _state.State.PharmacistName = pharmacistName;
        _state.State.Status = "ACTIVE";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DiscontinueAsync(string reason)
    {
        _state.State.Status = "DISCONTINUED";
        _state.State.DiscontinueReason = reason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task HoldAsync(string reason)
    {
        _state.State.Status = "HOLD";
        _state.State.HoldReason = reason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ResumeAsync()
    {
        _state.State.Status = "ACTIVE";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ExpireAsync()
    {
        _state.State.Status = "EXPIRED";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddIvAdditiveAsync(IvAdditive additive)
    {
        _state.State.IvAdditives.Add(additive);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddScheduledTimeAsync(DateTime scheduledTime)
    {
        _state.State.ScheduledAdminTimes.Add(scheduledTime);
        _state.State.ScheduledAdminTimes.Sort();
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordAdministrationAsync(string bcmaId, DateTime adminDate)
    {
        if (!_state.State.BcmaAdministrationIds.Contains(bcmaId))
            _state.State.BcmaAdministrationIds.Add(bcmaId);
        _state.State.LastAdminDate = adminDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
