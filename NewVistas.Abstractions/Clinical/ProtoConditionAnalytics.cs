// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.Abstractions.Clinical;

/// <summary>Verdict on whether a feature distinguishes the cluster from the background.</summary>
[GenerateSerializer]
public enum SignalVerdict
{
    /// <summary>Too few assessed/present members to judge, or lift in the ambiguous middle.</summary>
    Insufficient = 0,
    /// <summary>Cluster rate is at/near the background rate — the feature is not discriminating.</summary>
    Noise = 1,
    /// <summary>Cluster rate is well above background — a discriminating feature.</summary>
    Signal = 2
}

/// <summary>A background prevalence for a feature, with its provenance (input to the analytics).</summary>
[GenerateSerializer]
public record BackgroundRate
{
    [Id(0)] public string FeatureId { get; set; } = string.Empty;
    [Id(1)] public double Rate { get; set; }
    /// <summary>Where this rate came from (e.g. "assessed population (n=142)", "curated catalog").</summary>
    [Id(2)] public string Source { get; set; } = string.Empty;
}

/// <summary>The lift analysis for one feature over the confirmed cohort.</summary>
[GenerateSerializer]
public record FeatureSignal
{
    [Id(0)] public string FeatureId { get; set; } = string.Empty;
    [Id(1)] public string Display { get; set; } = string.Empty;
    [Id(2)] public ProtoFeatureKind Kind { get; set; }
    /// <summary>Confirmed members whose feature was Satisfied.</summary>
    [Id(3)] public int ClusterPresent { get; set; }
    /// <summary>Confirmed members whose feature could be assessed (the honest denominator).</summary>
    [Id(4)] public int ClusterAssessed { get; set; }
    [Id(5)] public double ClusterRate { get; set; }
    [Id(6)] public double BackgroundRate { get; set; }
    [Id(7)] public string BackgroundSource { get; set; } = string.Empty;
    /// <summary>ClusterRate ÷ BackgroundRate (double.PositiveInfinity when background is 0 and cluster &gt; 0).</summary>
    [Id(8)] public double Lift { get; set; }
    [Id(9)] public SignalVerdict Verdict { get; set; }
    [Id(10)] public string Note { get; set; } = string.Empty;
}

/// <summary>Kind of refinement the analytics suggests (human decides; never auto-applied).</summary>
[GenerateSerializer]
public enum RefinementKind
{
    RaiseWeight = 0,
    LowerWeight = 1,
    DropFeature = 2,
    AddCandidateFeature = 3
}

/// <summary>A suggested edit to the case definition, with its rationale.</summary>
[GenerateSerializer]
public record RefinementSuggestion
{
    [Id(0)] public RefinementKind Kind { get; set; }
    [Id(1)] public string? FeatureId { get; set; }
    [Id(2)] public string Display { get; set; } = string.Empty;
    [Id(3)] public string Rationale { get; set; } = string.Empty;
}

/// <summary>Pairwise co-occurrence of two Signal features across confirmed members (split evidence).</summary>
[GenerateSerializer]
public record FeatureCoOccurrence
{
    [Id(0)] public string FeatureAId { get; set; } = string.Empty;
    [Id(1)] public string FeatureBId { get; set; } = string.Empty;
    [Id(2)] public int BothPresent { get; set; }
    [Id(3)] public int OnlyA { get; set; }
    [Id(4)] public int OnlyB { get; set; }
    /// <summary>Fraction of members with exactly one of the two features (high = candidate split axis).</summary>
    [Id(5)] public double AntiCorrelation { get; set; }
}

/// <summary>One row of the member × feature grid (which features a member satisfies).</summary>
[GenerateSerializer]
public record MemberFeatureRow
{
    [Id(0)] public string PatientId { get; set; } = string.Empty;
    [Id(1)] public List<string> SatisfiedFeatureIds { get; set; } = new();
}

