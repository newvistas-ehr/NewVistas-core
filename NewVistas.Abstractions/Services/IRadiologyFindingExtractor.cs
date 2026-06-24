// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Text.RegularExpressions;
using Orleans;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Services;

/// <summary>
/// Seam for extracting discrete, source-anchored findings from a radiology report. SEAM
/// only: the default registration is <see cref="HeuristicRadiologyFindingExtractor"/>, which
/// runs offline with no model. A live Claude extractor drops in behind this interface; the
/// extractor only ever surfaces sentences the radiologist wrote (each finding carries its
/// verbatim source quote), and whatever it returns is re-checked against the report by
/// <see cref="RadiologyFindingVerifier"/>.
/// </summary>
public interface IRadiologyFindingExtractor
{
    bool IsLiveModel { get; }
    string ProviderName { get; }

    /// <summary>Extracts findings from the report text.</summary>
    Task<RadiologyExtractionResult> ExtractAsync(string reportText, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of an extraction pass. Serializable because it crosses the worker-grain boundary
/// back to the per-report extraction grain.
/// </summary>
[GenerateSerializer]
public sealed class RadiologyExtractionResult
{
    [Id(0)]
    public List<RadiologyFinding> Findings { get; set; } = new();

    [Id(1)]
    public string ProviderName { get; set; } = string.Empty;
}

/// <summary>
/// Offline default. A deterministic, no-model baseline: it scans the report sentence by
/// sentence and emits a finding for each sentence that names a structural finding
/// (stenosis / narrowing / herniation / compression) together with a graded severity,
/// anchoring the finding to that exact sentence. It is intentionally simple — the live
/// model does the nuanced extraction — but it grounds every finding in a real sentence
/// and lets the whole workflow run with no network.
/// </summary>
public sealed class HeuristicRadiologyFindingExtractor : IRadiologyFindingExtractor
{
    public bool IsLiveModel => false;
    public string ProviderName => "offline-heuristic";

    private static readonly Regex SeverityRx = new(
        @"\b(moderate[\s-]+to[\s-]+severe|mild[\s-]+to[\s-]+moderate|severe|moderate|mild|minimal)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LevelRx = new(
        @"\b([CTLS]\d+(?:\s*[-/]\s*[CTLS]?\d+)?)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] FindingTerms =
        { "stenosis", "stenoses", "narrowing", "herniation", "herniated", "protrusion", "compression" };

    public Task<RadiologyExtractionResult> ExtractAsync(string reportText, CancellationToken cancellationToken = default)
    {
        List<RadiologyFinding> findings = new();
        int n = 0;

        foreach (string raw in SplitSentences(reportText))
        {
            string sentence = raw.Trim();
            if (sentence.Length == 0)
                continue;

            string lower = sentence.ToLowerInvariant();
            if (!FindingTerms.Any(t => lower.Contains(t)))
                continue;

            Match sev = SeverityRx.Match(sentence);
            if (!sev.Success)
                continue;

            findings.Add(new RadiologyFinding
            {
                FindingId = $"RF{++n}",
                FindingType = ClassifyType(lower),
                Level = LevelRx.Match(sentence) is { Success: true } m ? m.Groups[1].Value.ToUpperInvariant() : string.Empty,
                Laterality = ClassifyLaterality(lower),
                Severity = ClassifySeverity(sev.Value),
                SeverityText = sev.Value.ToLowerInvariant(),
                SourceQuote = sentence,
            });
        }

        return Task.FromResult(new RadiologyExtractionResult { Findings = findings, ProviderName = ProviderName });
    }

    private static IEnumerable<string> SplitSentences(string text) =>
        Regex.Split(text ?? string.Empty, @"(?<=[.!?])\s+|[\r\n]+");

    private static string ClassifyType(string lower)
    {
        if (lower.Contains("foramin")) return "Neural foraminal stenosis";
        if (lower.Contains("central") || lower.Contains("canal") || lower.Contains("cord")) return "Central canal stenosis";
        if (lower.Contains("herniat") || lower.Contains("protrusion")) return "Disc herniation";
        if (lower.Contains("narrowing")) return "Narrowing";
        return "Stenosis";
    }

    private static FindingLaterality ClassifyLaterality(string lower)
    {
        if (lower.Contains("bilateral")) return FindingLaterality.Bilateral;
        bool left = lower.Contains("left");
        bool right = lower.Contains("right");
        if (left && right) return FindingLaterality.Bilateral;
        if (left) return FindingLaterality.Left;
        if (right) return FindingLaterality.Right;
        return FindingLaterality.Unspecified;
    }

    private static FindingSeverity ClassifySeverity(string text)
    {
        string s = text.ToLowerInvariant();
        if (s.Contains("severe")) return FindingSeverity.Severe;
        if (s.Contains("moderate")) return FindingSeverity.Moderate;
        if (s.Contains("mild")) return FindingSeverity.Mild;
        if (s.Contains("minimal")) return FindingSeverity.Minimal;
        return FindingSeverity.Unspecified;
    }
}
