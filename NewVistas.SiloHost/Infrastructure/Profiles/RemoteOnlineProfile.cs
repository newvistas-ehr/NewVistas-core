// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.Federation;
using NewVistas.SiloHost.Infrastructure.Federation;
using Orleans.Hosting;

namespace NewVistas.SiloHost.Infrastructure.Profiles;

/// <summary>
/// Small hospital with intermittent internet — runs a single-silo cluster
/// against a local SQL Express (or PostgreSQL) instance, with the SQL
/// federation outbox enabled and a drainer running. Outbound transport is
/// HTTP-with-mTLS when configured, falling back to the logging transport
/// for smoke tests and pre-mTLS deployments.
/// </summary>
public sealed class RemoteOnlineProfile : ISiteProfile
{
    public string Name => "remote-online";
    public bool UsesFederationOutbox => true;

    public const string ConnectionStringName = "SqlExpress";

    public void ConfigureSilo(ISiloBuilder siloBuilder, HostApplicationBuilder host, IReadOnlyList<string> storeNames)
    {
        siloBuilder.UseLocalhostClustering();

        string connStr = host.Configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"{ConnectionStringName} connection string not found. Ensure '{ConnectionStringName}' is defined in ConnectionStrings.");

        foreach (string storeName in storeNames)
        {
            siloBuilder.AddAdoNetGrainStorage(storeName, options =>
            {
                options.Invariant = "Microsoft.Data.SqlClient";
                options.ConnectionString = connStr;
            });
        }

        // Cluster identity stamped onto every fresh clinical event envelope.
        // Real clinics set Federation:LocalClusterId per site (e.g. "KIBALE-UGANDA")
        // and Federation:IcnPrefix to the 3-digit allocation from ClusterPrefixAllocations.md.
        // Fallbacks exist only so a smoke-test deployment without config still boots.
        string clusterId = host.Configuration["Federation:LocalClusterId"] ?? "REMOTE-ONLINE";
        string icnPrefix = host.Configuration["Federation:IcnPrefix"] ?? "001";
        siloBuilder.Services.AddSingleton<IClusterIdentity>(
            new StaticClusterIdentity(clusterId, icnPrefix));

        // Federation outbound — outbox writes envelopes durably; drainer ships
        // them via the chosen transport.
        siloBuilder
            .AddFederationOutbox(host, connStr)
            .AddFederationHttpOrLoggingTransport(host)
            .AddFederationDrainer()
            .AddFederationRenewal(host);

        siloBuilder
            .AddInMemoryStreaming()
            .AddCommonSiloServices();
    }
}
