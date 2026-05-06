// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans.TestingHost;

namespace NewVistas.PT.Tests;

/// <summary>
/// Singleton TestCluster for PT grain tests. Registers only the stores
/// needed by PT grains, keeping the test cluster lightweight.
/// Thread-safe lazy initialization — built once on first access.
/// </summary>
public static class SharedCluster
{
    private static TestCluster? _cluster;
    private static readonly object _lock = new();

    public static TestCluster Instance
    {
        get
        {
            if (_cluster is not null) return _cluster;
            lock (_lock)
            {
                if (_cluster is not null) return _cluster;
                var builder = new TestClusterBuilder(1);
                builder.AddSiloBuilderConfigurator<PTStoresConfigurator>();
                _cluster = builder.Build();
                _cluster.Deploy();
                return _cluster;
            }
        }
    }

    private class PTStoresConfigurator : ISiloConfigurator
    {
        public void Configure(ISiloBuilder siloBuilder)
        {
            siloBuilder.AddMemoryGrainStorageAsDefault();
            siloBuilder.AddMemoryGrainStorage("physTherapySessionStore");
            siloBuilder.AddMemoryGrainStorage("physTherapySessionIndexStore");
            siloBuilder.AddMemoryGrainStorage("physTherapyGoalStore");
            siloBuilder.AddMemoryGrainStorage("physTherapyHepStore");
        }
    }
}
