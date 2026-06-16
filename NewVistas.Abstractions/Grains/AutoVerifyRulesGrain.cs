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
/// Auto-Verify Rules Grain — evaluates lab results against configurable
/// rules to determine auto-verification eligibility.
///
/// Decision logic mirrors VistA's auto-release checks:
/// 1. Is auto-verify globally enabled?
/// 2. Is the specific test enabled for auto-verify?
/// 3. Is the result within normal range?
/// 4. Is the result within critical range? (critical alert)
/// 5. Does the delta check pass vs previous result?
/// </summary>
public class AutoVerifyRulesGrain : Grain, IAutoVerifyRulesGrain
{
    private readonly IPersistentState<AutoVerifyRulesState> _state;

    public AutoVerifyRulesGrain(
        [PersistentState("autoVerifyRules", "autoVerifyRulesStore")]
        IPersistentState<AutoVerifyRulesState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.InstrumentId))
        {
            _state.State.InstrumentId = this.GetPrimaryKeyString();
        }

        return base.OnActivateAsync(cancellationToken);
    }

    public Task<AutoVerifyRulesState> GetRulesAsync()
        => Task.FromResult(_state.State);

    public async Task UpdateGlobalRulesAsync(
        bool enabled,
        int maxMessageAgeMinutes,
        bool requireSpecimenBarcode,
        bool requireDuplicateConfirmation)
    {
        _state.State.Enabled = enabled;
        _state.State.MaxMessageAgeMinutes = maxMessageAgeMinutes;
        _state.State.RequireSpecimenBarcode = requireSpecimenBarcode;
        _state.State.RequireDuplicateConfirmation = requireDuplicateConfirmation;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task AddTestRuleAsync(TestVerifyRule rule)
    {
        int idx = _state.State.TestRules.FindIndex(r => r.LoincCode == rule.LoincCode);
        if (idx >= 0)
            _state.State.TestRules[idx] = rule;
        else
            _state.State.TestRules.Add(rule);

        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task RemoveTestRuleAsync(string loincCode)
    {
        _state.State.TestRules.RemoveAll(r => r.LoincCode == loincCode);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<AutoVerifyDecision> EvaluateResultAsync(
        string loincCode, string resultValue, string? previousValue)
    {
        // If auto-verify is globally disabled, always require review
        if (!_state.State.Enabled)
            return Task.FromResult(AutoVerifyDecision.REVIEW);

        // Find the test-specific rule
        TestVerifyRule? rule = _state.State.TestRules
            .FirstOrDefault(r => r.LoincCode == loincCode);

        // No rule configured = cannot auto-verify
        if (rule == null || !rule.AutoVerifyEnabled)
            return Task.FromResult(AutoVerifyDecision.REVIEW);

        // Try to parse numeric result
        if (!double.TryParse(resultValue, out double numericValue))
            return Task.FromResult(AutoVerifyDecision.REVIEW); // Non-numeric = manual

        // Check critical range first (takes priority)
        if (rule.CriticalLow.HasValue && numericValue < rule.CriticalLow.Value)
            return Task.FromResult(AutoVerifyDecision.CRITICAL_ALERT);
        if (rule.CriticalHigh.HasValue && numericValue > rule.CriticalHigh.Value)
            return Task.FromResult(AutoVerifyDecision.CRITICAL_ALERT);

        // Check normal range
        if (rule.NormalLow.HasValue && numericValue < rule.NormalLow.Value)
            return Task.FromResult(AutoVerifyDecision.REVIEW);
        if (rule.NormalHigh.HasValue && numericValue > rule.NormalHigh.Value)
            return Task.FromResult(AutoVerifyDecision.REVIEW);

        // Delta check against previous result
        if (!string.IsNullOrEmpty(previousValue) && double.TryParse(previousValue, out double prevNumeric))
        {
            // Percentage delta check
            if (rule.DeltaCheckPercent.HasValue && prevNumeric != 0)
            {
                double percentChange = Math.Abs((numericValue - prevNumeric) / prevNumeric * 100);
                if (percentChange > rule.DeltaCheckPercent.Value)
                    return Task.FromResult(AutoVerifyDecision.REVIEW);
            }

            // Absolute delta check
            if (rule.DeltaCheckAbsolute.HasValue)
            {
                double absoluteChange = Math.Abs(numericValue - prevNumeric);
                if (absoluteChange > rule.DeltaCheckAbsolute.Value)
                    return Task.FromResult(AutoVerifyDecision.REVIEW);
            }
        }

        // All checks passed — auto-verify
        return Task.FromResult(AutoVerifyDecision.ACCEPT);
    }
}
