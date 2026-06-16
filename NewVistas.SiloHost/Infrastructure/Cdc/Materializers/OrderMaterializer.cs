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
/// Materializes OrderGrain state into rpt.FactOrder.
/// Also upserts DimProvider and DimLocation as side effects.
/// </summary>
public class OrderMaterializer : ICdcEntityMaterializer
{
    private readonly ILogger<OrderMaterializer> _logger;

    public OrderMaterializer(ILogger<OrderMaterializer> logger) => _logger = logger;

    public string EntityName => "Order";
    public string GrainTypePattern => "%OrderGrain,%";
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
                OrderState state = await grainFactory
                    .GetGrain<IOrderGrain>(grain.GrainKey)
                    .GetOrderAsync();

                long? patientSK = await DimensionKeyResolver.ResolvePatientSKAsync(conn, state.PatientId);
                long? providerSK = await DimensionKeyResolver.UpsertProviderAsync(conn, state.ProviderId, state.ProviderName);
                long? locationSK = await DimensionKeyResolver.UpsertLocationAsync(conn, state.LocationId, state.LocationName);

                await UpsertFactOrderAsync(conn, grain.GrainKey, state, patientSK, providerSK, locationSK, ct);
                count++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "CDC Order: failed to read grain {GrainKey}, skipping",
                    grain.GrainKey);
            }
        }

        return count;
    }

    private static async Task UpsertFactOrderAsync(
        SqlConnection conn, string grainKey, OrderState state,
        long? patientSK, long? providerSK, long? locationSK, CancellationToken ct)
    {
        int? daysToSign = state.SignatureDateTime.HasValue
            ? (int)(state.SignatureDateTime.Value - state.OrderDateTime).TotalDays
            : null;
        int? daysActive = state.StopDateTime.HasValue
            ? (int)(state.StopDateTime.Value - state.StartDateTime).TotalDays
            : null;

        using SqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = @"
            MERGE rpt.FactOrder AS tgt
            USING (SELECT @grainKey AS OrderGrainKey) AS src
                ON tgt.OrderGrainKey = src.OrderGrainKey
            WHEN MATCHED THEN
                UPDATE SET
                    [Status] = @status,
                    SignedDateTime = @signedDt,
                    StopDateTime = @stopDt,
                    DaysToSign = @daysToSign,
                    DaysActive = @daysActive,
                    CDCTimestamp = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (OrderGrainKey, PatientSK, OrderingProviderSK, LocationSK, OrderDateKey,
                        OrderType, OrderText, Urgency, [Status],
                        OrderDateTime, StartDateTime, StopDateTime, SignedDateTime,
                        DaysToSign, DaysActive)
                VALUES (@grainKey, @patientSK, @providerSK, @locationSK, @orderDateKey,
                        @orderType, @orderText, @urgency, @status,
                        @orderDt, @startDt, @stopDt, @signedDt,
                        @daysToSign, @daysActive);";

        cmd.Parameters.AddWithValue("@grainKey", grainKey);
        cmd.Parameters.AddWithValue("@patientSK", patientSK.HasValue ? patientSK.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@providerSK", providerSK.HasValue ? providerSK.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@locationSK", locationSK.HasValue ? locationSK.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@orderDateKey", DimensionKeyResolver.ToDateKey(state.OrderDateTime) ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@orderType", (object?)state.OrderType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@orderText", (object?)state.OrderableItem ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@urgency", (object?)state.Urgency ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@status", (object?)state.Status ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@orderDt", state.OrderDateTime);
        cmd.Parameters.AddWithValue("@startDt", state.StartDateTime);
        cmd.Parameters.AddWithValue("@stopDt", state.StopDateTime.HasValue ? state.StopDateTime.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@signedDt", state.SignatureDateTime.HasValue ? state.SignatureDateTime.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@daysToSign", daysToSign.HasValue ? daysToSign.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@daysActive", daysActive.HasValue ? daysActive.Value : DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
    }
}
