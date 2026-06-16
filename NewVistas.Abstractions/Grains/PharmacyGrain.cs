// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Events.Clinical.Prescriptions;
using NewVistas.Abstractions.EventSourcing;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;
using NewVistas.Abstractions.Services;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Pharmacy Grain implementation based on VistA PRESCRIPTION file (#52)
/// </summary>
public class PharmacyGrain : Grain, IPharmacyGrain
{
    private readonly IPersistentState<PharmacyState> _state;
    private readonly IRouteValidationService _routeValidation;

    public PharmacyGrain(
        [PersistentState("pharmacyState", "pharmacyStore")] IPersistentState<PharmacyState> state,
        IRouteValidationService routeValidation)
    {
        _state = state;
        _routeValidation = routeValidation;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.PrescriptionId))
        {
            _state.State.PrescriptionId = this.GetPrimaryKeyString();
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

    public Task<PharmacyState> GetPrescriptionAsync() => Task.FromResult(_state.State);

    /// <summary>
    /// Pushes this prescription's current summary into the per-patient
    /// PSO-INDEX. Called after every lifecycle write so the index stays the
    /// authoritative COMPLETE prescription set — active-medication queries and
    /// drug-interaction screening read the index, not PatientState's capped
    /// recent-window ID list.
    /// </summary>
    private Task SyncPrescriptionIndexAsync()
    {
        if (string.IsNullOrEmpty(_state.State.PatientId))
            return Task.CompletedTask;

        IPatientPrescriptionIndexGrain index = GrainFactory
            .GetGrain<IPatientPrescriptionIndexGrain>($"PSO-INDEX:{_state.State.PatientId}");

        return index.AddOrUpdateEntryAsync(BuildIndexEntry(_state.State));
    }

    internal static PrescriptionIndexEntry BuildIndexEntry(PharmacyState state) => new()
    {
        PrescriptionId = state.PrescriptionId,
        DrugName = state.DrugName ?? string.Empty,
        DrugId = state.DrugId,
        Status = state.Status ?? string.Empty,
        IssueDate = state.IssueDate,
        ExpirationDate = state.ExpirationDate,
        Refills = state.Refills,
        RefillsRemaining = state.RefillsRemaining,
        Priority = state.Priority ?? "ROUTINE",
        IsVerified = state.IsVerified,
        CounselingRequired = state.CounselingRequired,
        ProviderId = state.ProviderId,
        ProviderName = state.ProviderName,
        Dosage = state.Dosage
    };

    public async Task CreatePrescriptionAsync(
        string patientId, string drugName, string? drugId,
        string? dosage, string? route, string? schedule, string? sig,
        int? daysSupply, int? quantity, int? refills,
        string? providerId, string? providerName,
        string? pharmacyId, string? pharmacyName,
        string? orderId, string? comments)
    {
        // Idempotent: re-issued create on the same grain key is a no-op.
        if (!string.IsNullOrEmpty(_state.State.PatientId))
            return;

        // Route-vs-dose-form check (warn-only): stamp an advisory when the route
        // is atypical for the drug's dose form. Never blocks the prescription.
        if (!string.IsNullOrWhiteSpace(route))
        {
            RouteValidationResult vr = await _routeValidation.ValidateByDrugAsync(GrainFactory, drugId, route);
            if (vr.Outcome == RouteValidationOutcome.Warn)
            {
                _state.State.RouteValidationWarning = vr.Message;
                _state.State.RouteSuggestions = vr.SuggestedRoutes;
            }
        }

        _state.State.PatientId = patientId;
        _state.State.DrugName = drugName;
        _state.State.DrugId = drugId;
        _state.State.Dosage = dosage;
        _state.State.Route = route;
        _state.State.Schedule = schedule;
        _state.State.Sig = sig;
        _state.State.DaysSupply = daysSupply;
        _state.State.Quantity = quantity;
        _state.State.Refills = refills;
        _state.State.RefillsRemaining = refills;
        _state.State.ProviderId = providerId;
        _state.State.ProviderName = providerName;
        _state.State.PharmacyId = pharmacyId;
        _state.State.PharmacyName = pharmacyName;
        _state.State.OrderId = orderId;
        _state.State.Comments = comments;
        _state.State.IssueDate = DateTime.UtcNow;
        _state.State.Status = "ACTIVE";
        _state.State.LastModifiedDate = DateTime.UtcNow;

        var evt = new PrescriptionCreatedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = patientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            PrescriptionId = _state.State.PrescriptionId,
            Snapshot = _state.State.Clone()
        };
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await _state.WriteStateAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
        await SyncPrescriptionIndexAsync();
    }

    public async Task FillPrescriptionAsync(DateTime fillDate)
    {
        if (_state.State.Status != "ACTIVE")
            throw new InvalidOperationException($"Cannot fill prescription in {_state.State.Status} status. Must be ACTIVE.");
        if (!_state.State.IsVerified)
            throw new InvalidOperationException("Cannot fill prescription that has not been verified by a pharmacist.");
        if (_state.State.FillDate.HasValue)
            throw new InvalidOperationException("Prescription has already been filled. Use RefillAsync for subsequent fills.");

        // DEA: controlled substance must have passed DEA check before fill
        if (_state.State.IsControlledSubstance && !_state.State.DeaCheckPassed)
            throw new InvalidOperationException(
                $"Cannot fill controlled substance (DEA Schedule {_state.State.DeaSchedule ?? "unknown"}): DEA check has not passed.");

        // Dispense constraints: days supply
        if (_state.State.MaxDaysSupply.HasValue && _state.State.DaysSupply.HasValue
            && _state.State.DaysSupply.Value > _state.State.MaxDaysSupply.Value)
            throw new InvalidOperationException(
                $"Days supply {_state.State.DaysSupply} exceeds maximum allowed {_state.State.MaxDaysSupply}.");

        // Dispense constraints: quantity
        if (_state.State.MaxQuantity.HasValue && _state.State.Quantity.HasValue
            && _state.State.Quantity.Value > _state.State.MaxQuantity.Value)
            throw new InvalidOperationException(
                $"Quantity {_state.State.Quantity} exceeds maximum allowed {_state.State.MaxQuantity}.");

        _state.State.FillDate = fillDate;
        _state.State.LastDispenseDate = fillDate;
        if (_state.State.DaysSupply.HasValue)
            _state.State.ExpirationDate = fillDate.AddDays(_state.State.DaysSupply.Value);

        _state.State.RefillHistory.Add(new RefillRecord
        {
            FillNumber = 0,
            FillDate = fillDate,
            Quantity = _state.State.Quantity,
            DaysSupply = _state.State.DaysSupply,
            RxNumber = _state.State.RxNumber
        });

        _state.State.LastModifiedDate = DateTime.UtcNow;

        var evt = new PrescriptionFilledV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = _state.State.PatientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            PrescriptionId = _state.State.PrescriptionId,
            FillDate = fillDate,
            Quantity = _state.State.Quantity,
            DaysSupply = _state.State.DaysSupply,
            RxNumber = _state.State.RxNumber
        };
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await _state.WriteStateAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
        await SyncPrescriptionIndexAsync();
    }

    public async Task DiscontinueAsync(string reason)
    {
        if (_state.State.Status is "DISCONTINUED" or "EXPIRED")
            throw new InvalidOperationException($"Cannot discontinue prescription in {_state.State.Status} status.");

        _state.State.Status = "DISCONTINUED";
        _state.State.DiscontinueReason = reason;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        var evt = new PrescriptionDiscontinuedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = _state.State.PatientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            PrescriptionId = _state.State.PrescriptionId,
            Reason = reason
        };
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await _state.WriteStateAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
        await SyncPrescriptionIndexAsync();
    }

    public async Task ExpireAsync()
    {
        if (_state.State.Status is not ("ACTIVE" or "HOLD"))
            throw new InvalidOperationException($"Cannot expire prescription in {_state.State.Status} status. Must be ACTIVE or HOLD.");

        _state.State.Status = "EXPIRED";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await SyncPrescriptionIndexAsync();
    }

    public async Task PlaceOnHoldAsync(string reason)
    {
        if (_state.State.Status != "ACTIVE")
            throw new InvalidOperationException($"Cannot place prescription on hold from {_state.State.Status} status. Must be ACTIVE.");

        _state.State.Status = "HOLD";
        _state.State.HoldReason = reason;
        _state.State.HoldDate = DateTime.UtcNow;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await SyncPrescriptionIndexAsync();
    }

    public async Task ResumeAsync()
    {
        if (_state.State.Status != "HOLD")
            throw new InvalidOperationException($"Cannot resume prescription from {_state.State.Status} status. Must be HOLD.");

        _state.State.Status = "ACTIVE";
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await SyncPrescriptionIndexAsync();
    }

    public async Task RefillAsync(DateTime fillDate)
    {
        if (_state.State.Status != "ACTIVE")
            throw new InvalidOperationException($"Cannot refill prescription in {_state.State.Status} status. Must be ACTIVE.");
        if (!_state.State.FillDate.HasValue)
            throw new InvalidOperationException("Cannot refill a prescription that has not been initially filled.");
        if (_state.State.RefillsRemaining.HasValue && _state.State.RefillsRemaining <= 0)
            throw new InvalidOperationException("No refills remaining on this prescription.");
        if (_state.State.ExpirationDate.HasValue && fillDate > _state.State.ExpirationDate.Value)
            throw new InvalidOperationException($"Prescription expired on {_state.State.ExpirationDate.Value:d}. Cannot refill after expiration.");

        // DEA Schedule II cannot be refilled (federal law — 21 CFR 1306.12)
        if (_state.State.IsControlledSubstance && _state.State.DeaSchedule is "II" or "IIN")
            throw new InvalidOperationException("DEA Schedule II prescriptions cannot be refilled. A new prescription is required.");

        // DEA Schedule III-V: max 5 refills within 6 months of issue (21 CFR 1306.22)
        if (_state.State.IsControlledSubstance && _state.State.DeaSchedule is "III" or "IIIIN" or "IV" or "V")
        {
            int refillsUsed = _state.State.RefillHistory.Count > 0 ? _state.State.RefillHistory.Count - 1 : 0;
            if (refillsUsed >= 5)
                throw new InvalidOperationException(
                    "Controlled substance (Schedule III-V): maximum 5 refills reached per 21 CFR 1306.22.");

            if (_state.State.IssueDate.HasValue)
            {
                DateTime sixMonthLimit = _state.State.IssueDate.Value.AddMonths(6);
                if (fillDate > sixMonthLimit)
                    throw new InvalidOperationException(
                        $"Controlled substance (Schedule III-V): refill must be within 6 months of issue date " +
                        $"({_state.State.IssueDate.Value:d}). Deadline was {sixMonthLimit:d}.");
            }
        }

        // Dispense constraints: max refills
        if (_state.State.MaxRefills.HasValue)
        {
            int refillsUsed = _state.State.RefillHistory.Count > 0 ? _state.State.RefillHistory.Count - 1 : 0;
            if (refillsUsed >= _state.State.MaxRefills.Value)
                throw new InvalidOperationException(
                    $"Maximum refills reached ({_state.State.MaxRefills}). No further refills allowed.");
        }

        // Dispense constraints: max days supply
        if (_state.State.MaxDaysSupply.HasValue && _state.State.DaysSupply.HasValue
            && _state.State.DaysSupply.Value > _state.State.MaxDaysSupply.Value)
            throw new InvalidOperationException(
                $"Days supply {_state.State.DaysSupply} exceeds maximum allowed {_state.State.MaxDaysSupply}.");

        // Dispense constraints: max quantity
        if (_state.State.MaxQuantity.HasValue && _state.State.Quantity.HasValue
            && _state.State.Quantity.Value > _state.State.MaxQuantity.Value)
            throw new InvalidOperationException(
                $"Quantity {_state.State.Quantity} exceeds maximum allowed {_state.State.MaxQuantity}.");

        // Early refill check — must have consumed at least 75% of days supply
        if (_state.State.LastDispenseDate.HasValue && _state.State.DaysSupply.HasValue && _state.State.DaysSupply.Value > 0)
        {
            DateTime earliestRefillDate = _state.State.LastDispenseDate.Value.AddDays(_state.State.DaysSupply.Value * 0.75);
            if (fillDate < earliestRefillDate)
                throw new InvalidOperationException(
                    $"Refill too early. Last dispensed {_state.State.LastDispenseDate.Value:d} with {_state.State.DaysSupply} day supply. " +
                    $"Earliest refill date is {earliestRefillDate:d} (75% consumed rule).");
        }

        _state.State.LastDispenseDate = fillDate;
        if (_state.State.RefillsRemaining.HasValue && _state.State.RefillsRemaining > 0)
            _state.State.RefillsRemaining--;
        if (_state.State.DaysSupply.HasValue)
            _state.State.ExpirationDate = fillDate.AddDays(_state.State.DaysSupply.Value);

        int fillNumber = _state.State.RefillHistory.Count > 0
            ? _state.State.RefillHistory[^1].FillNumber + 1
            : 1;

        _state.State.RefillHistory.Add(new RefillRecord
        {
            FillNumber = fillNumber,
            FillDate = fillDate,
            Quantity = _state.State.Quantity,
            DaysSupply = _state.State.DaysSupply,
            RxNumber = _state.State.RxNumber
        });

        _state.State.LastModifiedDate = DateTime.UtcNow;

        var evt = new PrescriptionRefilledV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = _state.State.PatientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            PrescriptionId = _state.State.PrescriptionId,
            FillNumber = fillNumber,
            FillDate = fillDate,
            Quantity = _state.State.Quantity,
            DaysSupply = _state.State.DaysSupply,
            RxNumber = _state.State.RxNumber,
            RefillsRemainingAfter = _state.State.RefillsRemaining
        };
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await _state.WriteStateAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
        await SyncPrescriptionIndexAsync();
    }

    public Task<RefillEligibilityResult> CheckRefillEligibilityAsync(DateTime proposedFillDate)
    {
        RefillEligibilityResult result = new()
        {
            Status = _state.State.Status,
            RefillsRemaining = _state.State.RefillsRemaining ?? 0,
            TotalRefillsAuthorized = _state.State.Refills ?? 0,
            ExpirationDate = _state.State.ExpirationDate,
            LastDispenseDate = _state.State.LastDispenseDate,
            DaysSupply = _state.State.DaysSupply,
            IsControlledSubstance = _state.State.IsControlledSubstance,
            DeaSchedule = _state.State.DeaSchedule,
            IsEligible = true,
        };

        // Calculate refills dispensed
        result.RefillsDispensed = _state.State.RefillHistory.Count > 0
            ? _state.State.RefillHistory.Count - 1  // subtract original fill
            : 0;

        // Check status
        if (_state.State.Status != "ACTIVE")
        {
            result.IsEligible = false;
            result.Reasons.Add($"Prescription status is {_state.State.Status}. Must be ACTIVE.");
        }

        // Check initial fill
        if (!_state.State.FillDate.HasValue)
        {
            result.IsEligible = false;
            result.Reasons.Add("Prescription has not been initially filled.");
        }

        // Check refills remaining
        if (_state.State.RefillsRemaining.HasValue && _state.State.RefillsRemaining <= 0)
        {
            result.IsEligible = false;
            result.Reasons.Add("No refills remaining.");
        }

        // Check expiration
        if (_state.State.ExpirationDate.HasValue && proposedFillDate > _state.State.ExpirationDate.Value)
        {
            result.IsEligible = false;
            result.Reasons.Add($"Prescription expired on {_state.State.ExpirationDate.Value:d}.");
        }

        // DEA Schedule II — no refills allowed
        if (_state.State.IsControlledSubstance && _state.State.DeaSchedule is "II" or "IIN")
        {
            result.IsEligible = false;
            result.Reasons.Add("DEA Schedule II prescriptions cannot be refilled.");
        }

        // DEA Schedule III-V — max 5 refills within 6 months of issue
        if (_state.State.IsControlledSubstance && _state.State.DeaSchedule is "III" or "IIIIN" or "IV" or "V")
        {
            if (result.RefillsDispensed >= 5)
            {
                result.IsEligible = false;
                result.Reasons.Add("Controlled substance (Schedule III-V): maximum 5 refills reached (21 CFR 1306.22).");
            }

            if (_state.State.IssueDate.HasValue)
            {
                DateTime sixMonthLimit = _state.State.IssueDate.Value.AddMonths(6);
                if (proposedFillDate > sixMonthLimit)
                {
                    result.IsEligible = false;
                    result.Reasons.Add(
                        $"Controlled substance: refill must be within 6 months of issue ({_state.State.IssueDate.Value:d}). Deadline: {sixMonthLimit:d}.");
                }
            }
        }

        // Dispense constraints: max refills
        if (_state.State.MaxRefills.HasValue && result.RefillsDispensed >= _state.State.MaxRefills.Value)
        {
            result.IsEligible = false;
            result.Reasons.Add($"Maximum refills reached ({_state.State.MaxRefills}).");
        }

        // Dispense constraints: max days supply
        if (_state.State.MaxDaysSupply.HasValue && _state.State.DaysSupply.HasValue
            && _state.State.DaysSupply.Value > _state.State.MaxDaysSupply.Value)
        {
            result.IsEligible = false;
            result.Reasons.Add($"Days supply {_state.State.DaysSupply} exceeds maximum {_state.State.MaxDaysSupply}.");
        }

        // Dispense constraints: max quantity
        if (_state.State.MaxQuantity.HasValue && _state.State.Quantity.HasValue
            && _state.State.Quantity.Value > _state.State.MaxQuantity.Value)
        {
            result.IsEligible = false;
            result.Reasons.Add($"Quantity {_state.State.Quantity} exceeds maximum {_state.State.MaxQuantity}.");
        }

        // Early refill check (75% consumed rule)
        if (_state.State.LastDispenseDate.HasValue && _state.State.DaysSupply.HasValue && _state.State.DaysSupply.Value > 0)
        {
            double daysSinceLastDispense = (proposedFillDate - _state.State.LastDispenseDate.Value).TotalDays;
            result.PercentConsumed = (int)(daysSinceLastDispense / _state.State.DaysSupply.Value * 100);

            DateTime earliestRefillDate = _state.State.LastDispenseDate.Value
                .AddDays(_state.State.DaysSupply.Value * 0.75);
            result.EarliestRefillDate = earliestRefillDate;

            if (proposedFillDate < earliestRefillDate)
            {
                result.IsTooEarly = true;
                result.IsEligible = false;
                result.Reasons.Add(
                    $"Refill too early — {result.PercentConsumed}% of supply consumed. " +
                    $"Earliest refill date: {earliestRefillDate:d} (75% consumed rule).");
            }
        }

        return Task.FromResult(result);
    }

    public async Task VerifyAsync(string pharmacistId)
    {
        if (_state.State.Status != "ACTIVE")
            throw new InvalidOperationException($"Cannot verify prescription in {_state.State.Status} status. Must be ACTIVE.");
        if (_state.State.IsVerified)
            throw new InvalidOperationException("Prescription has already been verified.");

        DateTime verifiedDate = DateTime.UtcNow;
        _state.State.IsVerified = true;
        _state.State.VerifiedBy = pharmacistId;
        _state.State.VerifiedDate = verifiedDate;
        _state.State.LastModifiedDate = verifiedDate;

        var evt = new PrescriptionVerifiedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = _state.State.PatientId,
            OccurredUtc = verifiedDate,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            PrescriptionId = _state.State.PrescriptionId,
            PharmacistId = pharmacistId,
            VerifiedDate = verifiedDate
        };
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await _state.WriteStateAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
        await SyncPrescriptionIndexAsync();
    }

    public async Task SetCounselingFlagAsync(bool required)
    {
        _state.State.CounselingRequired = required;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task PrintLabelAsync(string? rxNumber)
    {
        if (!_state.State.IsVerified)
            throw new InvalidOperationException("Cannot print label for a prescription that has not been verified.");

        _state.State.IsLabelPrinted = true;
        _state.State.LabelPrintedDate = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(rxNumber))
            _state.State.RxNumber = rxNumber;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RecordDispenseAsync(string? ndcDispensed, string? lotNumber, string? pharmacistId)
    {
        _state.State.NdcDispensed = ndcDispensed;
        _state.State.LotNumber = lotNumber;
        _state.State.LastModifiedDate = DateTime.UtcNow;

        // Update the most recent RefillRecord with dispense details
        if (_state.State.RefillHistory.Count > 0)
        {
            RefillRecord latest = _state.State.RefillHistory[^1];
            latest.NdcDispensed = ndcDispensed;
            latest.PharmacistId = pharmacistId;
        }

        await _state.WriteStateAsync();
    }

    public async Task RecordCounselingAsync(string pharmacistId, string? notes)
    {
        if (!_state.State.CounselingRequired)
            throw new InvalidOperationException("Counseling is not required for this prescription.");
        if (_state.State.CounselingCompleted)
            throw new InvalidOperationException("Counseling has already been completed for this prescription.");

        _state.State.CounselingCompleted = true;
        _state.State.CounselingDate = DateTime.UtcNow;
        _state.State.CounseledBy = pharmacistId;
        _state.State.CounselingNotes = notes;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<PrescriptionLabelContent> GenerateLabelContentAsync()
    {
        if (!_state.State.IsVerified)
            throw new InvalidOperationException("Cannot generate label for a prescription that has not been verified.");

        int fillNumber = _state.State.RefillHistory.Count > 0
            ? _state.State.RefillHistory[^1].FillNumber
            : 0;

        PrescriptionLabelContent label = new()
        {
            RxNumber = _state.State.RxNumber ?? _state.State.PrescriptionId,
            PatientId = _state.State.PatientId,
            PatientName = string.Empty, // Set by workflow grain which has access to patient grain
            DrugName = _state.State.DrugName,
            Dosage = _state.State.Dosage,
            Route = _state.State.Route,
            Schedule = _state.State.Schedule,
            Sig = _state.State.Sig,
            Quantity = _state.State.Quantity,
            DaysSupply = _state.State.DaysSupply,
            RefillsRemaining = _state.State.RefillsRemaining,
            ProviderName = _state.State.ProviderName,
            PharmacyName = _state.State.PharmacyName,
            IssueDate = _state.State.IssueDate,
            FillDate = _state.State.LastDispenseDate ?? _state.State.FillDate,
            ExpirationDate = _state.State.ExpirationDate,
            NdcDispensed = _state.State.NdcDispensed,
            LotNumber = _state.State.LotNumber,
            IsControlledSubstance = _state.State.IsControlledSubstance,
            DeaSchedule = _state.State.DeaSchedule,
            CounselingRequired = _state.State.CounselingRequired,
            BarcodeData = _state.State.RxNumber ?? _state.State.PrescriptionId,
            GeneratedDate = DateTime.UtcNow,
            FillNumber = fillNumber,
        };

        return Task.FromResult(label);
    }

    public async Task UpdatePriorityAsync(string priority)
    {
        if (priority != "ROUTINE" && priority != "URGENT" && priority != "STAT")
            throw new ArgumentException($"Invalid priority '{priority}'. Must be ROUTINE, URGENT, or STAT.");

        _state.State.Priority = priority;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<List<RefillRecord>> GetRefillHistoryAsync() =>
        Task.FromResult(_state.State.RefillHistory);

    public async Task AddRefillRecordAsync(RefillRecord record)
    {
        _state.State.RefillHistory.Add(record);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    // ── GAP 1: Medication Status Group ───────────────────────────────────────

    public async Task UpdateMedStatusGroupAsync()
    {
        string status = _state.State.Status.ToUpperInvariant();
        if (status is "ACTIVE" or "REFILL" or "HOLD" or "SUSPENDED" or "PROVIDER HOLD" or "ON CALL")
            _state.State.MedStatusGroup = 0; // MED_ACTIVE
        else if (status is "NON-VERIFIED" or "DRUG INTERACTIONS" or "INCOMPLETE" or "PENDING")
            _state.State.MedStatusGroup = 1; // MED_PENDING
        else
            _state.State.MedStatusGroup = 2; // MED_NONACTIVE
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    // ── GAP 3: DEA / Controlled Substance ────────────────────────────────────

    public async Task SetDeaCheckResultAsync(
        bool isControlledSubstance,
        string? deaSchedule,
        bool passed,
        string? failedReason)
    {
        _state.State.IsControlledSubstance = isControlledSubstance;
        _state.State.DeaSchedule = deaSchedule;
        _state.State.DeaCheckPassed = passed;
        _state.State.DeaCheckFailedReason = failedReason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    // ── GAP 4: Qty / Days / Refills Constraints ───────────────────────────────

    public async Task SetDispenseConstraintsAsync(
        int? maxRefills,
        int? maxDaysSupply,
        int? maxQuantity,
        bool isTitration,
        bool isDischarge,
        bool scheduleRequired)
    {
        _state.State.MaxRefills = maxRefills;
        _state.State.MaxDaysSupply = maxDaysSupply;
        _state.State.MaxQuantity = maxQuantity;
        _state.State.IsTitration = isTitration;
        _state.State.IsDischarge = isDischarge;
        _state.State.ScheduleRequired = scheduleRequired;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}
