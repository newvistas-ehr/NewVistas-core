// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using NewVistas.Abstractions.Federation;
using NewVistas.SiloHost.Infrastructure.Federation;
using Orleans.Hosting;

namespace NewVistas.SiloHost.Infrastructure.Profiles;

/// <summary>
/// Default development profile — localhost clustering, in-memory grain storage
/// for every store, in-memory streams, and the Orleans dashboard on :8080.
///
/// State is lost on silo restart; this is the right shape for `dotnet run`
/// against a hot-reload Blazor frontend and the unit-test fast path.
/// </summary>
public sealed class LocalhostDevProfile : ISiteProfile
{
    public string Name => "localhost-dev";

    public void ConfigureSilo(ISiloBuilder siloBuilder, HostApplicationBuilder host, IReadOnlyList<string> storeNames)
    {
        siloBuilder.UseLocalhostClustering();

        foreach (string storeName in storeNames)
        {
            siloBuilder.AddMemoryGrainStorage(storeName);
        }

        // Register the logging sink BEFORE AddCommonSiloServices, which uses
        // TryAddSingleton for the default no-op sink. Order matters.
        siloBuilder.Services.AddSingleton<IClinicalEventReplicationSink, LoggingClinicalEventReplicationSink>();

        // Cluster identity stamped onto every fresh clinical event envelope.
        // ICN prefix "000" is reserved for DEV-LOCAL per ClusterPrefixAllocations.md.
        siloBuilder.Services.AddSingleton<IClusterIdentity>(
            new StaticClusterIdentity("DEV-LOCAL", "000"));

        siloBuilder
            .AddInMemoryStreaming()
            .AddCommonSiloServices();
    }
}
