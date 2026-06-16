// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.SiloHost.Infrastructure.Cdc.Materializers;

/// <summary>
/// Materializes VitalGrain state into rpt.FactVital.
/// </summary>
public class VitalMaterializer : ICdcEntityMaterializer
{
    private readonly ILogger<VitalMaterializer> _logger;

    public VitalMaterializer(ILogger<VitalMaterializer> logger) => _logger = logger;

    public string EntityName => "Vital";
    public string GrainTypePattern => "%VitalGrain,%";
    public int Priority => 10;

    public async Task<int> MaterializeAsync(
        IReadOnlyList<ChangedGrainInfo> changedGrains,
        SqlConnection conn,
        IGrainFactory grainFactory,
        CancellationToken ct)
    {
        int count = 0;
        foreach (ChangedGrainInfo grain in changedGrains)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                VitalState state = await grainFactory
                    .GetGrain<IVitalGrain>(grain.GrainKey)
                    .GetVitalAsync();

                long? patientSK = await DimensionKeyResolver.ResolvePatientSKAsync(conn, state.PatientId);
                long? enteredBySK = await DimensionKeyResolver.UpsertProviderAsync(conn, state.EnteredById, state.EnteredByName);
                long? locationSK = await DimensionKeyResolver.UpsertLocationAsync(conn, state.LocationId, state.LocationName);

                await UpsertFactVitalAsync(conn, grain.GrainKey, state, patientSK, enteredBySK, locationSK, ct);
                count++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "CDC Vital: failed to read grain {GrainKey}, skipping",
                    grain.GrainKey);
            }
        }

        return count;
    }

    private static async Task UpsertFactVitalAsync(
        SqlConnection conn, string grainKey, VitalState state,
        long? patientSK, long? enteredBySK, long? locationSK, CancellationToken ct)
    {
        // Parse numeric values — for BP ("120/80"), split into systolic/diastolic
        decimal? resultNumeric = null;
        decimal? resultNumeric2 = null;
        if (state.Value.Contains('/'))
        {
            string[] parts = state.Value.Split('/');
            if (decimal.TryParse(parts[0], out decimal sys)) resultNumeric = sys;
            if (parts.Length > 1 && decimal.TryParse(parts[1], out decimal dia)) resultNumeric2 = dia;
        }
        else
        {
            if (decimal.TryParse(state.Value, out decimal val)) resultNumeric = val;
        }

        string? qualifier = state.Qualifiers.Count > 0 ? string.Join(", ", state.Qualifiers) : null;

        using SqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = @"
            MERGE rpt.FactVital AS tgt
            USING (SELECT @grainKey AS VitalGrainKey) AS src
                ON tgt.VitalGrainKey = src.VitalGrainKey
            WHEN MATCHED THEN
                UPDATE SET
                    ResultValue = @resultVal,
                    ResultNumeric = @resultNum,
                    ResultNumeric2 = @resultNum2,
                    Qualifier = @qualifier,
                    CDCTimestamp = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (VitalGrainKey, PatientSK, EnteredBySK, LocationSK, VitalDateKey,
                        VitalType, ResultValue, ResultNumeric, ResultNumeric2, Unit, Qualifier,
                        VitalDateTime)
                VALUES (@grainKey, @patientSK, @enteredBySK, @locationSK, @vitalDateKey,
                        @vitalType, @resultVal, @resultNum, @resultNum2, @unit, @qualifier,
                        @vitalDt);";

        cmd.Parameters.AddWithValue("@grainKey", grainKey);
        cmd.Parameters.AddWithValue("@patientSK", patientSK.HasValue ? patientSK.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@enteredBySK", enteredBySK.HasValue ? enteredBySK.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@locationSK", locationSK.HasValue ? locationSK.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@vitalDateKey", DimensionKeyResolver.ToDateKey(state.DateTimeTaken) ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@vitalType", state.VitalType);
        cmd.Parameters.AddWithValue("@resultVal", (object?)state.Value ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@resultNum", resultNumeric.HasValue ? resultNumeric.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@resultNum2", resultNumeric2.HasValue ? resultNumeric2.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@unit", (object?)state.Units ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@qualifier", (object?)qualifier ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@vitalDt", state.DateTimeTaken);

        await cmd.ExecuteNonQueryAsync(ct);
    }
}
