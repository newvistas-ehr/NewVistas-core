// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.SiloHost.Infrastructure.Cdc.Materializers;

/// <summary>
/// Materializes BcmaGrain state into rpt.FactMedAdmin.
/// </summary>
public class BcmaMaterializer : ICdcEntityMaterializer
{
    private readonly ILogger<BcmaMaterializer> _logger;

    public BcmaMaterializer(ILogger<BcmaMaterializer> logger) => _logger = logger;

    public string EntityName => "Bcma";
    public string GrainTypePattern => "%BcmaGrain,%";
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
                BcmaState state = await grainFactory
                    .GetGrain<IBcmaGrain>(grain.GrainKey)
                    .GetAdministrationAsync();

                long? patientSK = await DimensionKeyResolver.ResolvePatientSKAsync(conn, state.PatientId);
                long? adminProviderSK = await DimensionKeyResolver.UpsertProviderAsync(
                    conn, state.AdministeredById, state.AdministeredByName);
                long? drugSK = await DimensionKeyResolver.UpsertDrugAsync(conn, state.DrugId, state.DrugName);

                await UpsertFactMedAdminAsync(conn, grain.GrainKey, state, patientSK, adminProviderSK, drugSK, ct);
                count++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "CDC Bcma: failed to read grain {GrainKey}, skipping",
                    grain.GrainKey);
            }
        }

        return count;
    }

    private static async Task UpsertFactMedAdminAsync(
        SqlConnection conn, string grainKey, BcmaState state,
        long? patientSK, long? adminProviderSK, long? drugSK, CancellationToken ct)
    {
        // Variance: minutes between scheduled and actual administration
        int? varianceMinutes = state.ScheduledDateTime.HasValue && state.AdministrationDateTime.HasValue
            ? (int)(state.AdministrationDateTime.Value - state.ScheduledDateTime.Value).TotalMinutes
            : null;

        using SqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = @"
            MERGE rpt.FactMedAdmin AS tgt
            USING (SELECT @grainKey AS BcmaGrainKey) AS src
                ON tgt.BcmaGrainKey = src.BcmaGrainKey
            WHEN MATCHED THEN
                UPDATE SET
                    ActionStatus = @actionStatus,
                    AdminDateTime = @adminDt,
                    VarianceMinutes = @variance,
                    CDCTimestamp = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (BcmaGrainKey, PatientSK, AdminProviderSK, DrugSK, AdminDateKey,
                        ActionStatus, Dosage, [Route], InjectionSite,
                        ScheduledDateTime, AdminDateTime, VarianceMinutes)
                VALUES (@grainKey, @patientSK, @adminProviderSK, @drugSK, @adminDateKey,
                        @actionStatus, @dosage, @route, @injSite,
                        @scheduledDt, @adminDt, @variance);";

        cmd.Parameters.AddWithValue("@grainKey", grainKey);
        cmd.Parameters.AddWithValue("@patientSK", patientSK.HasValue ? patientSK.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@adminProviderSK", adminProviderSK.HasValue ? adminProviderSK.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@drugSK", drugSK.HasValue ? drugSK.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@adminDateKey", DimensionKeyResolver.ToDateKey(state.AdministrationDateTime) ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@actionStatus", (object?)state.ActionStatus ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@dosage", (object?)state.Dosage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@route", (object?)state.Route ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@injSite", (object?)state.InjectionSite ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@scheduledDt", state.ScheduledDateTime.HasValue ? state.ScheduledDateTime.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@adminDt", state.AdministrationDateTime.HasValue ? state.AdministrationDateTime.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@variance", varianceMinutes.HasValue ? varianceMinutes.Value : DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
    }
}
