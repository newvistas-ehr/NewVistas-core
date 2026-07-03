// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Patient Access Control Grain — manages sensitive record flags, authorized provider lists,
/// and break-the-glass audit trail.
///
/// Mirrors VistA DG SENSITIVITY routines and DG SECURITY LOG (File #38.1).
/// Key: "PAC:{patientId}"
/// </summary>
public class PatientAccessControlGrain : Grain, IPatientAccessControlGrain
{
    private readonly IPersistentState<PatientAccessControlState> _state;

    public PatientAccessControlGrain(
        [PersistentState("patientAccess", "patientAccessStore")] IPersistentState<PatientAccessControlState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId))
        {
            _state.State.PatientId = this.GetPrimaryKeyString();
            _state.State.CreatedDate = DateTime.UtcNow;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public Task<PatientAccessControlState> GetAccessControlAsync()
        => Task.FromResult(_state.State);

    public async Task SetSensitivityAsync(bool isSensitive, string sensitivityLevel, List<string> categories)
    {
        _state.State.IsSensitive = isSensitive;
        _state.State.SensitivityLevel = sensitivityLevel;
        _state.State.SensitivityCategories = categories;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddAuthorizedProviderAsync(string providerId)
    {
        if (!_state.State.AuthorizedProviderIds.Contains(providerId))
        {
            _state.State.AuthorizedProviderIds.Add(providerId);
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public async Task RemoveAuthorizedProviderAsync(string providerId)
    {
        if (_state.State.AuthorizedProviderIds.Remove(providerId))
        {
            _state.State.LastModifiedDate = DateTime.UtcNow;
            await _state.WriteStateAsync();
        }
    }

    public Task<bool> CheckAccessAsync(string userId)
    {
        if (!_state.State.IsSensitive)
            return Task.FromResult(true);

        bool authorized = _state.State.AuthorizedProviderIds.Contains(userId);
        return Task.FromResult(authorized);
    }

    public async Task RecordAccessAsync(string userId, string userName, string accessReason, bool wasBreakTheGlass, string? justificationText)
    {
        var entry = new PatientAccessLog
        {
            UserId = userId,
            UserName = userName,
            AccessDateTime = DateTime.UtcNow,
            AccessReason = accessReason,
            WasBreakTheGlass = wasBreakTheGlass,
            JustificationText = justificationText
        };
        _state.State.AccessLog.Add(entry);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<List<PatientAccessLog>> GetAccessLogAsync()
        => Task.FromResult(_state.State.AccessLog);

    public async Task ClearAccessLogAsync()
    {
        _state.State.AccessLog.Clear();
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetPart2ConsentAsync(bool hasConsent, DateTime? consentDate, string? scope)
    {
        _state.State.HasPart2Consent = hasConsent;
        _state.State.Part2ConsentDate = consentDate;
        _state.State.Part2ConsentScope = scope ?? string.Empty;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<bool> HasPart2ConsentAsync()
        => Task.FromResult(_state.State.HasPart2Consent);

    // ── ADR-002 Phase 4 ──────────────────────────────────────────────────────

    public async Task SetEmployeePatientAsync(bool isEmployeePatient)
    {
        const string cat = "EMPLOYEE";
        bool changed = false;
        if (isEmployeePatient)
        {
            if (!_state.State.SensitivityCategories.Contains(cat))
            {
                _state.State.SensitivityCategories.Add(cat);
                changed = true;
            }
            if (!_state.State.IsSensitive) { _state.State.IsSensitive = true; changed = true; }
            if (string.IsNullOrEmpty(_state.State.SensitivityLevel)) _state.State.SensitivityLevel = "ELEVATED";
        }
        else
        {
            if (_state.State.SensitivityCategories.Remove(cat)) changed = true;
            // Only stays sensitive if some OTHER reason remains.
            bool stillSensitive = _state.State.SensitivityCategories.Count > 0;
            if (_state.State.IsSensitive != stillSensitive) { _state.State.IsSensitive = stillSensitive; changed = true; }
            if (!stillSensitive) _state.State.SensitivityLevel = string.Empty;
        }
        if (!changed) return;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task SetSharePreferenceAsync(PatientSharePreference preference)
    {
        _state.State.SharePreference = preference;
        if (preference == PatientSharePreference.Restricted && !_state.State.IsSensitive)
        {
            _state.State.IsSensitive = true;
            if (!_state.State.SensitivityCategories.Contains("PATIENT_RESTRICTED"))
                _state.State.SensitivityCategories.Add("PATIENT_RESTRICTED");
            if (string.IsNullOrEmpty(_state.State.SensitivityLevel)) _state.State.SensitivityLevel = "ELEVATED";
        }
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task<PatientAccessDecision> DecideAccessAsync(
        string viewerUserId, string viewerName, bool breakTheGlassAttested, string? justificationText)
    {
        bool hasRelationship = _state.State.AuthorizedProviderIds.Contains(viewerUserId)
            || _state.State.Relationships.Any(r => r.UserId == viewerUserId
                && (r.ExpiresDate == null || r.ExpiresDate > DateTime.UtcNow));

        PatientAccessOutcome outcome;
        if (_state.State.SharePreference == PatientSharePreference.OpenForTeachingAndResearch)
            outcome = PatientAccessOutcome.AllowedByOpenConsent;      // patient chose openness — their record, their call
        else if (!_state.State.IsSensitive)
            outcome = PatientAccessOutcome.Allowed;
        else if (hasRelationship)
            outcome = PatientAccessOutcome.AllowedByRelationship;     // treating team — NEVER gated
        else if (breakTheGlassAttested)
            outcome = PatientAccessOutcome.AllowedByBreakTheGlass;    // attest-and-proceed
        else
            outcome = PatientAccessOutcome.RequiresBreakTheGlass;     // SOFT — attest to proceed

        bool granted = outcome != PatientAccessOutcome.RequiresBreakTheGlass;
        bool wasBtg = outcome == PatientAccessOutcome.AllowedByBreakTheGlass;

        (string reason, string message) = outcome switch
        {
            PatientAccessOutcome.Allowed => ("OPEN_RECORD", "Access granted."),
            PatientAccessOutcome.AllowedByRelationship => ("TREATING_PROVIDER", "Access granted — treatment relationship."),
            PatientAccessOutcome.AllowedByOpenConsent => ("PATIENT_OPEN_CONSENT", "Access granted — patient has opted into open sharing."),
            PatientAccessOutcome.AllowedByBreakTheGlass => ("BREAK_THE_GLASS", "Access granted via break-the-glass — this access is logged and the patient may be notified."),
            _ => ("BLOCKED_PENDING_BTG", "This is a protected record. You have no treatment relationship — attest a reason to proceed (break-the-glass).")
        };

        // Audit every decision, including a pending-BTG attempt (who tried to reach a protected record).
        _state.State.AccessLog.Add(new PatientAccessLog
        {
            UserId = viewerUserId,
            UserName = viewerName,
            AccessDateTime = DateTime.UtcNow,
            AccessReason = reason,
            WasBreakTheGlass = wasBtg,
            JustificationText = justificationText
        });
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();

        return new PatientAccessDecision
        {
            Outcome = outcome,
            Granted = granted,
            WasBreakTheGlass = wasBtg,
            IsSensitive = _state.State.IsSensitive,
            Message = message
        };
    }

    public async Task EstablishRelationshipAsync(string userId, TreatmentRelationshipReason reason, string sourceRef, DateTime? expiresAt)
    {
        if (string.IsNullOrWhiteSpace(userId)) return;
        // Upsert by (user, reason, source) so re-establishing the same relationship is idempotent.
        _state.State.Relationships.RemoveAll(r => r.UserId == userId && r.Reason == reason && r.SourceRef == sourceRef);
        _state.State.Relationships.Add(new TreatmentRelationship
        {
            UserId = userId,
            Reason = reason,
            SourceRef = sourceRef,
            EstablishedDate = DateTime.UtcNow,
            ExpiresDate = expiresAt
        });
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<List<PatientAccessLog>> GetSuspiciousAccessesAsync()
        // Anomaly surface: accesses to this (sensitive) record that had NO relationship — a break-the-glass
        // or a blocked pending-BTG attempt. For a flagged patient these are the ones a reviewer looks at.
        => Task.FromResult(_state.State.AccessLog
            .Where(e => e.WasBreakTheGlass || e.AccessReason == "BLOCKED_PENDING_BTG")
            .ToList());
}
