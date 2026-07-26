// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Services;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Manages the lifecycle of a single medical/procedure prior-authorization request. Grain key:
/// <c>PROC-AUTH:{guid}</c>. Manual-first: only an Electronic channel routes through the (offline-by-default)
/// 278/PAS transmitter; portal/phone/fax are recorded as submitted without transmission.
/// </summary>
public class ProcedureAuthorizationGrain : Grain, IProcedureAuthorizationGrain
{
    private readonly IPersistentState<ProcedureAuthorizationState> _state;
    private readonly IProcedurePriorAuthTransmitter _transmitter;

    public ProcedureAuthorizationGrain(
        [PersistentState("procAuthState", "procAuthStore")]
        IPersistentState<ProcedureAuthorizationState> state,
        IProcedurePriorAuthTransmitter transmitter)
    {
        _state = state;
        _transmitter = transmitter;
    }

    public Task<ProcedureAuthorizationState> GetAsync() => Task.FromResult(_state.State);

    public async Task SubmitAsync(
        string patientId, string cptCode, string cptDescription, string payerId, string payerName,
        string orderingProviderId, string orderingProviderName, List<string> diagnosisCodes,
        string clinicalJustification, DateTime? serviceStartDate, DateTime? serviceEndDate,
        ProcedureAuthSubmissionChannel channel, string? orderId, string? consultId, string? externalReferralId)
    {
        ProcedureAuthorizationState s = _state.State;
        s.ProcAuthId = this.GetPrimaryKeyString();
        s.PatientId = patientId;
        s.CptCode = (cptCode ?? string.Empty).Trim().ToUpperInvariant();
        s.CptDescription = cptDescription ?? string.Empty;
        s.PayerId = (payerId ?? string.Empty).Trim().ToUpperInvariant();
        s.PayerName = payerName ?? string.Empty;
        s.OrderingProviderId = orderingProviderId ?? string.Empty;
        s.OrderingProviderName = orderingProviderName ?? string.Empty;
        s.DiagnosisCodes = diagnosisCodes ?? new();
        s.ClinicalJustification = clinicalJustification ?? string.Empty;
        s.RequestedServiceStartDate = serviceStartDate;
        s.RequestedServiceEndDate = serviceEndDate;
        s.SubmissionChannel = channel;
        s.OrderId = orderId;
        s.ConsultId = consultId;
        s.ExternalReferralId = externalReferralId;
        s.Status = ProcedureAuthorizationStatus.Submitted;
        s.SubmittedDate = DateTime.UtcNow;
        s.LastModifiedDate = DateTime.UtcNow;

        // Only the Electronic channel touches the transmitter; it's offline by default (records a stand-in).
        if (channel == ProcedureAuthSubmissionChannel.Electronic)
        {
            ProcedurePaTransmissionResult result = await _transmitter.SubmitAsync(new ProcedurePaRequestMessage
            {
                ProcAuthId = s.ProcAuthId,
                PatientId = s.PatientId,
                CptCode = s.CptCode,
                PayerId = s.PayerId,
                DiagnosisCodes = s.DiagnosisCodes,
                ClinicalJustification = s.ClinicalJustification,
                ServiceStartDate = s.RequestedServiceStartDate,
                ServiceEndDate = s.RequestedServiceEndDate
            });
            s.TransmissionDetail = result.Detail;
        }

        await _state.WriteStateAsync();
    }

