// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.SiloHost.Infrastructure.Federation;

/// <summary>
/// Tunables for the SQL-backed federation outbox: sink storage location plus
/// drainer batching and retry. Bound from the <c>Federation:Outbox</c>
/// configuration section.
/// </summary>
public class OutboxOptions
{
    public const string SectionName = "Federation:Outbox";

    /// <summary>
    /// Connection string name in <c>ConnectionStrings</c>. If null, the profile
    /// resolves a sensible default (typically the same database the silo uses
    /// for grain storage).
    /// </summary>
    public string? ConnectionStringName { get; set; }

    /// <summary>How often the drainer wakes to look for unsent rows. Default: 30s.</summary>
    public int PollingIntervalSeconds { get; set; } = 30;

    /// <summary>Max envelopes the drainer ships per cycle. Default: 100.</summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>First retry delay after a transport failure. Doubled on each subsequent attempt. Default: 30s.</summary>
    public int InitialRetrySeconds { get; set; } = 30;

    /// <summary>Cap on the exponential backoff. Default: 1 hour.</summary>
    public int MaxRetrySeconds { get; set; } = 3600;

    /// <summary>Delay before the drainer's first poll, so the silo finishes warming up. Default: 30s.</summary>
    public int StartupDelaySeconds { get; set; } = 30;
}
