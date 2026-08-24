// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Services;

namespace NewVistas.AI;

// Used when live AI is ENABLED but no API key is resolvable (neither ClinicalNarrative:ApiKey
// nor the ANTHROPIC_API_KEY environment variable). These never call the model — so a missing
// key can't crash anything — they serve the offline, fully-grounded output and attach a setup
// notice the UI shows, telling the user how to supply their own key. The instant a key is
// configured, AddClinicalNarrativeAi registers the real live client in their place.

/// <summary>Offline clinical-narrative fallback that carries the "configure your key" notice.</summary>
public sealed class MisconfiguredClinicalNarrativeService : IClinicalNarrativeService
{
    private readonly TemplateClinicalNarrativeService _offline = new();

    public bool IsLiveModel => false;
    public string ProviderName => "offline-template (live AI key not configured)";

    public async Task<NarrativeResult> ComposeAsync(
        ClinicalSummaryContext context, CancellationToken cancellationToken = default)
    {
        NarrativeResult result = await _offline.ComposeAsync(context, cancellationToken);
        result.ProviderName = ProviderName;
        result.ConfigurationNotice = ClinicalNarrativeOptions.ApiKeyHelpText;
        return result;
    }
}

/// <summary>Offline radiology-extraction fallback that carries the "configure your key" notice.</summary>
public sealed class MisconfiguredRadiologyFindingExtractor : IRadiologyFindingExtractor
{
    private readonly HeuristicRadiologyFindingExtractor _offline = new();

    public bool IsLiveModel => false;
    public string ProviderName => "offline-heuristic (live AI key not configured)";

    public async Task<RadiologyExtractionResult> ExtractAsync(
        string reportText, CancellationToken cancellationToken = default)
    {
        RadiologyExtractionResult result = await _offline.ExtractAsync(reportText, cancellationToken);
        result.ProviderName = ProviderName;
        result.ConfigurationNotice = ClinicalNarrativeOptions.ApiKeyHelpText;
        return result;
    }
}

/// <summary>Offline coding-assistant fallback that carries the "configure your key" notice.</summary>
public sealed class MisconfiguredClinicalCodingAssistant : IClinicalCodingAssistant
{
    private readonly LexiconCodingAssistant _offline = new();

    public bool IsLiveModel => false;
    public string ProviderName => "offline-lexicon (live AI key not configured)";

    public async Task<CodingClaimsResult> SuggestClaimsAsync(
        string noteText, CancellationToken cancellationToken = default)
    {
        CodingClaimsResult result = await _offline.SuggestClaimsAsync(noteText, cancellationToken);
        result.ProviderName = ProviderName;
        result.ConfigurationNotice = ClinicalNarrativeOptions.ApiKeyHelpText;
        return result;
    }
}
