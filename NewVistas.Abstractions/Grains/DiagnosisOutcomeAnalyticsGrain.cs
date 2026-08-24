// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Orleans;
using Orleans.Concurrency;
using NewVistas.Abstractions.Clinical;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Grains;

/// <summary>
/// Read side for diagnostic stewardship (ADR-006). Stateless: merges shards across the read
/// window, applies the floors, and produces the clinician-facing advisory.
///
/// Everything about this grain is arranged so that the default outcome is <i>silence</i>. It is
/// far better to say nothing than to tell a clinician a wrong number about how often they are
/// wrong.
/// </summary>
[StatelessWorker]
public class DiagnosisOutcomeAnalyticsGrain : Grain, IDiagnosisOutcomeAnalyticsGrain
{
    public async Task<DiagnosisRevisionAdvisory> GetAdvisoryAsync(string workingCode, string workingDisplay)
    {
        string code = DiagnosisCodeRelation.Normalize(workingCode);
        string cat = DiagnosisCodeRelation.Category3(code);
        int thisYear = DateTime.UtcNow.Year;

        var advisory = new DiagnosisRevisionAdvisory
        {
            WorkingCode = code,
            WorkingDisplay = workingDisplay,
            GeneratedAt = DateTime.UtcNow
        };

        // Escalation ladder: exact code across the window, then widen to the category. The
        // provenance is relabelled at each step so the UI never presents a category rate as if
        // it were the code's own.
        DiagnosisOutcomeState exact = await MergeWindowAsync(DiagnosisCodeGranularity.Code, code, thisYear);
        DiagnosisOutcomeState used = exact;
        advisory.RateProvenance = RateProvenance.ExactCode;

        if (exact.AdjudicatedUnexposedCount < DiagnosticStewardshipThresholds.MinAdjudicatedForRate)
        {
            DiagnosisOutcomeState byCat =
                await MergeWindowAsync(DiagnosisCodeGranularity.Category, cat, thisYear);
            if (byCat.AdjudicatedUnexposedCount >= DiagnosticStewardshipThresholds.MinAdjudicatedForRate)
            {
                used = byCat;
                advisory.RateProvenance = RateProvenance.Category;
            }
        }

        DiagnosisOutcomeState siteWide =
            await MergeWindowAsync(DiagnosisCodeGranularity.All, "ALL", thisYear);

        Populate(advisory, used, siteWide);
        return DiagnosticRevisionCatalog.Merge(advisory, code, DateTime.UtcNow);
    }

