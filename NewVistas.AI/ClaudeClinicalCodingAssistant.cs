// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Anthropic;
using Anthropic.Models.Messages;
using NewVistas.Abstractions.Services;

namespace NewVistas.AI;

/// <summary>
/// Live <see cref="IClinicalCodingAssistant"/> backed by Claude via the official Anthropic
/// .NET SDK, constrained to the claims schema via structured outputs. The schema carries no
/// code field, so the model structurally cannot emit an ICD-10 code — it extracts claims with
/// verbatim quotes, and codes are resolved afterwards from the site's own index. Output is
/// parsed deterministically (<see cref="ClinicalCodingJson"/>) and every quote is re-verified
/// against the note; a failure here is caught by the resilient decorator, which falls back to
/// the offline lexicon.
/// </summary>
public sealed class ClaudeClinicalCodingAssistant : IClinicalCodingAssistant
{
    private readonly AnthropicClient _client;
    private readonly ClinicalNarrativeOptions _options;

    public ClaudeClinicalCodingAssistant(ClinicalNarrativeOptions options)
    {
        _options = options;
        _client = string.IsNullOrWhiteSpace(options.ApiKey)
            ? new AnthropicClient()
            : new AnthropicClient { ApiKey = options.ApiKey };
    }

    public bool IsLiveModel => true;

    public string ProviderName => "claude";

    public async Task<CodingClaimsResult> SuggestClaimsAsync(
        string noteText, CancellationToken cancellationToken = default)
    {
        Message response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = _options.Model,
            MaxTokens = _options.MaxTokens,
            System = ClinicalCodingJson.SystemPrompt,
            Thinking = new ThinkingConfigAdaptive(),
            OutputConfig = new OutputConfig
            {
                Format = new JsonOutputFormat { Schema = ClinicalCodingJson.BuildResponseSchema() },
            },
            Messages = [new() { Role = Role.User, Content = ClinicalCodingJson.BuildUserPrompt(noteText) }],
        });

        string text = string.Concat(
            response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));

        return ClinicalCodingJson.Parse(text, ProviderName);
    }
}
