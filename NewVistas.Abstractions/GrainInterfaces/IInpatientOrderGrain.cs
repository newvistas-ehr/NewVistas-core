// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Inpatient pharmacy order grain — VistA File #55 inpatient sub-file.
/// Grain key: order ID string (e.g. "PSJ-ORDER-{guid}").
/// Covers Unit Dose, IV admixture, and Large Volume Parenteral order types.
/// </summary>
public interface IInpatientOrderGrain : IGrainWithStringKey
{
    Task<InpatientOrderState> GetOrderAsync();

    Task CreateOrderAsync(
        string patientId,
        string wardId,
        string wardName,
        string? roomBed,
        string orderType,
        string drugName,
        string? drugId,
        string? dosage,
        string? doseUnit,
        string? route,
        string? schedule,
        string priority,
        DateTime? startDate,
        DateTime? stopDate,
        int? durationDays,
        int? quantityPerDose,
        string? providerId,
        string? providerName,
        string? comments,
        string? ivSolution,
        int? ivVolumeMl,
        string? infusionRateStr);

    Task VerifyAsync(string pharmacistId, string? pharmacistName);
    Task DiscontinueAsync(string reason);
    Task HoldAsync(string reason);
    Task ResumeAsync();
    Task ExpireAsync();
    Task AddIvAdditiveAsync(IvAdditive additive);
    Task AddScheduledTimeAsync(DateTime scheduledTime);
    Task RecordAdministrationAsync(string bcmaId, DateTime adminDate);
}
