// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Lab EDI Queue Grain Interface — manages the outbound order queue and
/// inbound result queue for a single reference laboratory.
///
/// Grain Key: "LAB-EDI-Q:{referenceLabId}"
///
/// Orders are enqueued when created and dequeued when transmitted.
/// Inbound results are recorded here for tracking and dashboard display.
/// </summary>
public interface ILabEdiQueueGrain : IGrainWithStringKey
{
    /// <summary>
    /// Enqueues a new outbound order for transmission to the reference lab.
    /// </summary>
    Task EnqueueOrderAsync(string orderId, string patientId, string patientName, DateTime queuedDate);

    /// <summary>
    /// Gets all pending outbound orders awaiting transmission.
    /// </summary>
    Task<List<LabEdiQueueEntry>> GetPendingOrdersAsync();

    /// <summary>
    /// Gets recent inbound results, limited to maxResults.
    /// </summary>
    Task<List<LabEdiQueueEntry>> GetRecentResultsAsync(int maxResults);

    /// <summary>
    /// Records an inbound result received from the reference lab.
    /// </summary>
    Task RecordInboundResultAsync(string resultId, string orderId, string patientId, DateTime receivedDate);

    /// <summary>
    /// Marks an outbound order as sent to the reference lab.
    /// </summary>
    Task MarkOrderSentAsync(string orderId, DateTime sentDate);

    /// <summary>
    /// Gets aggregate queue status for dashboard display.
    /// </summary>
    Task<LabEdiQueueStatus> GetQueueStatusAsync();
}
