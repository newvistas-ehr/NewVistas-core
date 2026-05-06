// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Orleans;

namespace NewVistas.Abstractions.GrainStates;

/// <summary>
/// State for the PatientLabIndex grain.
/// Lightweight metadata index that resolves queries like "last N results of type X"
/// into specific batch grain references. Uses a pull-on-activate pattern.
/// </summary>
[GenerateSerializer]
public class PatientLabIndexState
{
    /// <summary>
    /// Lightweight index entries. Enough metadata to resolve queries
    /// without activating batch grains unnecessarily.
    /// </summary>
    [Id(0)]
    public List<LabIndexEntry> Entries { get; set; } = new();

    /// <summary>
    /// When this index was last rebuilt from batch grains.
    /// </summary>
    [Id(1)]
    public DateTimeOffset? LastRebuilt { get; set; }

    /// <summary>
    /// The set of batch grain keys that were consulted when building this index.
    /// </summary>
    [Id(2)]
    public List<string> KnownBatchKeys { get; set; } = new();

    /// <summary>
    /// Distinct LOINC codes this patient has results for.
    /// Enables "what tests has this patient had?" without scanning batches.
    /// </summary>
    [Id(3)]
    public HashSet<string> DistinctTestTypes { get; set; } = new();
}

/// <summary>
/// Lightweight index entry pointing to a specific result in a batch grain.
/// ~50 bytes per entry.
/// </summary>
[GenerateSerializer]
public class LabIndexEntry
{
    [Id(0)]
    public string LoincCode { get; set; } = string.Empty;

    [Id(1)]
    public string TestName { get; set; } = string.Empty;

    [Id(2)]
    public DateTimeOffset ResultDate { get; set; }

    /// <summary>
    /// Which batch grain holds the full result.
    /// </summary>
    [Id(3)]
    public string BatchGrainKey { get; set; } = string.Empty;

    /// <summary>
    /// Unique result ID to locate the exact result within the batch.
    /// </summary>
    [Id(4)]
    public string ResultId { get; set; } = string.Empty;

    /// <summary>
    /// Allows filtering without activating batch grains.
    /// </summary>
    [Id(5)]
    public LabAbnormalFlag AbnormalFlag { get; set; }
}
