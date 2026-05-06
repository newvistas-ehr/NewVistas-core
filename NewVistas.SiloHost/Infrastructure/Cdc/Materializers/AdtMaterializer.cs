// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.SiloHost.Infrastructure.Cdc.Materializers;

/// <summary>
/// Materializes AdtGrain state into rpt.FactAdtMovement.
/// </summary>
public class AdtMaterializer : ICdcEntityMaterializer
{
    private readonly ILogger<AdtMaterializer> _logger;

    public AdtMaterializer(ILogger<AdtMaterializer> logger) => _logger = logger;

    public string EntityName => "AdtMovement";
    public string GrainTypePattern => "%AdtGrain,%";
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
                AdtState state = await grainFactory
                    .GetGrain<IAdtGrain>(grain.GrainKey)
                    .GetMovementAsync();

                long? patientSK = await DimensionKeyResolver.ResolvePatientSKAsync(conn, state.PatientId);
                long? providerSK = await DimensionKeyResolver.UpsertProviderAsync(
                    conn, state.AttendingPhysicianId, state.AttendingPhysicianName);
                long? locationSK = await DimensionKeyResolver.UpsertLocationAsync(
                    conn, state.WardLocationId, state.WardLocationName);

                await UpsertFactAdtMovementAsync(conn, grain.GrainKey, state, patientSK, providerSK, locationSK, ct);
                count++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "CDC AdtMovement: failed to read grain {GrainKey}, skipping",
                    grain.GrainKey);
            }
        }

        return count;
    }

    private static async Task UpsertFactAdtMovementAsync(
        SqlConnection conn, string grainKey, AdtState state,
        long? patientSK, long? providerSK, long? locationSK, CancellationToken ct)
    {
        using SqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = @"
            MERGE rpt.FactAdtMovement AS tgt
            USING (SELECT @grainKey AS MovementGrainKey) AS src
                ON tgt.MovementGrainKey = src.MovementGrainKey
            WHEN MATCHED THEN
                UPDATE SET
                    DischargeDateTime = @dischargeDt,
                    LengthOfStayDays = @los,
                    Disposition = @disposition,
                    CDCTimestamp = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (MovementGrainKey, PatientSK, AttendingProviderSK, LocationSK, MovementDateKey,
                        MovementType, WardName, RoomBed, TreatingSpecialty, Disposition,
                        LengthOfStayDays, MovementDateTime, DischargeDateTime)
                VALUES (@grainKey, @patientSK, @providerSK, @locationSK, @moveDateKey,
                        @moveType, @wardName, @roomBed, @specialty, @disposition,
                        @los, @moveDt, @dischargeDt);";

        cmd.Parameters.AddWithValue("@grainKey", grainKey);
        cmd.Parameters.AddWithValue("@patientSK", patientSK.HasValue ? patientSK.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@providerSK", providerSK.HasValue ? providerSK.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@locationSK", locationSK.HasValue ? locationSK.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@moveDateKey", DimensionKeyResolver.ToDateKey(state.MovementDateTime) ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@moveType", (object?)state.TransactionType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@wardName", (object?)state.WardLocationName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@roomBed", (object?)state.RoomBed ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@specialty", (object?)state.TreatingSpecialtyName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@disposition", (object?)state.Disposition ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@los", state.LengthOfStay.HasValue ? state.LengthOfStay.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@moveDt", state.MovementDateTime);
        cmd.Parameters.AddWithValue("@dischargeDt", state.DischargeDateTime.HasValue ? state.DischargeDateTime.Value : DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
    }
}
