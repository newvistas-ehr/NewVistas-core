// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.Hosting;

namespace NewVistas.SiloHost.Infrastructure.Profiles;

/// <summary>
/// A deployment shape for the silo — encapsulates clustering, grain storage,
/// streaming, log-consistency, and dashboard configuration for one named
/// way of running NewVistas (dev, demo, Azure cloud, remote clinic, ...).
///
/// Profiles are selected once at startup by <see cref="SiteProfileResolver"/>
/// and applied via <see cref="ConfigureSilo"/>. Subsequent federation work
/// (replication sinks, transports, federated readers) hangs off the same
/// abstraction so deployments don't need parallel plumbing.
/// </summary>
public interface ISiteProfile
{
    /// <summary>
    /// Stable name for this profile, used in logs and resolver-trigger matching.
    /// Lowercase-kebab (e.g. "localhost-dev", "sql-express-demo", "azure-cloud").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// True if this profile uses the SQL-backed federation outbox (schema needs
    /// to be applied at startup; sink + optional drainer registered). Default
    /// is false; override on profiles that opt in.
    /// </summary>
    bool UsesFederationOutbox => false;

    /// <summary>
    /// Configures clustering, grain storage for every name in <paramref name="storeNames"/>,
    /// streaming, log-consistency providers, the audit/authorization call filters,
    /// and (where appropriate) the Orleans dashboard.
    /// </summary>
    void ConfigureSilo(ISiloBuilder siloBuilder, HostApplicationBuilder host, IReadOnlyList<string> storeNames);
}