    public async Task PendAsync(string infoRequested)
    {
        _state.State.Status = ProcedureAuthorizationStatus.Pended;
        _state.State.PendedInfoRequested = infoRequested;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ApproveAsync(string reviewerId, string reviewerName, string authorizationNumber, DateTime? expirationDate)
    {
        ProcedureAuthorizationState s = _state.State;
        s.Status = ProcedureAuthorizationStatus.Approved;
        s.DecisionDate = DateTime.UtcNow;
        s.DecisionById = reviewerId ?? string.Empty;
        s.DecisionByName = reviewerName ?? string.Empty;
        s.AuthorizationNumber = authorizationNumber ?? string.Empty;
        s.ExpirationDate = expirationDate;
        s.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task DenyAsync(string reviewerId, string reviewerName, List<ProcedureDenialReason> denialReasons)
    {
        ProcedureAuthorizationState s = _state.State;
        s.Status = ProcedureAuthorizationStatus.Denied;
        s.DecisionDate = DateTime.UtcNow;
        s.DecisionById = reviewerId ?? string.Empty;
        s.DecisionByName = reviewerName ?? string.Empty;
        s.DenialReasons = denialReasons ?? new();
        s.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task ExpireAsync()
    {
        _state.State.Status = ProcedureAuthorizationStatus.Expired;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task CancelAsync()
    {
        _state.State.Status = ProcedureAuthorizationStatus.Cancelled;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }
}

/// <summary>Per-patient index of procedure-PA requests. Grain key: <c>PROC-AUTH-IDX:{patientId}</c>.</summary>
public class ProcedureAuthorizationIndexGrain : Grain, IProcedureAuthorizationIndexGrain
{
    private readonly IPersistentState<ProcedureAuthorizationIndexState> _state;

    public ProcedureAuthorizationIndexGrain(
        [PersistentState("procAuthIndexState", "procAuthIndexStore")]
        IPersistentState<ProcedureAuthorizationIndexState> state)
    {
        _state = state;
    }

    public async Task AddOrUpdateAsync(ProcedureAuthIndexEntry entry)
    {
        int idx = _state.State.Entries.FindIndex(e => e.ProcAuthId == entry.ProcAuthId);
        if (idx >= 0)
            _state.State.Entries[idx] = entry;
        else
            _state.State.Entries.Add(entry);
        await _state.WriteStateAsync();
    }

    public async Task RemoveAsync(string procAuthId)
    {
        _state.State.Entries.RemoveAll(e => e.ProcAuthId == procAuthId);
        await _state.WriteStateAsync();
    }

    public Task<List<ProcedureAuthIndexEntry>> GetAllAsync() =>
        Task.FromResult(_state.State.Entries.OrderByDescending(e => e.SubmittedDate ?? DateTime.MinValue).ToList());

    public Task<List<ProcedureAuthIndexEntry>> GetByStatusAsync(ProcedureAuthorizationStatus status) =>
        Task.FromResult(_state.State.Entries.Where(e => e.Status == status)
            .OrderByDescending(e => e.SubmittedDate ?? DateTime.MinValue).ToList());

    public async Task ClearAsync()
    {
        _state.State.Entries.Clear();
        await _state.WriteStateAsync();
    }
}

/// <summary>
/// Learned requirements shard for one (payer, procedure) pair. Grain key
/// <c>PAYER-PROC:{payerId}:{cptCode}</c>. The payerId contains hyphens (e.g. PAYER-BCBS-FL), so the key
/// is parsed by stripping the "PAYER-PROC:" prefix then splitting the remainder on the LAST ':'.
/// </summary>
public class PayerProcedureRequirementIndexGrain : Grain, IPayerProcedureRequirementIndexGrain
{
    private const string KeyPrefix = "PAYER-PROC:";
    private readonly IPersistentState<PayerProcedureRequirementState> _state;

    public PayerProcedureRequirementIndexGrain(
        [PersistentState("payerProcReqState", "payerProcReqStore")]
        IPersistentState<PayerProcedureRequirementState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.PayerId) && string.IsNullOrEmpty(_state.State.CptCode))
        {
            string key = this.GetPrimaryKeyString();
            string body = key.StartsWith(KeyPrefix, StringComparison.Ordinal) ? key[KeyPrefix.Length..] : key;
            int lastColon = body.LastIndexOf(':');
            if (lastColon >= 0)
            {
                _state.State.PayerId = body[..lastColon];
                _state.State.CptCode = body[(lastColon + 1)..];
            }
            else
            {
                _state.State.CptCode = body;
            }
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task RecordDenialAsync(List<PriorAuthRequirementCategory> categories, string reasonText, string procAuthId)
    {
        PayerProcedureRequirementState s = _state.State;
        s.TotalDenials++;

        if (categories == null || categories.Count == 0)
        {
            UnmappedDenial? existing = s.UnmappedDenials.FirstOrDefault(u => u.ReasonText == reasonText);
            if (existing is null)
                s.UnmappedDenials.Add(new UnmappedDenial { ReasonText = reasonText ?? string.Empty, Count = 1, LastSeen = DateTime.UtcNow });
            else { existing.Count++; existing.LastSeen = DateTime.UtcNow; }
        }
        else
        {
            foreach (PriorAuthRequirementCategory cat in categories.Distinct())
            {
                CategoryStat stat = s.CategoryStats.FirstOrDefault(c => c.Category == cat)
                    ?? AddStat(s, cat);
                stat.DenialCount++;
                stat.LastDeniedOn = DateTime.UtcNow;
                stat.LastSampleReason = reasonText;
            }
        }
        await _state.WriteStateAsync();
    }

    public async Task RecordApprovalAsync(List<PriorAuthRequirementCategory> categoriesSatisfied, string procAuthId)
    {
        PayerProcedureRequirementState s = _state.State;
        s.TotalApprovals++;
        foreach (PriorAuthRequirementCategory cat in (categoriesSatisfied ?? new()).Distinct())
        {
            CategoryStat stat = s.CategoryStats.FirstOrDefault(c => c.Category == cat) ?? AddStat(s, cat);
            stat.ApprovalSatisfiedCount++;
        }
        await _state.WriteStateAsync();
    }

    public Task<PayerProcedureRequirementProfile> GetProfileAsync() =>
        Task.FromResult(new PayerProcedureRequirementProfile
        {
            PayerId = _state.State.PayerId,
            CptCode = _state.State.CptCode,
            CategoryStats = _state.State.CategoryStats.ToList(),
            TotalDenials = _state.State.TotalDenials,
            TotalApprovals = _state.State.TotalApprovals,
            UnmappedDenials = _state.State.UnmappedDenials.ToList()
        });

    private static CategoryStat AddStat(PayerProcedureRequirementState s, PriorAuthRequirementCategory cat)
    {
        var stat = new CategoryStat { Category = cat };
        s.CategoryStats.Add(stat);
        return stat;
    }
}
