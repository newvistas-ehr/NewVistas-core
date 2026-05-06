// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Lab EDI Queue Grain — manages the outbound order queue and inbound
/// result tracking for a single reference laboratory.
///
/// Key: "LAB-EDI-Q:{referenceLabId}"
/// Store: labEdiQueueStore
/// </summary>
public class LabEdiQueueGrain : Grain, ILabEdiQueueGrain
{
    private readonly IPersistentState<LabEdiQueueState> _state;

    public LabEdiQueueGrain(
        [PersistentState("labEdiQueue", "labEdiQueueStore")]
        IPersistentState<LabEdiQueueState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.ReferenceLabId))
        {
            _state.State.ReferenceLabId = this.GetPrimaryKeyString();
        }

        return base.OnActivateAsync(cancellationToken);
    }

    public async Task EnqueueOrderAsync(string orderId, string patientId, string patientName, DateTime queuedDate)
    {
        LabEdiQueueEntry entry = new()
        {
            EntryId = $"EDI-Q-{Guid.NewGuid():N}",
            OrderId = orderId,
            PatientId = patientId,
            PatientName = patientName,
            Direction = "OUTBOUND",
            Status = "PENDING",
            QueuedDate = queuedDate
        };

        _state.State.PendingOrders.Add(entry);
        _state.State.LastActivityDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public Task<List<LabEdiQueueEntry>> GetPendingOrdersAsync()
        => Task.FromResult(_state.State.PendingOrders
            .Where(o => o.Status == "PENDING")
            .OrderBy(o => o.QueuedDate)
            .ToList());

    public Task<List<LabEdiQueueEntry>> GetRecentResultsAsync(int maxResults)
        => Task.FromResult(_state.State.RecentResults
            .OrderByDescending(r => r.QueuedDate)
            .Take(maxResults)
            .ToList());

    public async Task RecordInboundResultAsync(string resultId, string orderId, string patientId, DateTime receivedDate)
    {
        LabEdiQueueEntry entry = new()
        {
            EntryId = resultId,
            OrderId = orderId,
            PatientId = patientId,
            Direction = "INBOUND",
            Status = "RECEIVED",
            QueuedDate = receivedDate,
            ProcessedDate = receivedDate
        };

        _state.State.RecentResults.Add(entry);
        _state.State.TotalResultsReceived++;
        _state.State.LastActivityDate = DateTime.UtcNow;

        // Trim recent results to last 200
        if (_state.State.RecentResults.Count > 200)
        {
            _state.State.RecentResults = _state.State.RecentResults
                .OrderByDescending(r => r.QueuedDate)
                .Take(200)
                .ToList();
        }

        await _state.WriteStateAsync();
    }

    public async Task MarkOrderSentAsync(string orderId, DateTime sentDate)
    {
        LabEdiQueueEntry? entry = _state.State.PendingOrders
            .FirstOrDefault(o => o.OrderId == orderId);

        if (entry != null)
        {
            entry.Status = "SENT";
            entry.ProcessedDate = sentDate;
        }

        _state.State.TotalOrdersSent++;
        _state.State.LastActivityDate = DateTime.UtcNow;

        // Remove sent orders from pending list
        _state.State.PendingOrders.RemoveAll(o => o.Status == "SENT");

        await _state.WriteStateAsync();
    }

    public Task<LabEdiQueueStatus> GetQueueStatusAsync()
    {
        DateTime today = DateTime.UtcNow.Date;

        LabEdiQueueStatus status = new()
        {
            PendingOutbound = _state.State.PendingOrders.Count(o => o.Status == "PENDING"),
            PendingInbound = _state.State.RecentResults.Count(r => r.Status == "RECEIVED"),
            SentToday = (int)_state.State.TotalOrdersSent, // simplified — full impl would track daily
            ReceivedToday = _state.State.RecentResults.Count(r => r.QueuedDate.Date == today),
            LastActivityDate = _state.State.LastActivityDate
        };

        return Task.FromResult(status);
    }
}
