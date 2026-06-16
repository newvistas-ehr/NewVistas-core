// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.Events;
using NewVistas.Abstractions.Events.Clinical.Vitals;
using NewVistas.Abstractions.EventSourcing;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Vital Grain implementation based on VistA GMRV VITAL MEASUREMENT file (#120.5)
/// </summary>
public class VitalGrain : Grain, IVitalGrain
{
    private readonly IPersistentState<VitalState> _state;

    public VitalGrain(
        [PersistentState("vitalState", "vitalStore")] IPersistentState<VitalState> state)
    {
        _state = state;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.VitalId))
        {
            _state.State.VitalId = this.GetPrimaryKeyString();
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

    public Task<VitalState> GetVitalAsync() => Task.FromResult(_state.State);

    public async Task RecordVitalAsync(
        string patientId, string vitalType, string value, string? units,
        DateTime dateTimeTaken, string? locationId, string? locationName,
        string? enteredById, string? enteredByName,
        List<string>? qualifiers, string? comments)
    {
        // Idempotent: re-issued record on the same grain key is a no-op.
        if (!string.IsNullOrEmpty(_state.State.PatientId))
            return;

        _state.State.PatientId = patientId;
        _state.State.VitalType = vitalType;
        _state.State.Value = value;
        _state.State.Units = units;
        _state.State.DateTimeTaken = dateTimeTaken;
        _state.State.LocationId = locationId;
        _state.State.LocationName = locationName;
        _state.State.EnteredById = enteredById;
        _state.State.EnteredByName = enteredByName;
        if (qualifiers != null)
            _state.State.Qualifiers = qualifiers;
        _state.State.Comments = comments;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        ApplyRangeValidation(_state.State);

        var evt = new VitalRecordedV1
        {
            EventId = $"CEV-{Guid.NewGuid()}",
            PatientId = patientId,
            OccurredUtc = DateTime.UtcNow,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            VitalId = _state.State.VitalId,
            Snapshot = _state.State.Clone()
        };
        _state.State.PendingEvents.Add(EventEnvelope.Wrap(evt));

        await _state.WriteStateAsync();
        await this.DrainOutboxAsync(_state, GrainFactory);
    }

    public async Task MarkAbnormalAsync(string abnormalFlag)
    {
        _state.State.AbnormalFlag = abnormalFlag;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkEnteredInErrorAsync(string reason)
    {
        _state.State.IsEnteredInError = true;
        _state.State.EnteredInErrorReason = reason;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    // ── GAP 12: Range Validation ──────────────────────────────────────────────

    public async Task ValidateRangeAsync()
    {
        ApplyRangeValidation(_state.State);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    /// <summary>
    /// Applies VistA mGMV_VitalHiLo.pas range rules to the state.
    /// BLOOD PRESSURE value format is "systolic/diastolic" (e.g., "120/80").
    /// </summary>
    private static void ApplyRangeValidation(VitalState s)
    {
        s.IsAbnormalLow = false;
        s.IsAbnormalHigh = false;
        s.IsCriticalLow = false;
        s.IsCriticalHigh = false;
        s.IsOutOfRange = false;
        s.RangeValidationMessage = null;

        string vt = s.VitalType.ToUpperInvariant();

        if (vt == "BLOOD PRESSURE")
        {
            // Parse "120/80" format — validate both systolic and diastolic
            string[] parts = s.Value.Split('/');
            if (parts.Length == 2
                && double.TryParse(parts[0], out double sys)
                && double.TryParse(parts[1], out double dia))
            {
                if (sys < 0 || sys > 300 || dia < 0 || dia > 200)
                {
                    s.IsOutOfRange = true;
                    s.RangeValidationMessage = $"BP {s.Value} outside valid range (Sys 0-300, Dia 0-200)";
                }
                if (sys < 40) s.IsAbnormalLow = true;
                if (sys < 0) s.IsCriticalLow = true;
                if (sys > 300) { s.IsAbnormalHigh = true; s.IsCriticalHigh = true; }
                if (!string.IsNullOrEmpty(s.AbnormalFlag)) return;
                if (s.IsCriticalHigh || s.IsCriticalLow) s.AbnormalFlag = s.IsCriticalHigh ? "CRITICAL HIGH" : "CRITICAL LOW";
                else if (s.IsAbnormalHigh) s.AbnormalFlag = "HIGH";
                else if (s.IsAbnormalLow) s.AbnormalFlag = "LOW";
            }
            return;
        }

        if (!double.TryParse(s.Value, out double val)) return;

        (double validMin, double validMax, double abnLow, double abnHigh, double critLow, double critHigh) = vt switch
        {
            "TEMPERATURE"     => (60d, 120d, 86d, 106d, 60d, 120d),
            "PULSE"           => (0d, 300d, 20d, 300d, 0d, 300d),
            "RESPIRATION"     => (0d, 100d, 2d, 100d, 0d, 100d),
            "PULSE OXIMETRY"  => (50d, 100d, 50d, 100d, 50d, 100d),
            "CVP"             => (0d, 60d, 0d, 60d, 0d, 60d),
            _                 => (double.MinValue, double.MaxValue, double.MinValue, double.MaxValue, double.MinValue, double.MaxValue)
        };

        if (val < validMin || val > validMax)
        {
            s.IsOutOfRange = true;
            s.RangeValidationMessage = $"{s.VitalType} value {val} outside valid range ({validMin}-{validMax})";
        }

        if (val < critLow) s.IsCriticalLow = true;
        if (val > critHigh) s.IsCriticalHigh = true;
        if (val < abnLow) s.IsAbnormalLow = true;
        if (val > abnHigh) s.IsAbnormalHigh = true;

        if (s.IsCriticalHigh) s.AbnormalFlag = "CRITICAL HIGH";
        else if (s.IsCriticalLow) s.AbnormalFlag = "CRITICAL LOW";
        else if (s.IsAbnormalHigh) s.AbnormalFlag = "HIGH";
        else if (s.IsAbnormalLow) s.AbnormalFlag = "LOW";
    }
}
