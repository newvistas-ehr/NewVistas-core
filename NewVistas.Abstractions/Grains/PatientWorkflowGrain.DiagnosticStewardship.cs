// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans.Runtime;
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Security;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Diagnosis provenance and revision statistics (ADR-006).
///
/// Gated by <c>DIAGNOSTIC_STEWARDSHIP</c>, which is on by default and — uniquely — <b>one-way</b>:
/// once a site disables it, it can never be re-enabled, because the counters would resume against
/// a denominator that silently missed the dark period. See
/// <see cref="SiteFeatures.DiagnosticStewardship"/>.
///
/// Every write here is a no-op when the flag is off, so a disabled site simply stops accruing.
/// Reads return an empty advisory rather than throwing, because the advisory is decoration on a
/// problem-list screen and a disabled optional feature must not break a clinical page.
/// </summary>
public partial class PatientWorkflowGrain
{
    private const string DiagnosticStewardshipFeature = SiteFeatures.DiagnosticStewardship;

    private IDiagnosticEpisodeIndexGrain DxEpisodes()
        => GrainFactory.GetGrain<IDiagnosticEpisodeIndexGrain>(
            $"{DiagnosticEpisodeIndexGrain.KeyPrefix}{PatientId}");

    private Task<bool> StewardshipEnabledAsync()
        => GetSiteParams().IsFeatureEnabledAsync(DiagnosticStewardshipFeature);

    /// <summary>
    /// Open a diagnostic episode for a newly asserted working diagnosis, and count it into the
    /// three assertion-year shards. Call from the problem-add path.
    ///
    /// Silently does nothing when the feature is off — assertions made while disabled are
    /// permanently absent from the denominator, which is exactly why the disable is one-way.
    /// </summary>
    public async Task<string?> OpenDiagnosticEpisodeAsync(
        string problemId, string workingCode, string workingDisplay,
        List<EvidenceRef>? evidenceAtAssertion = null)
    {
        if (!await StewardshipEnabledAsync()) return null;

        string code = DiagnosisCodeRelation.Normalize(workingCode);
        if (code.Length == 0) return null;

        DateTime now = DateTime.UtcNow;
        string episodeId = await DxEpisodes().OpenEpisodeAsync(
            problemId, code, workingDisplay, now, evidenceAtAssertion ?? new());

        foreach (IDiagnosisOutcomeIndexGrain shard in ShardsFor(code, now.Year))
            await shard.RecordAssertionAsync();

        return episodeId;
    }

    /// <summary>
    /// Adjudicate an open episode — the single counting write.
    ///
    /// <paramref name="outcome"/> is the clinician's choice. Callers are expected to have offered
    /// <see cref="DiagnosisCodeRelation.Propose"/>'s suggestion as a default, but what is counted
    /// is what the clinician said: a machine's opinion that a doctor was wrong is an accusation,
    /// a doctor's own coded statement is evidence.
    ///
    /// Fans out to the <c>CODE</c>, <c>CAT</c> and <c>ALL</c> shards for the <b>assertion</b>
    /// year, so a bucket always means "the diagnostic practice of that year".
    /// </summary>
    [RequiresSecurityKey(SecurityKeys.GMPL_PROBLEM)]
    [AuditAction("PROBLEMS", "ADJUDICATE", EntityType = "DX_EPISODE", IsClinicalWrite = true)]
    public async Task<bool> AdjudicateDiagnosticEpisodeAsync(
        string problemId, DiagnosticEpisodeOutcome outcome,
        string? outcomeCode, string? outcomeDisplay, RevisionReason? reason, string? outcomeNote)
    {
        if (!await StewardshipEnabledAsync()) return false;
        if (outcome == DiagnosticEpisodeOutcome.Open) return false;

        DiagnosticEpisode? open = await DxEpisodes().GetOpenEpisodeForProblemAsync(problemId);
        if (open is null) return false;

        DateTime now = DateTime.UtcNow;
        string? providerId = RequestContext.Get(RequestContextKeys.UserId) as string;

        (List<string> newKeys, List<string> abnormalKeys) = await ComputeEvidenceDeltaAsync(open, now);
        List<string> presentKeys = open.EvidenceAtAssertion
            .Where(r => !string.IsNullOrEmpty(r.Code))
            .Select(TestKeyOf)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        DiagnosticEpisode? done = await DxEpisodes().AdjudicateAsync(
            problemId, outcome, outcomeCode, outcomeDisplay, reason, outcomeNote,
            newKeys, abnormalKeys, providerId, now);
        if (done is null) return false;

        // At-most-once: the guard lives patient-side so the shard stays a pure counter with no
        // unbounded dedupe set. A double-counted misdiagnosis is not cosmetic.
        if (done.ReportedToShardUtc is not null) return true;

        foreach (IDiagnosisOutcomeIndexGrain shard in ShardsFor(done.WorkingCode, done.AssertedUtc.Year))
        {
            await shard.RecordAdjudicationAsync(
                outcome, done.OutcomeCode, done.OutcomeDisplay, done.OutcomeNote,
                done.NewEvidence, done.AbnormalAmongNewEvidence, presentKeys,
                providerId, done.AdvisoryWasShown, now);
        }

        await DxEpisodes().MarkReportedAsync(done.EpisodeId, now);
        return true;
    }

