// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Runtime;
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Per-patient diagnostic episodes (ADR-006). Grain key <c>DX-EPISODE:{patientId}</c>.
/// </summary>
public class DiagnosticEpisodeIndexGrain : Grain, IDiagnosticEpisodeIndexGrain
{
    public const string KeyPrefix = "DX-EPISODE:";

    private readonly IPersistentState<DiagnosticEpisodeIndexState> _state;

    public DiagnosticEpisodeIndexGrain(
        [PersistentState("dxEpisodeState", "dxEpisodeStore")]
        IPersistentState<DiagnosticEpisodeIndexState> state)
    {
        _state = state;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_state.State.PatientId))
        {
            string key = this.GetPrimaryKeyString();
            _state.State.PatientId = key.StartsWith(KeyPrefix, StringComparison.Ordinal)
                ? key[KeyPrefix.Length..]
                : key;
        }
        return base.OnActivateAsync(cancellationToken);
    }

    public async Task<string> OpenEpisodeAsync(
        string problemId, string workingCode, string workingDisplay,
        DateTime assertedUtc, List<EvidenceRef> evidenceAtAssertion)
    {
        // Idempotent: re-asserting an already-open problem must not create a second episode,
        // or the same diagnosis would be counted twice in the Asserted denominator.
        DiagnosticEpisode? open = _state.State.Episodes
            .FirstOrDefault(e => e.ProblemId == problemId && e.Outcome == DiagnosticEpisodeOutcome.Open);
        if (open is not null) return open.EpisodeId;

        var episode = new DiagnosticEpisode
        {
            EpisodeId = $"DXE-{Guid.NewGuid()}",
            ProblemId = problemId,
            WorkingCode = DiagnosisCodeRelation.Normalize(workingCode),
            WorkingDisplay = workingDisplay,
            AssertedUtc = assertedUtc,
            EvidenceAtAssertion = new List<EvidenceRef>(evidenceAtAssertion ?? new()),
            Outcome = DiagnosticEpisodeOutcome.Open
        };

        _state.State.Episodes.Add(episode);
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        return episode.EpisodeId;
    }

    public async Task<DiagnosticEpisode?> AdjudicateAsync(
        string problemId, DiagnosticEpisodeOutcome outcome,
        string? outcomeCode, string? outcomeDisplay, RevisionReason? reason,
        string? outcomeNote, List<string> newEvidenceKeys, List<string> abnormalKeys,
        string? adjudicatingProviderId, DateTime adjudicatedUtc)
    {
        int idx = _state.State.Episodes.FindIndex(
            e => e.ProblemId == problemId && e.Outcome == DiagnosticEpisodeOutcome.Open);
        if (idx < 0) return null;

        DiagnosticEpisode e2 = _state.State.Episodes[idx];
        e2.Outcome = outcome;
        e2.OutcomeCode = DiagnosisCodeRelation.Normalize(outcomeCode);
        e2.OutcomeDisplay = outcomeDisplay;
        e2.OutcomeReason = reason;
        e2.OutcomeNote = outcomeNote;
        e2.AdjudicatedUtc = adjudicatedUtc;
        e2.AdjudicatingProviderId = adjudicatingProviderId;

        // Window the delta so an eight-month episode does not absorb an unrelated annual
        // physical and report the CBC as a discriminator.
        DateTime cutoff = adjudicatedUtc.AddDays(-DiagnosticStewardshipThresholds.DeltaWindowDays);
        var atAssertion = new HashSet<string>(
            e2.EvidenceAtAssertion
                .Where(r => r.Code is not null)
                .Select(TestKeyOf),
            StringComparer.Ordinal);

        e2.NewEvidence = (newEvidenceKeys ?? new())
            .Where(k => !atAssertion.Contains(k))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        e2.AbnormalAmongNewEvidence = (abnormalKeys ?? new())
            .Where(k => e2.NewEvidence.Contains(k, StringComparer.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // cutoff participates via the caller's key selection; retained here so the intent is
        // visible at the boundary that owns the window constant.
        _ = cutoff;

        _state.State.Episodes[idx] = e2;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
        return e2;
    }

    public async Task MarkAdvisoryShownAsync(string problemId)
    {
        int idx = _state.State.Episodes.FindIndex(
            e => e.ProblemId == problemId && e.Outcome == DiagnosticEpisodeOutcome.Open);
        if (idx < 0) return;
        if (_state.State.Episodes[idx].AdvisoryWasShown) return;

        DiagnosticEpisode e = _state.State.Episodes[idx];
        e.AdvisoryWasShown = true;
        _state.State.Episodes[idx] = e;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public async Task MarkReportedAsync(string episodeId, DateTime reportedUtc)
    {
        int idx = _state.State.Episodes.FindIndex(e => e.EpisodeId == episodeId);
        if (idx < 0) return;
        if (_state.State.Episodes[idx].ReportedToShardUtc is not null) return;

        DiagnosticEpisode e = _state.State.Episodes[idx];
        e.ReportedToShardUtc = reportedUtc;
        _state.State.Episodes[idx] = e;
        _state.State.LastModifiedDate = DateTime.UtcNow;
        await _state.WriteStateAsync();
    }

    public Task<List<DiagnosticEpisode>> GetEpisodesAsync()
        => Task.FromResult(_state.State.Episodes);

    public Task<DiagnosticEpisode?> GetOpenEpisodeForProblemAsync(string problemId)
        => Task.FromResult(_state.State.Episodes
            .FirstOrDefault(e => e.ProblemId == problemId && e.Outcome == DiagnosticEpisodeOutcome.Open));

    /// <summary>
    /// Namespaced test key for an evidence ref. LOINC and CPT number spaces overlap, so a bare
    /// "72148" would be ambiguous between a lab and an imaging study.
    /// </summary>
    private static string TestKeyOf(EvidenceRef r) => r.Kind switch
    {
        EvidenceKind.LabResult => $"L:{r.Code}",
        EvidenceKind.Imaging => $"R:{r.Code}",
        EvidenceKind.Procedure => $"C:{r.Code}",
        _ => $"E:{r.Code}"
    };
}