    /// <summary>
    /// Sum the assertion-year buckets across the read window. Buckets exist because diagnostic
    /// criteria move; summing them for power is the trade, and the current-versus-prior
    /// comparison below is what stops the pooling from hiding a sudden change.
    /// </summary>
    private async Task<DiagnosisOutcomeState> MergeWindowAsync(
        DiagnosisCodeGranularity granularity, string codeKey, int thisYear)
    {
        var merged = new DiagnosisOutcomeState
        {
            Granularity = granularity,
            CodeKey = codeKey,
            AssertionYear = thisYear
        };

        for (int i = 0; i < DiagnosticStewardshipThresholds.ReadWindowYears; i++)
        {
            int year = thisYear - i;
            var shard = GrainFactory.GetGrain<IDiagnosisOutcomeIndexGrain>(
                DiagnosisOutcomeIndexGrain.KeyFor(granularity, codeKey, year));
            DiagnosisOutcomeState s = await shard.GetStateAsync();
            if (s.AssertedCount == 0 && s.AdjudicatedCount == 0) continue;

            merged.AssertedCount += s.AssertedCount;
            merged.AdjudicatedCount += s.AdjudicatedCount;
            merged.ConfirmedCount += s.ConfirmedCount;
            merged.RevisedCount += s.RevisedCount;
            merged.RefinedCount += s.RefinedCount;
            merged.BroadenedCount += s.BroadenedCount;
            merged.ResolvedWithoutAlternateCount += s.ResolvedWithoutAlternateCount;
            merged.ClosedUnadjudicatedCount += s.ClosedUnadjudicatedCount;
            merged.RecodedCount += s.RecodedCount;
            merged.AdjudicatedUnexposedCount += s.AdjudicatedUnexposedCount;
            merged.RevisedUnexposedCount += s.RevisedUnexposedCount;
            merged.NosTerminatingRevisedCount += s.NosTerminatingRevisedCount;
            foreach (string p in s.AdjudicatingProviderIds) merged.AdjudicatingProviderIds.Add(p);

            foreach (DiagnosisRevisionStat r in s.RevisedTo)
            {
                DiagnosisRevisionStat? t = merged.RevisedTo.FirstOrDefault(x => x.OutcomeCode == r.OutcomeCode);
                if (t is null)
                {
                    merged.RevisedTo.Add(new DiagnosisRevisionStat
                    {
                        OutcomeCode = r.OutcomeCode,
                        OutcomeDisplay = r.OutcomeDisplay,
                        Count = r.Count,
                        CountUnexposed = r.CountUnexposed,
                        LastSeenUtc = r.LastSeenUtc
                    });
                }
                else
                {
                    t.Count += r.Count;
                    t.CountUnexposed += r.CountUnexposed;
                }
            }

            foreach (DiscriminatorStat d in s.Discriminators)
            {
                DiscriminatorStat? t = merged.Discriminators.FirstOrDefault(x => x.TestKey == d.TestKey);
                if (t is null)
                {
                    merged.Discriminators.Add(new DiscriminatorStat
                    {
                        TestKey = d.TestKey,
                        Kind = d.Kind,
                        Display = d.Display,
                        NewInRevised = d.NewInRevised,
                        NewInNotRevised = d.NewInNotRevised,
                        NewAndAbnormalInRevised = d.NewAndAbnormalInRevised,
                        NewAndAbnormalInNotRevised = d.NewAndAbnormalInNotRevised,
                        AlreadyPresentAtAssertion = d.AlreadyPresentAtAssertion,
                        NewInRevisedUnexposed = d.NewInRevisedUnexposed,
                        NewInNotRevisedUnexposed = d.NewInNotRevisedUnexposed,
                        NewAndAbnormalInRevisedUnexposed = d.NewAndAbnormalInRevisedUnexposed,
                        NewAndAbnormalInNotRevisedUnexposed = d.NewAndAbnormalInNotRevisedUnexposed,
                        LastSeenUtc = d.LastSeenUtc
                    });
                }
                else
                {
                    t.NewInRevised += d.NewInRevised;
                    t.NewInNotRevised += d.NewInNotRevised;
                    t.NewAndAbnormalInRevised += d.NewAndAbnormalInRevised;
                    t.NewAndAbnormalInNotRevised += d.NewAndAbnormalInNotRevised;
                    t.AlreadyPresentAtAssertion += d.AlreadyPresentAtAssertion;
                    t.NewInRevisedUnexposed += d.NewInRevisedUnexposed;
                    t.NewInNotRevisedUnexposed += d.NewInNotRevisedUnexposed;
                    t.NewAndAbnormalInRevisedUnexposed += d.NewAndAbnormalInRevisedUnexposed;
                    t.NewAndAbnormalInNotRevisedUnexposed += d.NewAndAbnormalInNotRevisedUnexposed;
                }
            }
        }

        return merged;
    }

