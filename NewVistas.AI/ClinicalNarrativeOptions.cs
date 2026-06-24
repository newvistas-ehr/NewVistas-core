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
}