/// <summary>The full net-closing analytics report for a proto-condition.</summary>
[GenerateSerializer]
public record ProtoAnalyticsReport
{
    [Id(0)] public int ConfirmedCount { get; set; }
    [Id(1)] public List<FeatureSignal> Signals { get; set; } = new();
    [Id(2)] public List<RefinementSuggestion> Suggestions { get; set; } = new();
    [Id(3)] public List<FeatureCoOccurrence> CoOccurrences { get; set; } = new();
    [Id(4)] public List<MemberFeatureRow> Grid { get; set; } = new();
}

/// <summary>
/// Net-closing analytics for a proto-condition. Deterministic and documented — this is local
/// operational triage (which feature discriminates, what the working case definition should be
/// TODAY), NOT etiological discovery (that is the CDC's job across sites). Every rate is over an
/// ASSESSED denominator (we only divide by patients we actually asked), and every lift verdict is a
/// documented heuristic with minimum-N guards — explicitly NOT a p-value.
/// </summary>
public static class ProtoConditionAnalytics
{
    // Documented heuristic thresholds (illustrative — deliberately conservative, never significance tests).
    private const int MinAssessed = 5;
    private const int MinPresent = 3;
    private const double SignalLift = 2.0;
    private const double NoiseLiftCeiling = 1.3;
    private const int MaxSuggestions = 20;

    /// <summary>
    /// Analyzes the confirmed cohort. <paramref name="backgrounds"/> supplies each feature's
    /// background prevalence (assembled by the caller from the assessed population or the curated
    /// catalog); features without a background get an <see cref="SignalVerdict.Insufficient"/> verdict.
    /// </summary>
    public static ProtoAnalyticsReport Analyze(ProtoConditionState proto, IReadOnlyList<BackgroundRate> backgrounds)
    {
        Dictionary<string, BackgroundRate> bg = backgrounds
            .GroupBy(b => b.FeatureId).ToDictionary(g => g.Key, g => g.First());

        List<ProtoMember> confirmed = proto.Members
            .Where(m => m.Status == ProtoMemberStatus.Confirmed).ToList();

        var signals = new List<FeatureSignal>();
        foreach (ProtoFeature f in proto.Features)
        {
            List<FeatureContribution> contribs = confirmed
                .Select(m => m.Contributions.FirstOrDefault(c => c.FeatureId == f.FeatureId))
                .Where(c => c is not null)
                .Select(c => c!)
                .ToList();

            int assessed = contribs.Count(c => c.Assessed);
            int present = contribs.Count(c => c.Satisfied);
            double clusterRate = assessed > 0 ? (double)present / assessed : 0.0;

            bg.TryGetValue(f.FeatureId, out BackgroundRate? b);
            double backgroundRate = b?.Rate ?? 0.0;
            string backgroundSource = b?.Source ?? "no background available";
            double lift = backgroundRate > 0 ? clusterRate / backgroundRate
                        : (clusterRate > 0 ? double.PositiveInfinity : 0.0);

            (SignalVerdict verdict, string note) = Judge(assessed, present, lift, b is not null);

            signals.Add(new FeatureSignal
            {
                FeatureId = f.FeatureId,
                Display = f.Display,
                Kind = f.Kind,
                ClusterPresent = present,
                ClusterAssessed = assessed,
                ClusterRate = clusterRate,
                BackgroundRate = backgroundRate,
                BackgroundSource = backgroundSource,
                Lift = lift,
                Verdict = verdict,
                Note = note
            });
        }

        signals = signals
            .OrderByDescending(s => s.Verdict)
            .ThenByDescending(s => double.IsInfinity(s.Lift) ? double.MaxValue : s.Lift)
            .ToList();

        return new ProtoAnalyticsReport
        {
            ConfirmedCount = confirmed.Count,
            Signals = signals,
            Suggestions = BuildSuggestions(proto, signals),
            CoOccurrences = BuildCoOccurrences(confirmed, signals),
            Grid = BuildGrid(confirmed, proto)
        };
    }

