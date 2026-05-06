// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.WebServer.Infrastructure.Federation;

/// <summary>
/// Wires up hub-CA services when <see cref="HubCaOptions.Enabled"/> is true.
/// </summary>
public static class HubCaExtensions
{
    /// <summary>
    /// Reads <c>Federation:HubCa</c>; if enabled, binds <see cref="HubCaOptions"/>
    /// and registers <see cref="IHubCertificateAuthority"/> as a singleton.
    /// Returns whether the hub-CA was enabled (so the caller can decide
    /// whether to surface the controller endpoints).
    /// </summary>
    public static bool TryAddHubCa(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(HubCaOptions.SectionName).Get<HubCaOptions>();
        if (options is null || !options.Enabled)
        {
            // Non-hub deployments still need an IRevocationCache so the auth
            // handler resolves cleanly. The no-op always reports "not revoked".
            services.AddSingleton<IRevocationCache, NoOpRevocationCache>();
            return false;
        }

        services.Configure<HubCaOptions>(configuration.GetSection(HubCaOptions.SectionName));
        services.AddSingleton<IHubCertificateAuthority>(sp =>
            new HubCertificateAuthority(
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<HubCaOptions>>().Value));

        // Revocation list lives on the hub: in-memory cache refreshed from
        // the registry grain by a background service.
        services.Configure<RevocationOptions>(configuration.GetSection(RevocationOptions.SectionName));
        services.AddSingleton<IRevocationCache, InMemoryRevocationCache>();
        services.AddHostedService<RevocationRefreshService>();

        return true;
    }
}
