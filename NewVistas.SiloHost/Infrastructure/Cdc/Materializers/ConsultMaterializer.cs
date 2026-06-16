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
/// Materializes ConsultGrain state into rpt.FactConsult.
/// </summary>
public class ConsultMaterializer : ICdcEntityMaterializer
{
    private readonly ILogger<ConsultMaterializer> _logger;

    public ConsultMaterializer(ILogger<ConsultMaterializer> logger) => _logger = logger;

    public string EntityName => "Consult";
    public string GrainTypePattern => "%ConsultGrain,%";
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
                ConsultState state = await grainFactory
                    .GetGrain<IConsultGrain>(grain.GrainKey)
                    .GetConsultAsync();

                long? patientSK = await DimensionKeyResolver.ResolvePatientSKAsync(conn, state.PatientId);
                long? providerSK = await DimensionKeyResolver.UpsertProviderAsync(
                    conn, state.RequestingProviderId, state.RequestingProviderName);
                long? locationSK = await DimensionKeyResolver.UpsertLocationAsync(conn, state.LocationId, state.LocationName);

                await UpsertFactConsultAsync(conn, grain.GrainKey, state, patientSK, providerSK, locationSK, ct);
                count++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "CDC Consult: failed to read grain {GrainKey}, skipping",
                    grain.GrainKey);
            }
        }

        return count;
    }

    private static async Task UpsertFactConsultAsync(
        SqlConnection conn, string grainKey, ConsultState state,
        long? patientSK, long? providerSK, long? locationSK, CancellationToken ct)
    {
        int? daysToComplete = state.CompletedDateTime.HasValue
            ? (int)(state.CompletedDateTime.Value - state.RequestDateTime).TotalDays
            : null;
        int? daysToSchedule = state.ScheduledDateTime.HasValue
            ? (int)(state.ScheduledDateTime.Value - state.RequestDateTime).TotalDays
            : null;

        using SqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = @"
            MERGE rpt.FactConsult AS tgt
            USING (SELECT @grainKey AS ConsultGrainKey) AS src
                ON tgt.ConsultGrainKey = src.ConsultGrainKey
            WHEN MATCHED THEN
                UPDATE SET
                    [Status] = @status,
                    ScheduledDateTime = @scheduledDt,
                    CompletedDateTime = @completedDt,
                    DaysToComplete = @daysToComplete,
                    DaysToSchedule = @daysToSchedule,
                    CDCTimestamp = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (ConsultGrainKey, PatientSK, RequestingProviderSK, LocationSK, RequestDateKey,
                        ToService, FromService, Urgency, [Status],
                        DaysToComplete, DaysToSchedule,
                        RequestDateTime, ScheduledDateTime, CompletedDateTime)
                VALUES (@grainKey, @patientSK, @providerSK, @locationSK, @reqDateKey,
                        @toService, @fromService, @urgency, @status,
                        @daysToComplete, @daysToSchedule,
                        @reqDt, @scheduledDt, @completedDt);";

        cmd.Parameters.AddWithValue("@grainKey", grainKey);
        cmd.Parameters.AddWithValue("@patientSK", patientSK.HasValue ? patientSK.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@providerSK", providerSK.HasValue ? providerSK.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@locationSK", locationSK.HasValue ? locationSK.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@reqDateKey", DimensionKeyResolver.ToDateKey(state.RequestDateTime) ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@toService", (object?)state.ToService ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@fromService", (object?)state.FromService ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@urgency", (object?)state.Urgency ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@status", (object?)state.Status ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@daysToComplete", daysToComplete.HasValue ? daysToComplete.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@daysToSchedule", daysToSchedule.HasValue ? daysToSchedule.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@reqDt", state.RequestDateTime);
        cmd.Parameters.AddWithValue("@scheduledDt", state.ScheduledDateTime.HasValue ? state.ScheduledDateTime.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@completedDt", state.CompletedDateTime.HasValue ? state.CompletedDateTime.Value : DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
    }
}