    private static (SignalVerdict, string) Judge(int assessed, int present, double lift, bool hasBackground)
    {
        if (!hasBackground)
            return (SignalVerdict.Insufficient, "no background prevalence available");
        if (assessed < MinAssessed || present < MinPresent)
            return (SignalVerdict.Insufficient, $"too few assessed/present (n={assessed}, present={present}; need ≥{MinAssessed}/≥{MinPresent})");
        if (lift >= SignalLift)
            return (SignalVerdict.Signal, $"cluster rate {lift:0.0}× background");
        if (lift <= NoiseLiftCeiling)
            return (SignalVerdict.Noise, $"cluster rate ≈ background ({lift:0.0}×)");
        return (SignalVerdict.Insufficient, $"borderline ({lift:0.0}×)");
    }

    private static List<RefinementSuggestion> BuildSuggestions(ProtoConditionState proto, List<FeatureSignal> signals)
    {
        var suggestions = new List<RefinementSuggestion>();
        Dictionary<string, ProtoFeature> byId = proto.Features.ToDictionary(f => f.FeatureId);
        double maxWeight = proto.Features.Where(f => f.Rule == ProtoFeatureRule.Weighted)
            .Select(f => f.Weight).DefaultIfEmpty(1.0).Max();

        foreach (FeatureSignal s in signals)
        {
            if (!byId.TryGetValue(s.FeatureId, out ProtoFeature? f) || f.Rule != ProtoFeatureRule.Weighted)
                continue;

            if (s.Verdict == SignalVerdict.Noise)
            {
                suggestions.Add(new RefinementSuggestion
                {
                    Kind = RefinementKind.DropFeature,
                    FeatureId = s.FeatureId,
                    Display = s.Display,
                    Rationale = $"'{s.Display}' occurs at ~background rate in the cluster ({s.Note}). The net closes by dropping it."
                });
            }
            else if (s.Verdict == SignalVerdict.Signal && (double.IsInfinity(s.Lift) || s.Lift >= 2 * SignalLift) && f.Weight < maxWeight)
            {
                suggestions.Add(new RefinementSuggestion
                {
                    Kind = RefinementKind.RaiseWeight,
                    FeatureId = s.FeatureId,
                    Display = s.Display,
                    Rationale = $"'{s.Display}' is strongly discriminating ({s.Note}) but under-weighted — consider raising its weight."
                });
            }
        }

        return suggestions.Take(MaxSuggestions).ToList();
    }

    private static List<FeatureCoOccurrence> BuildCoOccurrences(List<ProtoMember> confirmed, List<FeatureSignal> signals)
    {
        List<string> signalFeatures = signals
            .Where(s => s.Verdict == SignalVerdict.Signal)
            .Select(s => s.FeatureId).ToList();

        var pairs = new List<FeatureCoOccurrence>();
        for (int i = 0; i < signalFeatures.Count; i++)
        for (int j = i + 1; j < signalFeatures.Count; j++)
        {
            string a = signalFeatures[i], b = signalFeatures[j];
            int both = 0, onlyA = 0, onlyB = 0;
            foreach (ProtoMember m in confirmed)
            {
                bool hasA = m.Contributions.Any(c => c.FeatureId == a && c.Satisfied);
                bool hasB = m.Contributions.Any(c => c.FeatureId == b && c.Satisfied);
                if (hasA && hasB) both++;
                else if (hasA) onlyA++;
                else if (hasB) onlyB++;
            }
            int withEither = both + onlyA + onlyB;
            double anti = withEither > 0 ? (double)(onlyA + onlyB) / withEither : 0.0;
            pairs.Add(new FeatureCoOccurrence
            {
                FeatureAId = a, FeatureBId = b,
                BothPresent = both, OnlyA = onlyA, OnlyB = onlyB,
                AntiCorrelation = anti
            });
        }
        return pairs.OrderByDescending(p => p.AntiCorrelation).ToList();
    }

    private static List<MemberFeatureRow> BuildGrid(List<ProtoMember> confirmed, ProtoConditionState proto) =>
        confirmed.Select(m => new MemberFeatureRow
        {
            PatientId = m.PatientId,
            SatisfiedFeatureIds = m.Contributions.Where(c => c.Satisfied).Select(c => c.FeatureId).ToList()
        }).ToList();
}
