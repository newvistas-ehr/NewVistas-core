// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Manages the physical chart record for a single patient.
/// Key pattern: "RT-CHART:{patientId}" — one chart per patient.
/// VistA File #190 (RECORD TRACKING). RTOUT.m, RTIN.m
/// </summary>
public interface IChartGrain : IGrainWithStringKey
{
    Task InitializeChartAsync(string patientId, string patientName, string chartNumber, string homeLocation);

    Task CheckOutChartAsync(
        string borrowerId,
        string borrowerName,
        string location,
        ChartLocationType locationType,
        DateTime? expectedReturnDate,
        string handledBy);

    Task CheckInChartAsync(string handledBy);

    Task TransferChartAsync(
        string newLocation,
        ChartLocationType newLocationType,
        string newBorrowerId,
        string newBorrowerName,
        string handledBy);

    Task SetRequestFlagAsync(bool isOnRequest);
    Task AddVolumeAsync(int volumeNumber, string dateRange);
    Task MarkChartLostAsync(string notes, string handledBy);
    Task MarkChartFoundAsync(string location, ChartLocationType locationType, string handledBy);
    Task<ChartState> GetChartAsync();
}
