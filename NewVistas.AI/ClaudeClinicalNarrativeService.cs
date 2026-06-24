// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Anthropic;
using Anthropic.Models.Messages;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Services;

namespace NewVistas.AI;

/// <summary>
/// Live <see cref="IClinicalNarrativeService"/> backed by Claude via the official
/// Anthropic .NET SDK. It is handed an already-retrieved, grounded fact set and asked
/// to narrate ONLY those facts and cite the FactId(s) per claim (see
/// <see cref="ClinicalNarrativeJson"/>). The model is the least load-bearing part:
/// whatever it returns is parsed deterministically and then re-verified against the
/// source facts downstream, and a failure here is caught by the resilient decorator,
/// which falls back to the offline grounded template.
///
/// Hardening left for production: turn on structured outputs (output_config.format) for
/// a schema guarantee instead of prompt-instructed JSON, and stream for large outputs.
/// </summary>
public sealed class ClaudeClinicalNarrativeService : IClinicalNarrativeService
{
    private readonly AnthropicClient _client;
    private readonly ClinicalNarrativeOptions _options;

    public ClaudeClinicalNarrativeService(ClinicalNarrativeOptions options)
    {
        _options = options;
        _client = string.IsNullOrWhiteSpace(options.ApiKey)
            ? new AnthropicClient()                          // reads ANTHROPIC_API_KEY from env
            : new AnthropicClient { ApiKey = options.ApiKey };
    }

    public bool IsLiveModel => true;

    public string ProviderName => "claude";

    public async Task<NarrativeResult> ComposeAsync(
        ClinicalSummaryContext context, CancellationToken cancellationToken = default)
    {
        Message response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = _options.Model,
            MaxTokens = _options.MaxTokens,
            System = ClinicalNarrativeJson.SystemPrompt,
            Thinking = new ThinkingConfigAdaptive(),
            Messages = [new() { Role = Role.User, Content = ClinicalNarrativeJson.BuildUserPrompt(context) }],
        });

        // Thinking blocks (if any) precede the text; concatenate only the text blocks.
        string text = string.Concat(
            response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));

        return ClinicalNarrativeJson.Parse(text, ProviderName);
    }
}