    private static void Populate(
        DiagnosisRevisionAdvisory a, DiagnosisOutcomeState s, DiagnosisOutcomeState siteWide)
    {
        a.AdjudicatedCount = s.AdjudicatedUnexposedCount;
        a.RevisedCount = s.RevisedUnexposedCount;
        a.RefinedCount = s.RefinedCount;
        a.BroadenedCount = s.BroadenedCount;
        // Recoded episodes are closed, not open — omitting them here would report every
        // recoded case as an outstanding workup forever.
        a.StillOpenCount = Math.Max(0,
            s.AssertedCount - s.AdjudicatedCount - s.ClosedUnadjudicatedCount - s.RecodedCount);
        a.ClosedUnadjudicatedCount = s.ClosedUnadjudicatedCount;
        a.NosTerminatingRevisedCount = s.NosTerminatingRevisedCount;
        a.DistinctAdjudicatingProviders = s.AdjudicatingProviderIds.Count;

        // Every gate below sets Insufficient and leaves RevisionRate null. The DTO contract is
        // that a null rate is never rendered — a caveat beside a number does not stop the number
        // being read, and this is the one place over-calling a diagnosis wrong is most costly.
        string? reason = null;
        if (s.AdjudicatedUnexposedCount < DiagnosticStewardshipThresholds.MinAdjudicatedForRate)
            reason = $"only {s.AdjudicatedUnexposedCount} adjudicated episodes without the advisory " +
                     $"(need {DiagnosticStewardshipThresholds.MinAdjudicatedForRate})";
        else if (s.AdjudicatingProviderIds.Count < DiagnosticStewardshipThresholds.MinDistinctProviders)
            reason = "too few distinct adjudicating clinicians — one clinician's practice must " +
                     "not become the site's statistic";
        else
        {
            int denominatorBase = s.AdjudicatedCount + s.ClosedUnadjudicatedCount;
            double coverage = denominatorBase == 0 ? 0 : (double)s.AdjudicatedCount / denominatorBase;
            if (coverage < DiagnosticStewardshipThresholds.MinAdjudicationCoverage)
                reason = $"only {coverage:P0} of episodes were adjudicated — a rate from this " +
                         "sample would be biased toward the memorable cases";
        }

        if (reason is not null)
        {
            a.Band = RevisionRateBand.Insufficient;
            a.InsufficientReason = reason;
            a.IsColdStart = s.AdjudicatedCount == 0;
            a.RevisionRate = null;
            return;
        }

        double rate = (double)s.RevisedUnexposedCount / s.AdjudicatedUnexposedCount;
        a.RevisionRate = rate;

        double siteRate = siteWide.AdjudicatedUnexposedCount == 0
            ? 0
            : (double)siteWide.RevisedUnexposedCount / siteWide.AdjudicatedUnexposedCount;
        a.SiteWideRevisionRate = siteWide.AdjudicatedUnexposedCount == 0 ? null : siteRate;

        double lift = siteRate <= 0 ? 0 : rate / siteRate;
        a.LiftOverSiteWide = siteRate <= 0 ? null : lift;

        a.Band = siteRate <= 0
            ? RevisionRateBand.Insufficient
            : lift >= DiagnosticStewardshipThresholds.SignalLift
              && rate >= DiagnosticStewardshipThresholds.MinRateToReport
                ? RevisionRateBand.Elevated
                : lift <= DiagnosticStewardshipThresholds.NoiseLiftCeiling
                    ? RevisionRateBand.Typical
                    : RevisionRateBand.Borderline;

        if (a.Band == RevisionRateBand.Insufficient)
        {
            a.InsufficientReason = "no site-wide baseline to compare against yet";
            a.RevisionRate = null;
            return;
        }

        a.Alternatives = s.RevisedTo
            .Where(r => r.CountUnexposed >= DiagnosticStewardshipThresholds.MinRevisionsForAlternative)
            .OrderByDescending(r => r.CountUnexposed)
            .Select(r => new DiagnosisAlternative
            {
                Code = r.OutcomeCode,
                Display = r.OutcomeDisplay,
                Count = r.CountUnexposed,
                OutOf = s.RevisedUnexposedCount
            })
            .ToList();

        a.SuggestedTests = ScoreDiscriminators(s);
    }

    private static List<DiagnosticTestSuggestion> ScoreDiscriminators(DiagnosisOutcomeState s)
    {
        int revised = s.RevisedUnexposedCount;
        int notRevised = s.AdjudicatedUnexposedCount - s.RevisedUnexposedCount;

        // Both arms must clear their floor. A lift with an empty comparison arm is not a lift,
        // it is a raw count wearing a ratio's clothes.
        if (revised < DiagnosticStewardshipThresholds.MinRevisedForDiscriminator) return new();
        if (notRevised < DiagnosticStewardshipThresholds.MinNotRevisedForComparison) return new();

        var results = new List<DiagnosticTestSuggestion>();
        foreach (DiscriminatorStat d in s.Discriminators)
        {
            double revisedRate = (double)d.NewInRevisedUnexposed / revised;
            double notRevisedRate = (double)d.NewInNotRevisedUnexposed / notRevised;

            SignalVerdict verdict;
            double? lift = null;
            if (notRevisedRate <= 0)
            {
                // Present in one arm and never the other. Suggestive, but with a zero
                // denominator it is not a measured ratio — report it as insufficient rather
                // than as an infinite lift.
                verdict = SignalVerdict.Insufficient;
            }
            else
            {
                lift = revisedRate / notRevisedRate;
                verdict = lift >= DiagnosticStewardshipThresholds.SignalLift
                    ? SignalVerdict.Signal
                    : lift <= DiagnosticStewardshipThresholds.NoiseLiftCeiling
                        ? SignalVerdict.Noise
                        : SignalVerdict.Insufficient;
            }

            // Noise is suppressed: a universally ordered CBC lands at lift ≈ 1 and must never
            // be presented as the test that would have made the difference.
            if (verdict != SignalVerdict.Signal) continue;

            results.Add(new DiagnosticTestSuggestion
            {
                TestKey = d.TestKey,
                Display = d.Display,
                Kind = d.Kind,
                ArrivedBeforeRevision = d.NewInRevisedUnexposed,
                RevisedTotal = revised,
                ArrivedInNotRevised = d.NewInNotRevisedUnexposed,
                NotRevisedTotal = notRevised,
                Lift = lift,
                Verdict = verdict
            });
        }

        return results.OrderByDescending(r => r.Lift ?? 0).ToList();
    }
}
