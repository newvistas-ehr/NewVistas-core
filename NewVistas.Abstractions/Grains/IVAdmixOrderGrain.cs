// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

public class IVAdmixOrderGrain : Grain, IIVAdmixOrderGrain
{
    private readonly IPersistentState<IVAdmixOrderState> _state;

    public IVAdmixOrderGrain(
        [PersistentState("ivAdmixOrderState", "ivAdmixOrderStore")] IPersistentState<IVAdmixOrderState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.OrderId))
            _state.State.OrderId = this.GetPrimaryKeyString();
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<IVAdmixOrderState> GetOrderAsync() => Task.FromResult(_state.State);

    public async Task CreateOrderAsync(
        string patientId,
        string baseSolution,
        int baseSolutionVolumeMl,
        IVAdmixRoute route,
        IVAdmixFrequency frequency,
        IVContainerType containerType,
        int containerCount,
        IVAdmixPriority priority,
        string? linkedInpatientOrderId,
        string? infusionRateStr,
        decimal? infusionRateMlHr,
        decimal? infusionDurationHours,
        string? routeDescription,
        string? frequencyDescription,
        DateTime? startDateTime,
        DateTime? stopDateTime,
        string? providerId,
        string? providerName,
        string? notes)
    {
        _state.State.PatientId = patientId;
        _state.State.BaseSolution = baseSolution;
        _state.State.BaseSolutionVolumeMl = baseSolutionVolumeMl;
        _state.State.TotalVolumeMl = baseSolutionVolumeMl;
        _state.State.Route = route;
        _state.State.Frequency = frequency;
        _state.State.ContainerType = containerType;
        _state.State.ContainerCount = containerCount;
        _state.State.Priority = priority;
        _state.State.LinkedInpatientOrderId = linkedInpatientOrderId;
        _state.State.InfusionRateStr = infusionRateStr;
        _state.State.InfusionRateMlHr = infusionRateMlHr;
        _state.State.InfusionDurationHours = infusionDurationHours;
        _state.State.RouteDescription = routeDescription;
        _state.State.FrequencyDescription = frequencyDescription;
        _state.State.StartDateTime = startDateTime;
        _state.State.StopDateTime = stopDateTime;
        _state.State.ProviderId = providerId;
        _state.State.ProviderName = providerName;
        _state.State.Notes = notes;
        _state.State.Status = IVAdmixOrderStatus.Pending;
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddAdditiveAsync(IVAdmixAdditive additive)
    {
        _state.State.Additives.Add(additive);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RemoveAdditiveAsync(string drugName)
    {
        int idx = _state.State.Additives.FindIndex(a => a.DrugName == drugName);
        if (idx >= 0)
        {
            _state.State.Additives.RemoveAt(idx);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task VerifyOrderAsync(string pharmacistId, string pharmacistName, DateTime verifiedDate)
    {
        _state.State.PharmacistId = pharmacistId;
        _state.State.PharmacistName = pharmacistName;
        _state.State.VerifiedDate = verifiedDate;
        _state.State.Status = IVAdmixOrderStatus.Verified;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task StartCompoundingAsync(string compoundedById, string compoundedByName, DateTime startDate)
    {
        _state.State.CompoundedById = compoundedById;
        _state.State.CompoundedByName = compoundedByName;
        _state.State.CompoundingStartDate = startDate;
        _state.State.Status = IVAdmixOrderStatus.Compounding;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CompleteCompoundingAsync(DateTime completedDate, string? lotNumber, DateTime? expirationDate)
    {
        _state.State.CompoundingCompleteDate = completedDate;
        _state.State.LotNumber = lotNumber;
        _state.State.ExpirationDate = expirationDate;
        _state.State.Status = IVAdmixOrderStatus.Ready;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task PrintLabelAsync(string printedBy, DateTime printedDate)
    {
        _state.State.LabelPrinted = true;
        _state.State.LabelPrintedBy = printedBy;
        _state.State.LabelPrintedDate = printedDate;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DispenseOrderAsync(DateTime dispensingDateTime)
    {
        _state.State.DispensingDateTime = dispensingDateTime;
        _state.State.Status = IVAdmixOrderStatus.Dispensed;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordAdministrationAsync(DateTime administrationDateTime)
    {
        _state.State.AdministrationDateTime = administrationDateTime;
        _state.State.Status = IVAdmixOrderStatus.Administered;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DiscontinueOrderAsync(string reason)
    {
        _state.State.DiscontinuationReason = reason;
        _state.State.Status = IVAdmixOrderStatus.Discontinued;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CancelOrderAsync(string reason)
    {
        _state.State.CancellationReason = reason;
        _state.State.Status = IVAdmixOrderStatus.Cancelled;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UpdateScheduleAsync(DateTime? startDateTime, DateTime? stopDateTime)
    {
        _state.State.StartDateTime = startDateTime;
        _state.State.StopDateTime = stopDateTime;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetTotalVolumeAsync(int totalVolumeMl)
    {
        _state.State.TotalVolumeMl = totalVolumeMl;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
