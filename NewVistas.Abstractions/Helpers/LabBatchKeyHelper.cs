// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
namespace NewVistas.Abstractions.Helpers;

/// <summary>
/// Computes time-partitioned batch grain keys for lab results.
/// Partition scheme: 6-month windows (H1=Jan-Jun, H2=Jul-Dec).
/// This allows callers to go directly to the right batch grain without consulting an index.
/// </summary>
public static class LabBatchKeyHelper
{
    public static string GetBatchKey(string patientIcn, DateTimeOffset resultDate)
    {
        var half = resultDate.Month <= 6 ? "H1" : "H2";
        return $"PatientLabBatch/{patientIcn}/{resultDate.Year}-{half}";
    }

    public static IEnumerable<string> GetBatchKeysForRange(
        string patientIcn, DateTimeOffset from, DateTimeOffset to)
    {
        var current = new DateTimeOffset(from.Year, from.Month <= 6 ? 1 : 7, 1,
            0, 0, 0, TimeSpan.Zero);
        while (current <= to)
        {
            yield return GetBatchKey(patientIcn, current);
            current = current.AddMonths(6);
        }
    }

    /// <summary>
    /// Extracts the patient ICN from a batch grain key.
    /// </summary>
    public static string GetPatientIcn(string batchKey)
    {
        // Format: PatientLabBatch/{patientIcn}/{year}-{half}
        var parts = batchKey.Split('/');
        return parts.Length >= 2 ? parts[1] : string.Empty;
    }
}
