// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.AI;

/// <summary>
/// Configuration for the live clinical-narrative model. Bound from the
/// "ClinicalNarrative" configuration section. Disabled by default — when
/// <see cref="Enabled"/> is false the system uses the offline grounded template.
/// </summary>
public sealed class ClinicalNarrativeOptions
{
    public const string SectionName = "ClinicalNarrative";

    /// <summary>Turn on the live model. Off by default (offline-first).</summary>
    public bool Enabled { get; set; }

    /// <summary>Provider key. Only "claude" is implemented today.</summary>
    public string Provider { get; set; } = "claude";

    /// <summary>API key. When null/empty the Anthropic SDK reads ANTHROPIC_API_KEY from the environment.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Model id. Defaults to the current flagship.</summary>
    public string Model { get; set; } = "claude-opus-4-8";

    /// <summary>Output cap. A grounded summary is small; keep this modest.</summary>
    public int MaxTokens { get; set; } = 2000;

    /// <summary>
    /// The key actually in effect: the configured <see cref="ApiKey"/> if set, otherwise the
    /// ANTHROPIC_API_KEY environment variable (which the SDK also reads). Null/empty when
    /// neither is present — callers degrade gracefully instead of calling the API.
    /// </summary>
    public string? ResolveApiKey() =>
        string.IsNullOrWhiteSpace(ApiKey)
            ? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
            : ApiKey;

    /// <summary>
    /// Shown in the UI when live AI is enabled but no key is configured, so anyone who turns
    /// the feature on is told how to supply their OWN key — and never sees a crash.
    /// </summary>
    public const string ApiKeyHelpText =
        "Live AI is turned on, but no Anthropic API key was found — so this is the offline, " +
        "fully-grounded summary. To use live AI, supply your own Anthropic API key:\n" +
        "1. Get a key — sign in at https://console.anthropic.com/settings/keys and create one (starts with \"sk-ant-\").\n" +
        "2. Provide it — set an environment variable:  setx ANTHROPIC_API_KEY \"sk-ant-...\"\n" +
        "   (or, per project:  dotnet user-secrets set \"ClinicalNarrative:ApiKey\" \"sk-ant-...\"  in NewVistas.SiloHost).\n" +
        "3. Restart the silo. Your key stays on your machine — never commit it to source control.";
}
