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
/// A ProtoCondition — the emerging-disease cluster grain (key <c>PROTO:{guid}</c>). Owns the case
/// definition, the versioned membership with evidence snapshots, guidance, the count-threshold
/// alert, and promotion/migration. The membership invariants live in <see cref="UpsertEvaluationAsync"/>;
/// once promoted, <see cref="ThrowIfPromoted"/> guards every matching mutator.
/// </summary>
public class ProtoConditionGrain : Grain, IProtoConditionGrain
{
    private readonly IPersistentState<ProtoConditionState> _state;

    public ProtoConditionGrain(
        [PersistentState("protoConditionState", "protoConditionStore")]
        IPersistentState<ProtoConditionState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.ProtoConditionId))
        {
            string key = this.GetPrimaryKeyString();
            int colon = key.IndexOf(':');
            _state.State.ProtoConditionId = colon >= 0 ? key[(colon + 1)..] : key;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    // ─── Lifecycle & definition ─────────────────────────────────────────

    public async Task CreateAsync(string name, string description, string createdBy)
    {
        ThrowIfPromoted();
        bool isNew = string.IsNullOrEmpty(_state.State.CreatedBy);
        _state.State.Name = name;
        _state.State.Description = description;
        if (isNew)
        {
            _state.State.CreatedBy = createdBy;
            _state.State.CreatedDate = DateTime.UtcNow;
            _state.State.Status = ProtoConditionStatus.Draft;
        }
        Log(createdBy, "CREATE", $"Created proto-condition '{name}'");
        await SaveAsync();
    }

    public async Task AddOrUpdateFeatureAsync(ProtoFeature feature, string byUser)
    {
        ThrowIfPromoted();
        if (feature is null || string.IsNullOrWhiteSpace(feature.FeatureId))
            throw new ArgumentException("Feature must have a FeatureId.", nameof(feature));

        _state.State.Features.RemoveAll(f => f.FeatureId == feature.FeatureId);
        _state.State.Features.Add(feature);
        BumpVersion();
        Log(byUser, "FEATURE", $"Set feature '{feature.Display}' ({feature.Kind}) → v{_state.State.DefinitionVersion}");
        await SaveAsync();
    }

    public async Task RemoveFeatureAsync(string featureId, string byUser)
    {
        ThrowIfPromoted();
        if (_state.State.Features.RemoveAll(f => f.FeatureId == featureId) == 0)
            return;
        BumpVersion();
        Log(byUser, "FEATURE", $"Removed feature '{featureId}' → v{_state.State.DefinitionVersion}");
        await SaveAsync();
    }

    public async Task SetMatchThresholdAsync(double threshold, string byUser)
    {
        ThrowIfPromoted();
        _state.State.MatchThreshold = Math.Clamp(threshold, 0.0, 1.0);
        BumpVersion();
        Log(byUser, "THRESHOLD", $"Match threshold → {_state.State.MatchThreshold:0.00} (v{_state.State.DefinitionVersion})");
        await SaveAsync();
    }

    public async Task ActivateAsync(string byUser)
    {
        ThrowIfPromoted();
        if (_state.State.Status == ProtoConditionStatus.Retired)
            throw new InvalidOperationException("A retired proto-condition cannot be re-activated.");
        _state.State.Status = ProtoConditionStatus.Active;
        Log(byUser, "ACTIVATE", "Activated — screening is live");
        await SaveAsync();
    }

    public async Task RetireAsync(string byUser, string reason)
    {
        ThrowIfPromoted();
        _state.State.Status = ProtoConditionStatus.Retired;
        Log(byUser, "RETIRE", $"Retired: {reason}");
        await SaveAsync();
    }

    // ─── Guidance (no version bump — matching semantics unchanged) ──────

    public async Task SetGuidanceAsync(BedIsolationType? isolation, string? ppeNotes, List<string> orderSetIds, string byUser)
    {
        ThrowIfPromoted();
        _state.State.IsolationRecommendation = isolation;
        _state.State.PpeNotes = ppeNotes;
        _state.State.AssociatedOrderSetIds = orderSetIds ?? new();
        Log(byUser, "GUIDANCE", $"Guidance updated (isolation={isolation?.ToString() ?? "none"})");
        await SaveAsync();
    }

    public async Task SetAlertRuleAsync(ProtoAlertRule rule, string byUser)
    {
        ThrowIfPromoted();
        if (rule is null)
        {
            _state.State.AlertRule = null;
        }
        else
        {
            // Preserve fired bookkeeping so editing the rule never re-fires historical crossings.
            ProtoAlertRule? prev = _state.State.AlertRule;
            _state.State.AlertRule = rule with
            {
                LastFiredCount = prev?.LastFiredCount ?? 0,
                LastFiredDate = prev?.LastFiredDate,
                TimesFired = prev?.TimesFired ?? 0
            };
        }
        Log(byUser, "ALERT", rule is null ? "Alert rule cleared" : $"Alert rule set (threshold {rule.Threshold})");
        await SaveAsync();
    }

    // ─── Membership ─────────────────────────────────────────────────────

    public async Task UpsertEvaluationAsync(ProtoMatchResult result)
    {
        ThrowIfPromoted();
        if (result is null || result.DefinitionVersion != _state.State.DefinitionVersion)
            return; // stale evaluation — computed against a superseded definition

        ProtoMember? member = FindMember(result.PatientId);

        // Excluded is terminal for the machine — never resurrected.
        if (member is { Status: ProtoMemberStatus.Excluded })
            return;

        // A Confirmed member is never silently reversed — flag it for re-review instead.
        if (member is { Status: ProtoMemberStatus.Confirmed })
        {
            member.Score = result.Score;
            member.Contributions = result.Contributions;
            member.EvaluatedAtVersion = result.DefinitionVersion;
            if (!result.Matches)
            {
                member.ReviewFlag = true;
                member.ReviewReason = $"No longer matches definition v{_state.State.DefinitionVersion}";
            }
            else
            {
                member.ReviewFlag = false;
                member.ReviewReason = null;
            }
            await SaveAsync();
            return;
        }

        bool changed = false;
        if (result.Matches)
        {
            if (member is null)
            {
                _state.State.Members.Add(new ProtoMember
                {
                    PatientId = result.PatientId,
                    Status = ProtoMemberStatus.Candidate,
                    Source = ProtoMemberSource.Machine,
                    Score = result.Score,
                    Contributions = result.Contributions,
                    EvaluatedAtVersion = result.DefinitionVersion,
                    FirstSeenDate = DateTime.UtcNow,
                    StatusChangedDate = DateTime.UtcNow
                });
            }
            else
            {
                member.Score = result.Score;
                member.Contributions = result.Contributions;
                member.EvaluatedAtVersion = result.DefinitionVersion;
            }
            changed = true;
        }
        else // no longer matches
        {
            if (member is { Source: ProtoMemberSource.Machine })
            {
                _state.State.Members.RemoveAll(m => m.PatientId == result.PatientId);
                changed = true;
            }
            else if (member is not null) // human-suggested — persists, snapshot refreshed
            {
                member.Score = result.Score;
                member.Contributions = result.Contributions;
                member.EvaluatedAtVersion = result.DefinitionVersion;
                changed = true;
            }
            // member is null & no match → nothing to do
        }

        if (changed)
            await SaveAsync();
    }

    public async Task SuggestMemberAsync(string patientId, string suggestedBy)
    {
        ThrowIfPromoted();
        if (string.IsNullOrWhiteSpace(patientId))
            return;

        ProtoMember? member = FindMember(patientId);
        if (member is null)
        {
            _state.State.Members.Add(new ProtoMember
            {
                PatientId = patientId,
                Status = ProtoMemberStatus.Candidate,
                Source = ProtoMemberSource.ManualSuggestion,
                SuggestedBy = suggestedBy,
                EvaluatedAtVersion = 0,
                FirstSeenDate = DateTime.UtcNow,
                StatusChangedDate = DateTime.UtcNow
            });
            Log(suggestedBy, "SUGGEST", $"{patientId} suggested into cluster");
        }
        else if (member.Status == ProtoMemberStatus.Confirmed)
        {
            return; // already a confirmed member
        }
        else if (member.Status == ProtoMemberStatus.Excluded)
        {
            // Deliberate human re-inclusion of a previously excluded patient (distinct from machine
            // resurrection, which is forbidden). Re-open as a human-sourced candidate.
            member.Status = ProtoMemberStatus.Candidate;
            member.Source = ProtoMemberSource.ManualSuggestion;
            member.SuggestedBy = suggestedBy;
            member.ReviewFlag = false;
            member.ReviewReason = null;
            member.StatusChangedBy = suggestedBy;
            member.StatusChangedDate = DateTime.UtcNow;
            Log(suggestedBy, "SUGGEST", $"{patientId} re-suggested (was excluded)");
        }
        else // Candidate — mark human-sourced so it persists through non-matching evals
        {
            member.Source = ProtoMemberSource.ManualSuggestion;
            member.SuggestedBy ??= suggestedBy;
            Log(suggestedBy, "SUGGEST", $"{patientId} suggestion reaffirmed");
        }
        await SaveAsync();
    }

    public async Task ConfirmMemberAsync(string patientId, string byUser)
    {
        ThrowIfPromoted();
        ProtoMember? member = FindMember(patientId)
            ?? throw new InvalidOperationException($"Patient {patientId} is not a candidate of this proto-condition.");

        if (member.Status == ProtoMemberStatus.Excluded)
            throw new InvalidOperationException("Cannot confirm an excluded member — re-suggest first.");
        if (member.Status == ProtoMemberStatus.Confirmed)
            return; // idempotent

        member.Status = ProtoMemberStatus.Confirmed;
        member.StatusChangedBy = byUser;
        member.StatusChangedDate = DateTime.UtcNow;
        member.ReviewFlag = false;
        member.ReviewReason = null;

        await Cohort().AddAsync(patientId);
        Log(byUser, "CONFIRM", $"{patientId} confirmed into cluster");

        await EvaluateAlertRuleAsync();
        await SaveAsync();
    }

    public async Task ExcludeMemberAsync(string patientId, string byUser, string reason)
    {
        ThrowIfPromoted();
        ProtoMember? member = FindMember(patientId);
        if (member is null)
        {
            // Pre-emptive exclusion so future machine evals cannot surface this patient.
            member = new ProtoMember { PatientId = patientId, Source = ProtoMemberSource.Machine, FirstSeenDate = DateTime.UtcNow };
            _state.State.Members.Add(member);
        }

        member.Status = ProtoMemberStatus.Excluded;
        member.ReviewFlag = false;
        member.ReviewReason = reason;
        member.StatusChangedBy = byUser;
        member.StatusChangedDate = DateTime.UtcNow;

        await Cohort().RemoveAsync(patientId);
        Log(byUser, "EXCLUDE", $"{patientId} excluded: {reason}");
        await SaveAsync();
    }

    // ─── Promotion & migration ──────────────────────────────────────────

    public async Task PromoteAsync(string officialName, List<string> icd10Codes, string? snomedCode,
        DateTime? effectiveFrom, List<string> jurisdictions, string notes, string byUser)
    {
        ThrowIfPromoted();
        if (_state.State.Status != ProtoConditionStatus.Active)
            throw new InvalidOperationException("Only an Active proto-condition can be promoted.");

        _state.State.PromotedName = officialName;
        _state.State.PromotedIcd10Codes = icd10Codes ?? new();
        _state.State.PromotedSnomed = snomedCode;
        _state.State.PromotedEffectiveFrom = effectiveFrom;
        _state.State.PromotionJurisdictions = jurisdictions ?? new();
        _state.State.PromotionNotes = notes;
        _state.State.PromotedDate = DateTime.UtcNow;
        _state.State.PromotedBy = byUser;
        _state.State.Status = ProtoConditionStatus.Promoted;

        // Candidates expire — the definition is frozen and clinical judgment is now the coded pipeline's.
        foreach (ProtoMember m in _state.State.Members.Where(m => m.Status == ProtoMemberStatus.Candidate))
        {
            m.Status = ProtoMemberStatus.Excluded;
            m.ReviewReason = "Expired at promotion";
            m.StatusChangedBy = byUser;
            m.StatusChangedDate = DateTime.UtcNow;
        }

        // Build the per-confirmed-member migration worklist (Pending).
        _state.State.MigrationLog = _state.State.Members
            .Where(m => m.Status == ProtoMemberStatus.Confirmed)
            .Select(m => new ProtoMigrationEntry { PatientId = m.PatientId, Status = ProtoMigrationStatus.Pending })
            .ToList();

        await EmitEcrTriggerAsync(officialName, icd10Codes ?? new(), snomedCode, jurisdictions ?? new());
        await NotifyPromotionAsync(officialName);

        Log(byUser, "PROMOTE", $"Promoted to '{officialName}' [{string.Join(", ", _state.State.PromotedIcd10Codes)}]");
        await SaveAsync();
    }

    public async Task RecordMigrationAsync(string patientId, ProtoMigrationStatus status, string? problemId, string? reason, string byUser)
    {
        // NOTE: deliberately NOT guarded by ThrowIfPromoted — migration happens AFTER promotion.
        ProtoMigrationEntry? entry = _state.State.MigrationLog.FirstOrDefault(e => e.PatientId == patientId);
        if (entry is null)
        {
            entry = new ProtoMigrationEntry { PatientId = patientId };
            _state.State.MigrationLog.Add(entry);
        }
        entry.Status = status;
        entry.ProblemId = problemId;
        entry.Reason = reason;
        entry.Date = DateTime.UtcNow;
        entry.By = byUser;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    // ─── Reads (open) ───────────────────────────────────────────────────

    public Task<ProtoConditionState> GetAsync() => Task.FromResult(_state.State);

    public Task<List<ProtoMember>> GetMembersByStatusAsync(ProtoMemberStatus status) =>
        Task.FromResult(_state.State.Members.Where(m => m.Status == status)
            .OrderByDescending(m => m.Score).ToList());

    public Task<int> GetConfirmedCountAsync() =>
        Task.FromResult(_state.State.Members.Count(m => m.Status == ProtoMemberStatus.Confirmed));

    public Task<ProtoConditionSummary> GetSummaryAsync() => Task.FromResult(BuildSummary());

    // ─── Internals ──────────────────────────────────────────────────────

    private ProtoMember? FindMember(string patientId) =>
        _state.State.Members.FirstOrDefault(m => m.PatientId == patientId);

    private IProtoCohortIndexGrain Cohort() =>
        GrainFactory.GetGrain<IProtoCohortIndexGrain>($"PROTO-COHORT:{_state.State.ProtoConditionId}");

    private IProtoConditionIndexGrain Index() =>
        GrainFactory.GetGrain<IProtoConditionIndexGrain>("PROTOCONDITION-INDEX");

    private void ThrowIfPromoted()
    {
        if (_state.State.Status == ProtoConditionStatus.Promoted)
            throw new InvalidOperationException("This proto-condition has been promoted and its definition is frozen.");
    }

    private void BumpVersion()
    {
        _state.State.DefinitionVersion++;
    }

    private void Log(string user, string kind, string detail) =>
        _state.State.ChangeLog.Add(new ProtoChangeLogEntry
        {
            Timestamp = DateTime.UtcNow,
            User = user,
            Kind = kind,
            Detail = detail
        });

    private async Task SaveAsync()
    {
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        await Index().AddOrUpdateAsync(BuildSummary());
    }

    private ProtoConditionSummary BuildSummary() => new()
    {
        ProtoConditionId = _state.State.ProtoConditionId,
        Name = _state.State.Name,
        Status = _state.State.Status,
        ConfirmedCount = _state.State.Members.Count(m => m.Status == ProtoMemberStatus.Confirmed),
        CandidateCount = _state.State.Members.Count(m => m.Status == ProtoMemberStatus.Candidate),
        StaleCount = _state.State.Members.Count(m =>
            m.Status != ProtoMemberStatus.Excluded && m.EvaluatedAtVersion < _state.State.DefinitionVersion),
        DefinitionVersion = _state.State.DefinitionVersion,
        LastModifiedDate = _state.State.LastModifiedDate,
        IsolationRecommendation = _state.State.IsolationRecommendation,
        PromotedCode = _state.State.Status == ProtoConditionStatus.Promoted
            ? _state.State.PromotedIcd10Codes.FirstOrDefault() ?? _state.State.PromotedSnomed
            : null
    };

    private async Task EvaluateAlertRuleAsync()
    {
        ProtoAlertRule? rule = _state.State.AlertRule;
        if (rule is null || rule.Recipients.Count == 0)
            return;

        int confirmedCount = _state.State.Members.Count(m => m.Status == ProtoMemberStatus.Confirmed);
        bool cooldownElapsed = rule.LastFiredDate is null
            || (DateTime.UtcNow - rule.LastFiredDate.Value).TotalHours >= rule.CooldownHours;

        if (confirmedCount >= rule.Threshold && confirmedCount > rule.LastFiredCount && cooldownElapsed)
        {
            await FireClusterAlertAsync(confirmedCount, rule.Recipients);
            rule.LastFiredCount = confirmedCount;
            rule.LastFiredDate = DateTime.UtcNow;
            rule.TimesFired++;
        }
    }

    private async Task FireClusterAlertAsync(int confirmedCount, List<string> recipients)
    {
        string message = $"Emerging cluster '{_state.State.Name}' reached {confirmedCount} confirmed members " +
                         $"(threshold {_state.State.AlertRule!.Threshold}). Review the surveillance dashboard.";
        foreach (string recipient in recipients)
        {
            string alertId = $"ALERT-PROTO-{_state.State.ProtoConditionId}-{Guid.NewGuid():N}";
            await GrainFactory.GetGrain<INotificationGrain>(alertId).CreateNotificationAsync(
                patientId: string.Empty,
                notificationType: NotificationType.EmergingClusterThreshold,
                notificationTypeText: "Emerging cluster threshold reached",
                recipientId: recipient,
                recipientName: recipient,
                sendingPackage: "SURVEILLANCE",
                messageText: message,
                followUpAction: $"/emerging-conditions/{_state.State.ProtoConditionId}",
                isCritical: true,
                xqaData: _state.State.ProtoConditionId);
        }
    }

    private async Task NotifyPromotionAsync(string officialName)
    {
        List<string>? recipients = _state.State.AlertRule?.Recipients;
        if (recipients is null || recipients.Count == 0)
            return;

        string message = $"Emerging cluster '{_state.State.Name}' was promoted to '{officialName}'. " +
                         "Confirmed members are queued for problem-list recoding.";
        foreach (string recipient in recipients)
        {
            string alertId = $"ALERT-PROTO-PROMOTE-{_state.State.ProtoConditionId}-{Guid.NewGuid():N}";
            await GrainFactory.GetGrain<INotificationGrain>(alertId).CreateNotificationAsync(
                patientId: string.Empty,
                notificationType: NotificationType.EmergingConditionPromoted,
                notificationTypeText: "Emerging condition promoted",
                recipientId: recipient,
                recipientName: recipient,
                sendingPackage: "SURVEILLANCE",
                messageText: message,
                followUpAction: $"/emerging-conditions/{_state.State.ProtoConditionId}",
                isCritical: false,
                xqaData: _state.State.ProtoConditionId);
        }
    }

    private async Task EmitEcrTriggerAsync(string officialName, List<string> icd10Codes, string? snomedCode, List<string> jurisdictions)
    {
        string triggerId = $"PROTO-{_state.State.ProtoConditionId}";

        var triggerCodes = icd10Codes.Select(c => new EcrTriggerCode
        {
            Code = c,
            CodeSystem = "ICD-10",
            Description = officialName,
            TriggerType = "diagnosis"
        }).ToList();
        if (!string.IsNullOrWhiteSpace(snomedCode))
            triggerCodes.Add(new EcrTriggerCode { Code = snomedCode!, CodeSystem = "SNOMED", Description = officialName, TriggerType = "diagnosis" });

        await GrainFactory.GetGrain<IEcrTriggerGrain>($"ECR-TRIGGER:{triggerId}").SaveTriggerAsync(new EcrTriggerState
        {
            TriggerId = triggerId,
            ConditionName = officialName,
            ConditionCode = snomedCode,
            ConditionCodeSystem = snomedCode is null ? null : "SNOMED",
            TriggerCodes = triggerCodes,
            Jurisdictions = jurisdictions,
            IsActive = true,
            Category = "communicable"
        });

        await GrainFactory.GetGrain<IEcrTriggerIndexGrain>("ECR-TRIGGER-INDEX").AddTriggerAsync(new EcrTriggerSummary
        {
            TriggerId = triggerId,
            ConditionName = officialName,
            Category = "communicable",
            IsActive = true,
            ReportingTimeframe = "24 hours",
            TriggerCodeCount = triggerCodes.Count
        });

        _state.State.EcrTriggerId = triggerId;
    }
}
