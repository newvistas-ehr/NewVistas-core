// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.SiloHost.Infrastructure.Cdc;

public class CdcOptions
{
    public const string SectionName = "Cdc";

    /// <summary>Polling interval in seconds. Default: 120 (2 minutes).</summary>
    public int PollingIntervalSeconds { get; set; } = 120;

    /// <summary>Maximum grains to process per entity type per cycle. Default: 5000.</summary>
    public int BatchSize { get; set; } = 5000;

    /// <summary>
    /// Connection string name for the reporting database.
    /// Falls back to "SqlExpress" or "OrleansDatabase" if not set.
    /// </summary>
    public string? ReportingConnectionStringName { get; set; }

    /// <summary>
    /// Whether the CDC data warehouse service is enabled. Default: false (opt-in).
    /// Small clinics that do not need the rpt.* star schema can leave this off to avoid
    /// the materializer workload and reporting-schema footprint.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Startup delay in seconds before first poll, allowing the silo to stabilize. Default: 30.</summary>
    public int StartupDelaySeconds { get; set; } = 30;
}
