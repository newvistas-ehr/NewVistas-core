// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Manages a single chart pull request.
/// Key pattern: "RT-REQUEST:{guid}".
/// VistA File #190.2 (RECORD TRACKING REQUEST). RTREQ.m
/// </summary>
public interface IChartRequestGrain : IGrainWithStringKey
{
    Task CreateRequestAsync(
        string patientId,
        string patientName,
        string requestedById,
        string requestedByName,
        DateTime neededBy,
        ChartRequestPriority priority,
        string requestedForLocation,
        ChartRequestType requestType,
        string notes);

    Task FulfillRequestAsync(string fulfilledBy);
    Task MarkInTransitAsync(string handledBy);
    Task MarkDeliveredAsync(string handledBy);
    Task MarkNotFoundAsync();
    Task CancelRequestAsync(string cancellationReason);
    Task<ChartRequestState> GetRequestAsync();
}
