// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;

namespace NewVistas.Abstractions.GrainInterfaces;

/// <summary>
/// Auto-Verify Rules Grain Interface. Defines per-instrument rules for
/// automatic verification (release) of lab results.
///
/// Grain Key: "LA-AUTOVERIFY:{instrumentId}"
///
/// Mirrors VistA's auto-release configuration in AUTO INSTRUMENT file (#62.4,
/// field 99) and the validation/acceptance logic applied during result processing
/// in LA7VIN7.m.
///
/// When a result arrives from an instrument:
///   1. Message queue grain calls EvaluateResultAsync
///   2. Rules grain checks: range, delta, critical thresholds
///   3. Returns ACCEPT (auto-verify), REVIEW (manual), or CRITICAL_ALERT
/// </summary>
public interface IAutoVerifyRulesGrain : IGrainWithStringKey
{
    /// <summary>
    /// Gets the complete rules state.
    /// </summary>
    Task<GrainStates.AutoVerifyRulesState> GetRulesAsync();

    /// <summary>
    /// Updates the global rules configuration.
    /// </summary>
    Task UpdateGlobalRulesAsync(
        bool enabled,
        int maxMessageAgeMinutes,
        bool requireSpecimenBarcode,
        bool requireDuplicateConfirmation);

    /// <summary>
    /// Adds or updates a test-specific verification rule.
    /// </summary>
    Task AddTestRuleAsync(GrainStates.TestVerifyRule rule);

    /// <summary>
    /// Removes a test-specific rule by LOINC code.
    /// </summary>
    Task RemoveTestRuleAsync(string loincCode);

    /// <summary>
    /// Evaluates a result value against the configured rules.
    /// Returns the auto-verify decision.
    /// </summary>
    /// <param name="loincCode">Test LOINC code</param>
    /// <param name="resultValue">Numeric result value as string</param>
    /// <param name="previousValue">Previous result value for delta check (null if none)</param>
    Task<GrainStates.AutoVerifyDecision> EvaluateResultAsync(
        string loincCode,
        string resultValue,
        string? previousValue);
}
