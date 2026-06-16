// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using System.Collections.Concurrent;

namespace NewVistas.Abstractions.Importers;

/// <summary>
/// Tracks the mapping from VistA IEN (Internal Entry Number) to Orleans grain key.
/// Uses deterministic keys based on global + IEN for idempotent re-import.
/// Thread-safe for concurrent access by parallel importers.
/// </summary>
public class IenMap
{
    private readonly ConcurrentDictionary<(string Global, long Ien), string> _map = new();

    /// <summary>
    /// Get or create a grain key for the given global/IEN pair.
    /// Uses deterministic format: {prefix}-IEN-{ien} (e.g., ORDER-IEN-42).
    /// Special case: DPT (patient) global uses P{ien} format (e.g., P1, P42).
    /// </summary>
    public string GetOrCreateKey(string global, long ien, string prefix)
    {
        return _map.GetOrAdd((global, ien), _ =>
            global == "DPT" ? $"P{ien}" : $"{prefix}-IEN-{ien}");
    }

    /// <summary>
    /// Try to get an existing grain key for the given global/IEN pair.
    /// Returns null if no mapping exists.
    /// </summary>
    public string? TryGetKey(string global, long ien)
    {
        return _map.TryGetValue((global, ien), out string? key) ? key : null;
    }

    /// <summary>
    /// Explicitly register a grain key for the given global/IEN pair.
    /// Used to pre-populate mappings for entities created outside the import pipeline
    /// (e.g., demo users created by Program.cs).
    /// </summary>
    public void Register(string global, long ien, string grainKey)
    {
        _map[(global, ien)] = grainKey;
    }

    public int Count => _map.Count;
}
