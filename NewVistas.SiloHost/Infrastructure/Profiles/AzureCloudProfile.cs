// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Federation;
using NewVistas.SiloHost.Infrastructure.Federation;
using Orleans.Hosting;

namespace NewVistas.SiloHost.Infrastructure.Profiles;

/// <summary>
/// Azure cloud profile — AdoNet clustering and grain storage against Azure SQL,
/// configurable silo/gateway endpoints, in-memory streaming, with full
/// outbound federation (SQL outbox + HTTP transport + auto-renewal) when
/// configured.
///
/// A future plan adds a Cosmos change-feed sink as an alternative to the
/// SQL outbox for cloud-to-cloud federation; the wiring here pre-positions
/// for that swap.
/// </summary>
public sealed class AzureCloudProfile : ISiteProfile
{
    public string Name => "azure-cloud";
    public bool UsesFederationOutbox => true;

    public const string ConnectionStringName = "OrleansDatabase";

    private const int DefaultSiloPort = 11111;
    private const int DefaultGatewayPort = 30000;

    public void ConfigureSilo(ISiloBuilder siloBuilder, HostApplicationBuilder host, IReadOnlyList<string> storeNames)
    {
        string connectionString = host.Configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"{ConnectionStringName} connection string not found.");

        siloBuilder.UseAdoNetClustering(options =>
        {
            options.Invariant = "Microsoft.Data.SqlClient";
            options.ConnectionString = connectionString;
        });

        foreach (string storeName in storeNames)
        {
            siloBuilder.AddAdoNetGrainStorage(storeName, options =>
            {
                options.Invariant = "Microsoft.Data.SqlClient";
                options.ConnectionString = connectionString;
            });
        }

        int siloPort = host.Configuration.GetValue<int?>("Orleans:SiloPort") ?? DefaultSiloPort;
        int gatewayPort = host.Configuration.GetValue<int?>("Orleans:GatewayPort") ?? DefaultGatewayPort;
        siloBuilder.ConfigureEndpoints(siloPort, gatewayPort);

        // Cluster identity stamped onto every fresh clinical event envelope.
        // Real deployments set Federation:LocalClusterId per site (e.g. "VAMC-BOSTON")
        // and Federation:IcnPrefix to the 3-digit allocation from ClusterPrefixAllocations.md.
        // Fallbacks exist only so a smoke-test deployment without config still boots.
        string clusterId = host.Configuration["Federation:LocalClusterId"] ?? "AZURE-CLOUD";
        string icnPrefix = host.Configuration["Federation:IcnPrefix"] ?? "001";
        siloBuilder.Services.AddSingleton<IClusterIdentity>(
            new StaticClusterIdentity(clusterId, icnPrefix));

        // VA-aligned cloud deployment: apply 38 CFR §17.36 priority-group rules at registration.
        // Registered BEFORE AddCommonSiloServices so the TryAdd default (no-op) is skipped.
        siloBuilder.Services.AddSingleton<NewVistas.Abstractions.Eligibility.IRegistrationEligibilityPolicy,
            NewVistas.Abstractions.Eligibility.VaRegistrationEligibilityPolicy>();

        // Federation outbound — same shape as RemoteOnline (the cloud profile
        // can be either a hub receiving from spokes, a spoke pushing to a peer
        // hub, or both, all by configuration).
        siloBuilder
            .AddFederationOutbox(host, connectionString)
            .AddFederationHttpOrLoggingTransport(host)
            .AddFederationDrainer()
            .AddFederationRenewal(host);

        siloBuilder
            .AddInMemoryStreaming()
            .AddCommonSiloServices();
    }
}
