// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Extensions.DependencyInjection;
using NewVistas.Abstractions.Services;

namespace NewVistas.AI;

/// <summary>
/// Registers the live clinical-narrative model behind the seam. When disabled (the
/// default) this is a no-op and the offline <see cref="TemplateClinicalNarrativeService"/>
/// registered by the host remains the active <see cref="IClinicalNarrativeService"/>.
/// </summary>
public static class ClinicalNarrativeServiceCollectionExtensions
{
    /// <summary>
    /// Wires the live narrative provider when <paramref name="options"/> is enabled.
    /// The live client is wrapped in <see cref="ResilientClinicalNarrativeService"/> so a
    /// model failure degrades to the grounded template rather than breaking the summary.
    /// Call this BEFORE the host registers the template default (a plain AddSingleton here
    /// wins over the host's TryAddSingleton fallback).
    /// </summary>
    public static IServiceCollection AddClinicalNarrativeAi(
        this IServiceCollection services, ClinicalNarrativeOptions options)
    {
        if (!options.Enabled)
            return services;

        if (!string.Equals(options.Provider, "claude", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Clinical narrative provider '{options.Provider}' is not implemented.");

        services.AddSingleton(options);

        // Enabled but no key (neither ClinicalNarrative:ApiKey nor the ANTHROPIC_API_KEY env
        // var): do NOT construct a live client — that path needs a key and would fail. Register
        // graceful wrappers that serve the offline grounded output and carry a setup notice the
        // UI shows. This is how someone who turns AI on without a key is taught to supply their
        // own, instead of getting a crash.
        if (string.IsNullOrWhiteSpace(options.ResolveApiKey()))
        {
            services.AddSingleton<IClinicalNarrativeService>(_ => new MisconfiguredClinicalNarrativeService());
            services.AddSingleton<IRadiologyFindingExtractor>(_ => new MisconfiguredRadiologyFindingExtractor());
            return services;
        }

        services.AddSingleton<IClinicalNarrativeService>(_ =>
            new ResilientClinicalNarrativeService(
                new ClaudeClinicalNarrativeService(options),
                new TemplateClinicalNarrativeService()));
        services.AddSingleton<IRadiologyFindingExtractor>(_ =>
            new ResilientRadiologyFindingExtractor(
                new ClaudeRadiologyFindingExtractor(options),
                new HeuristicRadiologyFindingExtractor()));

        return services;
    }
}
