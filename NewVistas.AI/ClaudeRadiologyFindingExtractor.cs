// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Anthropic;
using Anthropic.Models.Messages;
using NewVistas.Abstractions.Services;

namespace NewVistas.AI;

/// <summary>
/// Live <see cref="IRadiologyFindingExtractor"/> backed by Claude via the official Anthropic
/// .NET SDK, constrained to the findings schema via structured outputs. It surfaces only
/// findings the radiologist documented, each with a verbatim source quote — extraction with
/// citation, not diagnosis. Output is parsed deterministically (<see cref="RadiologyFindingJson"/>)
/// and re-verified against the report; a failure here is caught by the resilient decorator,
/// which falls back to the offline heuristic.
/// </summary>
public sealed class ClaudeRadiologyFindingExtractor : IRadiologyFindingExtractor
{
    private readonly AnthropicClient _client;
    private readonly ClinicalNarrativeOptions _options;

    public ClaudeRadiologyFindingExtractor(ClinicalNarrativeOptions options)
    {
        _options = options;
        _client = string.IsNullOrWhiteSpace(options.ApiKey)
            ? new AnthropicClient()
            : new AnthropicClient { ApiKey = options.ApiKey };
    }

    public bool IsLiveModel => true;

    public string ProviderName => "claude";

    public async Task<RadiologyExtractionResult> ExtractAsync(
        string reportText, CancellationToken cancellationToken = default)
    {
        Message response = await _client.Messages.Create(new MessageCreateParams
        {
            Model = _options.Model,
            MaxTokens = _options.MaxTokens,
            System = RadiologyFindingJson.SystemPrompt,
            Thinking = new ThinkingConfigAdaptive(),
            OutputConfig = new OutputConfig
            {
                Format = new JsonOutputFormat { Schema = RadiologyFindingJson.BuildResponseSchema() },
            },
            Messages = [new() { Role = Role.User, Content = RadiologyFindingJson.BuildUserPrompt(reportText) }],
        });

        string text = string.Concat(
            response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));

        return RadiologyFindingJson.Parse(text, ProviderName);
    }
}
