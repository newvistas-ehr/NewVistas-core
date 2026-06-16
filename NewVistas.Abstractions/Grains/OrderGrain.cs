// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Events.Clinical.Orders;
using NewVistas.Abstractions.EventSourcing;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Order Grain implementation based on VistA ORDER file (#100)
/// </summary>
public class OrderGrain : Grain, IOrderGrain
{
    private readonly IPersistentState<OrderState> _state;

    public OrderGrain(
        [PersistentState("orderState", "orderStore")] IPersistentState<OrderState> state)
    {
        _state = state;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.OrderId))
        {
            _state.State.OrderId = this.GetPrimaryKeyString();
            _state.State.OrderDateTime = DateTime.UtcNow;
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

    public Task<OrderState> GetOrderAsync()
    {
        return Task.FromResult(_state.State);
    }

    public async Task CreateOrderAsync(
        string patientId,
        string orderType,
        string orderableItem,
        string? orderableItemId,
        string providerId,
        string providerName,
        DateTime startDateTime,
        string? locationId,
        string? locationName,
        string urgency,
        string? instructions,
        string? reason,
        string? nature,
        string? createdBy)
    {
        // Idempotent: a re-issued create call for the same order is a no-op.
        if (!string.IsNullOrEmpty(_state.State.PatientId))
            return;

        _state.State.PatientId = patientId;
        _state.State.OrderType = orderType;
        _state.State.OrderableItem = orderableItem;
        _state.State.OrderableItemId = orderableItemId;
        _state.State.ProviderId = providerId;
        _state.State.ProviderName = providerName;
        _state.State.StartDateTime = startDateTime;
        _state.State.LocationId = locationId;
        _state.State.LocationName = locationName;
        _state.State.Urgency = urgency;
        _state.State.Instructions = instructions;
        _state.State.Reason = reason;
        _state.State.Nature = nature;
        _state.State.Status = "Pending";
        _state.State.CreatedBy = createdBy;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        // Causal event: the placement of this order is the legal record.
        // Snapshot is a defensive clone so future mutations to live state
        // (sign/release/discontinue) cannot retroactively rewrite history.
        var evt = new OrderPlacedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = patientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            OrderId = _state.State.OrderId,
            Snapshot = _state.State.Clone()
        };
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await _state.WriteStateAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
    }

    public async Task UpdateInstructionsAsync(string instructions, string? modifiedBy)
    {
        _state.State.Instructions = instructions;
        _state.State.ModifiedBy = modifiedBy;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task UpdateStatusAsync(string status)
    {
        _state.State.Status = status;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task SignOrderAsync(string signature, DateTime signatureDateTime)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId)) return;
        if (!string.IsNullOrEmpty(_state.State.ElectronicSignature)) return; // already signed

        _state.State.ElectronicSignature = signature;
        _state.State.SignatureDateTime = signatureDateTime;
        _state.State.SignatureStatus = "E-SIGNED";
        _state.State.LastModifiedDate = DateTime.UtcNow;

        var evt = new OrderSignedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = _state.State.PatientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            OrderId = _state.State.OrderId,
            ElectronicSignature = signature,
            SignatureDateTime = signatureDateTime
        };
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await _state.WriteStateAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
    }

    public async Task ReleaseOrderAsync(DateTime releaseDateTime)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId)) return;

        _state.State.ReleaseDateTime = releaseDateTime;
        _state.State.Status = "Active";
        _state.State.LastModifiedDate = DateTime.UtcNow;

        var evt = new OrderReleasedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = _state.State.PatientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            OrderId = _state.State.OrderId,
            ReleaseDateTime = releaseDateTime
        };
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await _state.WriteStateAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
    }

    public async Task DiscontinueOrderAsync(
        DateTime discontinuedDateTime,
        string reason,
        string? discontinuedByProviderId)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId)) return;
        if (_state.State.Status == "Discontinued") return;

        _state.State.DiscontinuedDateTime = discontinuedDateTime;
        _state.State.DiscontinuedReason = reason;
        _state.State.DiscontinuedByProviderId = discontinuedByProviderId;
        _state.State.Status = "Discontinued";
        _state.State.LastModifiedDate = DateTime.UtcNow;

        var evt = new OrderDiscontinuedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = _state.State.PatientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            OrderId = _state.State.OrderId,
            DiscontinuedDateTime = discontinuedDateTime,
            Reason = reason,
            DiscontinuedByProviderId = discontinuedByProviderId
        };
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await _state.WriteStateAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
    }

    public async Task CompleteOrderAsync(DateTime completedDateTime)
    {
        _state.State.CompletedDateTime = completedDateTime;
        _state.State.Status = "Completed";
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task SetStopDateTimeAsync(DateTime stopDateTime)
    {
        _state.State.StopDateTime = stopDateTime;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task AddCommentsAsync(string comments)
    {
        _state.State.Comments = comments;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task FlagOrderAsync()
    {
        _state.State.IsFlagged = true;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task UnflagOrderAsync()
    {
        _state.State.IsFlagged = false;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        await _state.WriteStateAsync();
    }

    public async Task HoldOrderAsync()
    {
        if (string.IsNullOrEmpty(_state.State.PatientId)) return;
        if (_state.State.Status == "Hold") return;

        _state.State.Status = "Hold";
        _state.State.LastModifiedDate = DateTime.UtcNow;

        var evt = new OrderHeldV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = _state.State.PatientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            OrderId = _state.State.OrderId
        };
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await _state.WriteStateAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
    }

    public Task<string> GetPatientIdAsync()
    {
        return Task.FromResult(_state.State.PatientId);
    }

    public Task<string> GetOrderTypeAsync()
    {
        return Task.FromResult(_state.State.OrderType);
    }

    public Task<bool> IsActiveAsync()
    {
        return Task.FromResult(_state.State.Status == "Active");
    }

    public Task<bool> IsCompletedAsync()
    {
        return Task.FromResult(_state.State.Status == "Completed");
    }

    // ── GAP 2: Missing Order Actions ──────────────────────────────────────────

    public async Task RenewOrderAsync(string renewedByProviderId, DateTime? newStopDateTime)
    {
        _state.State.Nature = "RENEWAL";
        _state.State.Status = "Active";
        _state.State.StopDateTime = newStopDateTime;
        _state.State.SignatureStatus = "UNSIGNED";
        _state.State.ModifiedBy = renewedByProviderId;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task VerifyOrderAsync(string nurseId, DateTime verifiedDateTime)
    {
        _state.State.NurseVerifiedBy = nurseId;
        _state.State.NurseVerifiedDateTime = verifiedDateTime;
        _state.State.SignatureStatus = "ON CHART";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetCopiedFromAsync(string sourceOrderId)
    {
        _state.State.CopiedFromOrderId = sourceOrderId;
        _state.State.Nature = "COPY";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UnholdOrderAsync()
    {
        _state.State.Status = "Active";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task TransferOrderAsync(string toLocationId, string toLocationName)
    {
        _state.State.LocationId = toLocationId;
        _state.State.LocationName = toLocationName;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ParkOrderAsync(DateTime parkedDateTime)
    {
        _state.State.IsParked = true;
        _state.State.ParkedDateTime = parkedDateTime;
        _state.State.Status = "PARKED";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task UnparkOrderAsync(DateTime releaseDateTime)
    {
        _state.State.IsParked = false;
        _state.State.ReleaseDateTime = releaseDateTime;
        _state.State.Status = "Active";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordChartReviewAsync(string reviewedByUserId, DateTime reviewedDateTime)
    {
        _state.State.ChartReviewedBy = reviewedByUserId;
        _state.State.ChartReviewedDateTime = reviewedDateTime;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    // ── GAP 11: Event-Delayed Orders ──────────────────────────────────────────

    public async Task SetEventDelayAsync(
        string eventType,
        string eventIen,
        string eventName,
        string? patientEventIen,
        DateTime? eventEffectiveDateTime)
    {
        _state.State.IsDelayedOrder = true;
        _state.State.EventType = eventType;
        _state.State.EventIen = eventIen;
        _state.State.EventName = eventName;
        _state.State.PatientEventIen = patientEventIen;
        _state.State.EventEffectiveDateTime = eventEffectiveDateTime;
        _state.State.Status = "DELAYED";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ReleaseDelayedOrderAsync(DateTime releasedDateTime)
    {
        _state.State.IsDelayedOrder = false;
        _state.State.DelayedOrderReleasedDateTime = releasedDateTime;
        _state.State.ReleaseDateTime = releasedDateTime;
        _state.State.Status = "Active";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    // ── GAP 7: Billing Treatment Factors ─────────────────────────────────────

    public async Task SetTreatmentFactorsAsync(
        bool serviceConnected,
        bool agentOrange,
        bool ionizingRadiation,
        bool southwestAsia,
        bool mst,
        bool headNeckCancer,
        bool combatVeteran,
        bool shad,
        bool campLejeune)
    {
        _state.State.TfServiceConnected = serviceConnected;
        _state.State.TfAgentOrange = agentOrange;
        _state.State.TfIonizingRadiation = ionizingRadiation;
        _state.State.TfSouthwestAsia = southwestAsia;
        _state.State.TfMst = mst;
        _state.State.TfHeadNeckCancer = headNeckCancer;
        _state.State.TfCombatVeteran = combatVeteran;
        _state.State.TfShad = shad;
        _state.State.TfCampLejeune = campLejeune;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetAssociatedDiagnosisCodesAsync(List<string> icd10Codes)
    {
        _state.State.AssociatedDiagnosisCodes = icd10Codes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    // ── GAP 3: Controlled Substance ───────────────────────────────────────────

    public async Task SetControlledSubstanceAsync(bool isControlledSubstance, bool isDetox)
    {
        _state.State.IsControlledSubstance = isControlledSubstance;
        _state.State.IsDetox = isDetox;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
