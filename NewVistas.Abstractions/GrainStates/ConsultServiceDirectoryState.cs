// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

[GenerateSerializer]
public class ConsultServiceDirectoryState
{
    [Id(0)]
    public List<ConsultServiceEntry> Services { get; set; } = new();

    [Id(1)]
    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;
}

[GenerateSerializer]
public class ConsultServiceEntry
{
    [Id(0)]
    public string ServiceId { get; set; } = string.Empty;

    [Id(1)]
    public string ServiceName { get; set; } = string.Empty;

    [Id(2)]
    public string? Specialty { get; set; }

    [Id(3)]
    public string? DefaultProviderId { get; set; }

    [Id(4)]
    public string? DefaultProviderName { get; set; }

    [Id(5)]
    public string? LocationId { get; set; }

    [Id(6)]
    public string? LocationName { get; set; }

    [Id(7)]
    public bool IsActive { get; set; } = true;

    [Id(8)]
    public bool AcceptsInterfacility { get; set; }
}
