// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.Data.SqlClient;

namespace NewVistas.SiloHost.Infrastructure.Cdc;

/// <summary>
/// Shared helper for resolving dimension surrogate keys used by multiple materializers.
/// </summary>
internal static class DimensionKeyResolver
{
    /// <summary>
    /// Resolves PatientSK from DimPatient by PatientId. Returns null if not found.
    /// </summary>
    internal static async Task<long?> ResolvePatientSKAsync(SqlConnection conn, string patientId)
    {
        using SqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT PatientSK FROM rpt.DimPatient WHERE PatientId = @pid AND IsCurrent = 1";
        cmd.Parameters.AddWithValue("@pid", patientId);
        object? result = await cmd.ExecuteScalarAsync();
        return result is long sk ? sk : null;
    }

    /// <summary>
    /// Upserts a provider into DimProvider and returns ProviderSK.
    /// </summary>
    internal static async Task<long?> UpsertProviderAsync(
        SqlConnection conn, string? providerId, string? providerName)
    {
        if (string.IsNullOrEmpty(providerId)) return null;

        using SqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = @"
            MERGE rpt.DimProvider AS tgt
            USING (SELECT @pid AS ProviderId, @pname AS ProviderName) AS src
                ON tgt.ProviderId = src.ProviderId AND tgt.IsCurrent = 1
            WHEN MATCHED THEN
                UPDATE SET ProviderName = src.ProviderName, LastCDCTimestamp = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (ProviderId, ProviderName, LastCDCTimestamp)
                VALUES (src.ProviderId, src.ProviderName, SYSUTCDATETIME())
            OUTPUT inserted.ProviderSK;";
        cmd.Parameters.AddWithValue("@pid", providerId);
        cmd.Parameters.AddWithValue("@pname", (object?)providerName ?? DBNull.Value);
        object? result = await cmd.ExecuteScalarAsync();
        return result is long sk ? sk : null;
    }

    /// <summary>
    /// Upserts a location into DimLocation and returns LocationSK.
    /// </summary>
    internal static async Task<long?> UpsertLocationAsync(
        SqlConnection conn, string? locationId, string? locationName)
    {
        if (string.IsNullOrEmpty(locationId)) return null;

        using SqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = @"
            MERGE rpt.DimLocation AS tgt
            USING (SELECT @lid AS LocationId, @lname AS LocationName) AS src
                ON tgt.LocationId = src.LocationId AND tgt.IsCurrent = 1
            WHEN MATCHED THEN
                UPDATE SET LocationName = src.LocationName, LastCDCTimestamp = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (LocationId, LocationName, LastCDCTimestamp)
                VALUES (src.LocationId, src.LocationName, SYSUTCDATETIME())
            OUTPUT inserted.LocationSK;";
        cmd.Parameters.AddWithValue("@lid", locationId);
        cmd.Parameters.AddWithValue("@lname", (object?)locationName ?? DBNull.Value);
        object? result = await cmd.ExecuteScalarAsync();
        return result is long sk ? sk : null;
    }

    /// <summary>
    /// Upserts a drug into DimDrug and returns DrugSK.
    /// </summary>
    internal static async Task<long?> UpsertDrugAsync(
        SqlConnection conn, string? drugId, string? drugName)
    {
        if (string.IsNullOrEmpty(drugId)) return null;

        using SqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = @"
            MERGE rpt.DimDrug AS tgt
            USING (SELECT @did AS DrugId, @dname AS DrugName) AS src
                ON tgt.DrugId = src.DrugId AND tgt.IsCurrent = 1
            WHEN MATCHED THEN
                UPDATE SET DrugName = src.DrugName
            WHEN NOT MATCHED THEN
                INSERT (DrugId, DrugName)
                VALUES (src.DrugId, src.DrugName)
            OUTPUT inserted.DrugSK;";
        cmd.Parameters.AddWithValue("@did", drugId);
        cmd.Parameters.AddWithValue("@dname", (object?)drugName ?? DBNull.Value);
        object? result = await cmd.ExecuteScalarAsync();
        return result is long sk ? sk : null;
    }

    /// <summary>
    /// Upserts a lab test into DimLabTest and returns LabTestSK.
    /// </summary>
    internal static async Task<long?> UpsertLabTestAsync(
        SqlConnection conn, string testId, string? testName, string? loincCode, string? category)
    {
        if (string.IsNullOrEmpty(testId)) return null;

        using SqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = @"
            MERGE rpt.DimLabTest AS tgt
            USING (SELECT @tid AS TestId, @tname AS TestName, @loinc AS LoincCode, @cat AS Category) AS src
                ON tgt.TestId = src.TestId AND tgt.IsCurrent = 1
            WHEN MATCHED THEN
                UPDATE SET TestName = src.TestName, LoincCode = src.LoincCode, Category = src.Category
            WHEN NOT MATCHED THEN
                INSERT (TestId, TestName, LoincCode, Category)
                VALUES (src.TestId, src.TestName, src.LoincCode, src.Category)
            OUTPUT inserted.LabTestSK;";
        cmd.Parameters.AddWithValue("@tid", testId);
        cmd.Parameters.AddWithValue("@tname", (object?)testName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@loinc", (object?)loincCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@cat", (object?)category ?? DBNull.Value);
        object? result = await cmd.ExecuteScalarAsync();
        return result is long sk ? sk : null;
    }

    /// <summary>
    /// Converts a DateTime to the DimDate DateKey format (YYYYMMDD integer).
    /// </summary>
    internal static int? ToDateKey(DateTime? dt)
        => dt.HasValue ? int.Parse(dt.Value.ToString("yyyyMMdd")) : null;
}
