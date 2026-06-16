// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Federation;
using Orleans.Hosting;

namespace NewVistas.SiloHost.Infrastructure.Profiles;

/// <summary>
/// SQL Express demo profile — localhost clustering plus AdoNet grain storage
/// against a local SQL Express instance. Selected via the legacy
/// <c>--use-sqlexpress</c> CLI flag.
///
/// Keeps demo data persistent across silo restarts so customer demos can pick
/// up where they left off, without standing up Azure infrastructure.
/// </summary>
public sealed class SqlExpressDemoProfile : ISiteProfile
{
    public string Name => "sql-express-demo";

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
        // ICN prefix "001" allocated to demo/test deployments per ClusterPrefixAllocations.md.
        siloBuilder.Services.AddSingleton<IClusterIdentity>(
            new StaticClusterIdentity("DEMO-SQLEXPRESS", "001"));

        // VA-aligned demo: apply 38 CFR §17.36 priority-group rules at registration.
        // Registered BEFORE AddCommonSiloServices so the TryAdd default (no-op) is skipped.
        siloBuilder.Services.AddSingleton<NewVistas.Abstractions.Eligibility.IRegistrationEligibilityPolicy,
            NewVistas.Abstractions.Eligibility.VaRegistrationEligibilityPolicy>();

        siloBuilder
            .AddInMemoryStreaming()
            .AddCommonSiloServices();
    }
}
