// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.Extensions.Options;

namespace NewVistas.WebServer.Infrastructure.Federation;

/// <summary>
/// Background service that periodically pulls the revocation list from the
/// registry grain into the local <see cref="IRevocationCache"/>. Runs only
/// on hub deployments (registered alongside <see cref="HubCaOptions.Enabled"/>);
/// non-hub WebServers use the <see cref="NoOpRevocationCache"/> and don't
/// need this service.
/// </summary>
public sealed class RevocationRefreshService : BackgroundService
{
    private readonly IRevocationCache _cache;
    private readonly RevocationOptions _options;
    private readonly ILogger<RevocationRefreshService> _logger;

    public RevocationRefreshService(
        IRevocationCache cache,
        IOptions<RevocationOptions> options,
        ILogger<RevocationRefreshService> logger)
    {
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan interval = TimeSpan.FromMinutes(_options.RefreshIntervalMinutes);
        _logger.LogInformation("Revocation refresh service started — interval {Interval}", interval);

        // Initial refresh so the auth handler isn't running cold for the first cycle.
        await TryRefreshAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await TryRefreshAsync(stoppingToken);
        }
    }

    private async Task TryRefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _cache.RefreshAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Revocation cache refresh failed; previous snapshot still in effect.");
        }
    }
}
