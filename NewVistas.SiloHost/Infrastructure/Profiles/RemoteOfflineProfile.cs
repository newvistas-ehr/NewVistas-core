// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using NewVistas.Abstractions.Federation;
using NewVistas.SiloHost.Infrastructure.Federation;
using Orleans.Hosting;

namespace NewVistas.SiloHost.Infrastructure.Profiles;

/// <summary>
/// Fully isolated clinic — runs a single-silo cluster against a local SQL
/// Express (or PostgreSQL) instance with no expectation of upstream
/// connectivity.
///
/// Federation works via sneakernet: outbound events drain into the configured
/// <see cref="FileBundleOptions.OutboundDirectory"/> as JSON bundles; an
/// operator copies them to a USB drive or satellite-uplink batch for
/// delivery. Bundles delivered <i>to</i> this clinic land in
/// <see cref="FileBundleOptions.InboundDirectory"/>, where the
/// <see cref="FileBundleInboundService"/> picks them up and applies them
/// through the same <see cref="IFederationInboundApplier"/> the HTTP receiver
/// uses.
///
/// When the file-bundle config is absent, the profile falls back to the
/// logging transport so the silo still boots cleanly for smoke tests.
/// </summary>
public sealed class RemoteOfflineProfile : ISiteProfile
{
    public string Name => "remote-offline";
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
        string clusterId = host.Configuration["Federation:LocalClusterId"] ?? "REMOTE-OFFLINE";
        string icnPrefix = host.Configuration["Federation:IcnPrefix"] ?? "001";
        siloBuilder.Services.AddSingleton<IClusterIdentity>(
            new StaticClusterIdentity(clusterId, icnPrefix));

        // Outbox + file-bundle transport + drainer: events drain durably into
        // the bundle directory. Inbound service watches the inbound directory
        // for bundles a peer has dropped off.
        siloBuilder
            .AddFederationOutbox(host, connStr)
            .AddFederationFileBundleTransport(host)
            .AddFederationDrainer()
            .AddFileBundleInbound(host);

        siloBuilder
            .AddInMemoryStreaming()
            .AddCommonSiloServices();
    }
}
