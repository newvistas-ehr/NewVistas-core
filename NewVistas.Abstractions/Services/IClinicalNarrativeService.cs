// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Text;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Services;

/// <summary>
/// Seam for turning a grounded clinical context into narrative prose. This is a SEAM
/// only: the default registration is <see cref="TemplateClinicalNarrativeService"/>,
/// which composes a deterministic, fully-grounded summary offline (no model, no network).
///
/// The contract enforces the architecture, not just the call: the service is handed a
/// <see cref="ClinicalSummaryContext"/> of discrete, already-retrieved facts and must
/// return claims that each cite the fact ids they came from. The model's job is to
/// NARRATE supplied facts, never to be the source of them — which is what lets every
/// claim be verified against the chart afterwards. A live implementation (Azure OpenAI,
/// Claude, etc.) drops in behind this interface; the surrounding grounding + verification
/// harness is unchanged, because the model is the least load-bearing part.
/// </summary>
public interface IClinicalNarrativeService
{
    /// <summary>Whether a live model backs this service. False for the offline default.</summary>
    bool IsLiveModel { get; }

    /// <summary>Provider label recorded on the draft (e.g., "offline-template", "claude").</summary>
    string ProviderName { get; }

    /// <summary>
    /// Composes a narrative from the grounded context, returning the prose plus the
    /// claims that compose it, each tagged with the supporting fact ids.
    /// </summary>
    Task<NarrativeResult> ComposeAsync(ClinicalSummaryContext context, CancellationToken cancellationToken = default);
}

/// <summary>Output of a narrative composition: the prose and its constituent claims.</summary>
public sealed class NarrativeResult
{
    public string Narrative { get; init; } = string.Empty;
    public List<SummaryClaim> Claims { get; init; } = new();
}

/// <summary>
/// Offline default. Composes a sectioned summary directly from the structured facts —
/// one claim per clinical domain, each citing exactly the facts it lists. Because the
/// prose is built FROM the facts (not generated then matched to them), every claim is
/// grounded by construction. This demonstrates the "structured-first" principle and
/// gives a faithful baseline that runs with no model and no network.
/// </summary>
public sealed class TemplateClinicalNarrativeService : IClinicalNarrativeService
{
    public bool IsLiveModel => false;
    public string ProviderName => "offline-template";

    public Task<NarrativeResult> ComposeAsync(ClinicalSummaryContext context, CancellationToken cancellationToken = default)
    {
        List<SummaryClaim> claims = new();
        StringBuilder narrative = new();
        narrative.Append(string.IsNullOrWhiteSpace(context.Purpose)
            ? "Clinical summary."
            : $"Clinical summary for {context.Purpose}.");

        foreach (IGrouping<ClinicalFactCategory, ClinicalFact> group in
                 context.Facts.GroupBy(f => f.Category).OrderBy(g => g.Key))
        {
            List<ClinicalFact> items = group.ToList();
            string sentence = $"{SectionLabel(group.Key)}: {string.Join("; ", items.Select(f => f.Text))}.";

            narrative.Append(' ').Append(sentence);
            claims.Add(new SummaryClaim
            {
                Text = sentence,
                SupportingFactIds = items.Select(f => f.FactId).ToList(),
            });
        }

        if (context.Facts.Count == 0)
            narrative.Append(" No active problems, medications, allergies, or recent results on file.");

        return Task.FromResult(new NarrativeResult
        {
            Narrative = narrative.ToString(),
            Claims = claims,
        });
    }

    private static string SectionLabel(ClinicalFactCategory category) => category switch
    {
        ClinicalFactCategory.Problem => "Active problems",
        ClinicalFactCategory.Medication => "Active medications",
        ClinicalFactCategory.Allergy => "Allergies",
        ClinicalFactCategory.Lab => "Recent results",
        ClinicalFactCategory.Vital => "Vitals",
        _ => "Other",
    };
}
