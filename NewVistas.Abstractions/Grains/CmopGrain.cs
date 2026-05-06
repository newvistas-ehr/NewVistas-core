// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans.Runtime;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// CMOP Transmission Grain — manages a single batch transmission.
/// </summary>
public class CmopTransmissionGrain : Grain, ICmopTransmissionGrain
{
    private readonly IPersistentState<CmopTransmissionState> _state;

    public CmopTransmissionGrain(
        [PersistentState("cmopTransmission", "cmopTransmissionStore")] IPersistentState<CmopTransmissionState> state)
    {
        _state = state;
    }

    public Task<CmopTransmissionState> GetTransmissionAsync()
        => Task.FromResult(_state.State);

    public async Task CreateTransmissionAsync(
        string cmopFacilityId, string cmopFacilityName,
        string originatingSiteId, List<CmopPrescriptionEntry> prescriptions)
    {
        _state.State.TransmissionId = this.GetPrimaryKeyString().Replace("CMOP-TX:", "");
        _state.State.CmopFacilityId = cmopFacilityId;
        _state.State.CmopFacilityName = cmopFacilityName;
        _state.State.OriginatingSiteId = originatingSiteId;
        _state.State.Prescriptions = prescriptions;
        _state.State.PrescriptionCount = prescriptions.Count;
        _state.State.Status = "PENDING";
        _state.State.CreatedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task TransmitAsync()
    {
        _state.State.Status = "TRANSMITTED";
        _state.State.TransmittedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AcknowledgeReceiptAsync()
    {
        _state.State.Status = "RECEIVED";
        _state.State.ReceivedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordDispensedAsync(int dispensedCount, int rejectedCount, string? errorMessage)
    {
        _state.State.Status = "DISPENSED";
        _state.State.DispensedCount = dispensedCount;
        _state.State.RejectedCount = rejectedCount;
        _state.State.ErrorMessage = errorMessage;
        _state.State.DispensedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordShippedAsync(string trackingNumber, string carrier)
    {
        _state.State.Status = "SHIPPED";
        _state.State.TrackingNumber = trackingNumber;
        _state.State.ShippingCarrier = carrier;
        _state.State.ShippedDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CompleteAsync()
    {
        _state.State.Status = "COMPLETED";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CancelAsync(string reason)
    {
        _state.State.Status = "CANCELLED";
        _state.State.ErrorMessage = reason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RejectItemAsync(string prescriptionId, string reason)
    {
        var item = _state.State.Prescriptions.Find(p => p.PrescriptionId == prescriptionId);
        if (item != null)
        {
            item.ItemStatus = "REJECTED";
            item.RejectReason = reason;
            _state.State.RejectedCount++;
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }
}

/// <summary>
/// CMOP Suspense Queue Grain — manages queued prescriptions for mail-order.
/// </summary>
public class CmopSuspenseGrain : Grain, ICmopSuspenseGrain
{
    private readonly IPersistentState<CmopSuspenseState> _state;
    private readonly IGrainFactory _grainFactory;

    public CmopSuspenseGrain(
        [PersistentState("cmopSuspense", "cmopSuspenseStore")] IPersistentState<CmopSuspenseState> state,
        IGrainFactory grainFactory)
    {
        _state = state;
        _grainFactory = grainFactory;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        string key = this.GetPrimaryKeyString();
        _state.State.SiteId = key.Replace("CMOP-SUSPENSE:", "");
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<CmopSuspenseState> GetSuspenseAsync()
        => Task.FromResult(_state.State);

    public Task<List<CmopSuspenseEntry>> GetQueuedPrescriptionsAsync()
        => Task.FromResult(_state.State.QueuedPrescriptions);

    public Task<int> GetQueueCountAsync()
        => Task.FromResult(_state.State.QueuedPrescriptions.Count);

    public async Task AddToSuspenseAsync(CmopSuspenseEntry entry)
    {
        // Prevent duplicates
        if (!_state.State.QueuedPrescriptions.Exists(e => e.PrescriptionId == entry.PrescriptionId))
        {
            _state.State.QueuedPrescriptions.Add(entry);
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveFromSuspenseAsync(string prescriptionId)
    {
        _state.State.QueuedPrescriptions.RemoveAll(e => e.PrescriptionId == prescriptionId);
        await _state.WriteStateAsync();
    }

    public async Task ClearQueueAsync()
    {
        _state.State.QueuedPrescriptions.Clear();
        await _state.WriteStateAsync();
    }

    public async Task SetAutoTransmitAsync(bool enabled, int? hour)
    {
        _state.State.AutoTransmitEnabled = enabled;
        if (hour.HasValue) _state.State.AutoTransmitHour = hour.Value;
        await _state.WriteStateAsync();
    }

    public async Task<string> TransmitQueueAsync(string cmopFacilityId, string cmopFacilityName)
    {
        List<CmopSuspenseEntry> queued = _state.State.QueuedPrescriptions;
        if (queued.Count == 0)
            throw new InvalidOperationException("Suspense queue is empty — nothing to transmit.");

        string txId = $"CMOP-TX-{Guid.NewGuid():N}";
        var prescriptions = queued.Select(e => new CmopPrescriptionEntry
        {
            PrescriptionId = e.PrescriptionId,
            PatientId = e.PatientId,
            PatientName = e.PatientName,
            DrugName = e.DrugName,
            RxNumber = e.RxNumber,
            Quantity = e.Quantity,
            DaysSupply = e.DaysSupply,
            FillType = e.FillType,
            ItemStatus = "PENDING"
        }).ToList();

        // Create the transmission grain
        var txGrain = _grainFactory.GetGrain<ICmopTransmissionGrain>($"CMOP-TX:{txId}");
        await txGrain.CreateTransmissionAsync(cmopFacilityId, cmopFacilityName, _state.State.SiteId, prescriptions);
        await txGrain.TransmitAsync();

        // Update the index
        var indexGrain = _grainFactory.GetGrain<ICmopTransmissionIndexGrain>($"CMOP-TX-INDEX:{_state.State.SiteId}");
        await indexGrain.AddOrUpdateAsync(new CmopTransmissionSummary
        {
            TransmissionId = txId,
            CmopFacilityName = cmopFacilityName,
            Status = "TRANSMITTED",
            TransmittedDate = DateTime.UtcNow,
            PrescriptionCount = prescriptions.Count
        });

        // Clear the queue
        _state.State.QueuedPrescriptions.Clear();
        _state.State.LastAutoTransmitDate = DateTime.UtcNow;
        await _state.WriteStateAsync();

        return txId;
    }
}

/// <summary>
/// CMOP Transmission Index Grain — lightweight index of transmissions per site.
/// </summary>
public class CmopTransmissionIndexGrain : Grain, ICmopTransmissionIndexGrain
{
    private readonly IPersistentState<CmopTransmissionIndexState> _state;

    public CmopTransmissionIndexGrain(
        [PersistentState("cmopTransmissionIndex", "cmopTransmissionIndexStore")] IPersistentState<CmopTransmissionIndexState> state)
    {
        _state = state;
    }

    public Task<List<CmopTransmissionSummary>> GetTransmissionsAsync()
        => Task.FromResult(_state.State.Transmissions);

    public Task<List<CmopTransmissionSummary>> GetTransmissionsByStatusAsync(string status)
        => Task.FromResult(_state.State.Transmissions.Where(t => t.Status == status).ToList());

    public async Task AddOrUpdateAsync(CmopTransmissionSummary summary)
    {
        int idx = _state.State.Transmissions.FindIndex(t => t.TransmissionId == summary.TransmissionId);
        if (idx >= 0)
            _state.State.Transmissions[idx] = summary;
        else
            _state.State.Transmissions.Add(summary);
        await _state.WriteStateAsync();
    }

    public async Task RemoveAsync(string transmissionId)
    {
        _state.State.Transmissions.RemoveAll(t => t.TransmissionId == transmissionId);
        await _state.WriteStateAsync();
    }
}