    /// <summary>
    /// The clinician-facing advisory for a working diagnosis. Pull-only.
    ///
    /// Returns an empty advisory when the feature is off. Records exposure when a learned rate
    /// is actually shown, so the unexposed comparison arm stays honest — showing a curated
    /// baseline line alone does not count as exposure, because the baseline carries no
    /// site-learned number that could bias practice back into the statistics.
    /// </summary>
    public async Task<DiagnosisRevisionAdvisory> GetDiagnosisRevisionAdvisoryAsync(
        string workingCode, string workingDisplay, string? problemId = null)
    {
        if (!await StewardshipEnabledAsync())
            return new DiagnosisRevisionAdvisory
            {
                WorkingCode = DiagnosisCodeRelation.Normalize(workingCode),
                WorkingDisplay = workingDisplay,
                Band = RevisionRateBand.Insufficient,
                IsColdStart = true,
                InsufficientReason = "Diagnostic stewardship is not enabled at this site.",
                GeneratedAt = DateTime.UtcNow
            };

        var analytics = GrainFactory.GetGrain<IDiagnosisOutcomeAnalyticsGrain>(
            DiagnosisCodeRelation.Normalize(workingCode));
        DiagnosisRevisionAdvisory advisory =
            await analytics.GetAdvisoryAsync(workingCode, workingDisplay);

        if (problemId is not null && advisory.RevisionRate is not null)
            await DxEpisodes().MarkAdvisoryShownAsync(problemId);

        return advisory;
    }

    /// <summary>All episodes for this patient, for the provenance view.</summary>
    public async Task<List<DiagnosticEpisode>> GetDiagnosticEpisodesAsync()
        => !await StewardshipEnabledAsync() ? new() : await DxEpisodes().GetEpisodesAsync();

    // ── helpers ─────────────────────────────────────────────────────────────

    private IEnumerable<IDiagnosisOutcomeIndexGrain> ShardsFor(string normalizedCode, int assertionYear)
    {
        yield return GrainFactory.GetGrain<IDiagnosisOutcomeIndexGrain>(
            DiagnosisOutcomeIndexGrain.KeyFor(DiagnosisCodeGranularity.Code, normalizedCode, assertionYear));
        yield return GrainFactory.GetGrain<IDiagnosisOutcomeIndexGrain>(
            DiagnosisOutcomeIndexGrain.KeyFor(DiagnosisCodeGranularity.Category,
                DiagnosisCodeRelation.Category3(normalizedCode), assertionYear));
        yield return GrainFactory.GetGrain<IDiagnosisOutcomeIndexGrain>(
            DiagnosisOutcomeIndexGrain.KeyFor(DiagnosisCodeGranularity.All, "ALL", assertionYear));
    }

    /// <summary>
    /// Which results arrived between assertion and adjudication, windowed to
    /// <see cref="DiagnosticStewardshipThresholds.DeltaWindowDays"/> so a long-running episode
    /// does not absorb an unrelated workup and report the CBC as a discriminator.
    ///
    /// Reads the existing lab index — <c>LabIndexEntry</c> already carries LOINC, result date and
    /// the abnormal flag, so no new read path is needed.
    /// </summary>
    private async Task<(List<string> NewKeys, List<string> AbnormalKeys)> ComputeEvidenceDeltaAsync(
        DiagnosticEpisode episode, DateTime adjudicatedUtc)
    {
        DateTime from = adjudicatedUtc.AddDays(-DiagnosticStewardshipThresholds.DeltaWindowDays);
        if (episode.AssertedUtc > from) from = episode.AssertedUtc;

        var newKeys = new List<string>();
        var abnormalKeys = new List<string>();

        try
        {
            var labIndex = GrainFactory.GetGrain<IPatientLabIndex>($"PatientLabIndex/{PatientId}");
            IReadOnlyList<LabIndexEntry> labs =
                await labIndex.GetByDateRange(new DateTimeOffset(from, TimeSpan.Zero),
                                              new DateTimeOffset(adjudicatedUtc, TimeSpan.Zero));

            foreach (LabIndexEntry lab in labs)
            {
                if (string.IsNullOrWhiteSpace(lab.LoincCode)) continue;

                string key = $"L:{lab.LoincCode}";
                newKeys.Add(key);
                if (lab.AbnormalFlag != LabAbnormalFlag.Normal)
                    abnormalKeys.Add(key);
            }
        }
        catch
        {
            // The delta is an enrichment, not the record. If the lab index is unavailable the
            // adjudication must still be counted — losing the outcome would corrupt the
            // denominator, which is far worse than losing a discriminator observation.
        }

        return (newKeys.Distinct(StringComparer.Ordinal).ToList(),
                abnormalKeys.Distinct(StringComparer.Ordinal).ToList());
    }

    private static string TestKeyOf(EvidenceRef r) => r.Kind switch
    {
        EvidenceKind.LabResult => $"L:{r.Code}",
        EvidenceKind.Imaging => $"R:{r.Code}",
        EvidenceKind.Procedure => $"C:{r.Code}",
        _ => $"E:{r.Code}"
    };
}
