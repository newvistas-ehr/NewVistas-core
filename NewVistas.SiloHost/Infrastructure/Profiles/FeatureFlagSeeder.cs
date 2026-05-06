// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using Orleans;

namespace NewVistas.SiloHost.Infrastructure.Profiles;

/// <summary>
/// Hosted service that enables a fixed set of site feature flags on startup.
/// Used by site profiles (e.g., <see cref="IhsTribalSiteProfile"/>) to bake
/// in the features a deployment expects, rather than requiring an operator to
/// flip them via the API after every silo restart.
///
/// Idempotent: <see cref="ISiteParametersGrain.EnableFeatureAsync"/> is safe to
/// call repeatedly. Operators can still toggle features at runtime via the
/// API; the seeder runs once per silo startup and only ensures the configured
/// list is on.
///
/// Pattern: profiles register an instance with their list of features —
/// <c>host.Services.AddHostedService(sp =&gt; new FeatureFlagSeeder(...))</c>.
/// </summary>
public sealed class FeatureFlagSeeder : IHostedService
{
    private readonly IGrainFactory _grainFactory;
    private readonly IReadOnlyList<string> _features;
    private readonly ILogger<FeatureFlagSeeder> _logger;
    private readonly string _siteParametersKey;

    public FeatureFlagSeeder(
        IGrainFactory grainFactory,
        IReadOnlyList<string> features,
        ILogger<FeatureFlagSeeder> logger,
        string siteParametersKey = "SITE:DEFAULT")
    {
        _grainFactory = grainFactory;
        _features = features;
        _logger = logger;
        _siteParametersKey = siteParametersKey;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_features.Count == 0) return;

        ISiteParametersGrain siteParams = _grainFactory.GetGrain<ISiteParametersGrain>(_siteParametersKey);

        foreach (string feature in _features)
        {
            if (cancellationToken.IsCancellationRequested) break;
            try
            {
                await siteParams.EnableFeatureAsync(feature);
                _logger.LogInformation(
                    "Site feature pre-enabled by profile: {Feature}.", feature);
            }
            catch (Exception ex)
            {
                // Don't fail silo startup if a feature can't be enabled — log and
                // continue. Operators can flip the missing flag via the API.
                _logger.LogWarning(ex,
                    "Failed to pre-enable site feature {Feature}; continuing.", feature);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
