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
/// Materializes PharmacyGrain state into rpt.FactPrescription and rpt.DimDrug.
/// </summary>
public class PrescriptionMaterializer : ICdcEntityMaterializer
{
    private readonly ILogger<PrescriptionMaterializer> _logger;

    public PrescriptionMaterializer(ILogger<PrescriptionMaterializer> logger) => _logger = logger;

    public string EntityName => "Prescription";
    public string GrainTypePattern => "%PharmacyGrain,%";
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
                PharmacyState state = await grainFactory
                    .GetGrain<IPharmacyGrain>(grain.GrainKey)
                    .GetPrescriptionAsync();

                long? patientSK = await DimensionKeyResolver.ResolvePatientSKAsync(conn, state.PatientId);
                long? prescriberSK = await DimensionKeyResolver.UpsertProviderAsync(conn, state.ProviderId, state.ProviderName);
                long? drugSK = await DimensionKeyResolver.UpsertDrugAsync(conn, state.DrugId, state.DrugName);

                await UpsertFactPrescriptionAsync(conn, grain.GrainKey, state, patientSK, prescriberSK, drugSK, ct);
                count++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "CDC Prescription: failed to read grain {GrainKey}, skipping",
                    grain.GrainKey);
            }
        }

        return count;
    }

    private static async Task UpsertFactPrescriptionAsync(
        SqlConnection conn, string grainKey, PharmacyState state,
        long? patientSK, long? prescriberSK, long? drugSK, CancellationToken ct)
    {
        using SqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = @"
            MERGE rpt.FactPrescription AS tgt
            USING (SELECT @grainKey AS PrescriptionGrainKey) AS src
                ON tgt.PrescriptionGrainKey = src.PrescriptionGrainKey
            WHEN MATCHED THEN
                UPDATE SET
                    [Status] = @status,
                    Dosage = @dosage,
                    RefillsRemaining = @refillsRem,
                    LastFillDateTime = @lastFillDt,
                    ExpirationDateTime = @expDt,
                    CDCTimestamp = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (PrescriptionGrainKey, PatientSK, PrescriberSK, DrugSK, IssueDateKey,
                        Dosage, [Route], Schedule, DaysSupply, Quantity,
                        Refills, RefillsRemaining, [Status],
                        IssueDateTime, LastFillDateTime, ExpirationDateTime)
                VALUES (@grainKey, @patientSK, @prescriberSK, @drugSK, @issueDateKey,
                        @dosage, @route, @schedule, @daysSupply, @quantity,
                        @refills, @refillsRem, @status,
                        @issueDt, @lastFillDt, @expDt);";

        cmd.Parameters.AddWithValue("@grainKey", grainKey);
        cmd.Parameters.AddWithValue("@patientSK", patientSK.HasValue ? patientSK.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@prescriberSK", prescriberSK.HasValue ? prescriberSK.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@drugSK", drugSK.HasValue ? drugSK.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@issueDateKey", DimensionKeyResolver.ToDateKey(state.IssueDate) ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@dosage", (object?)state.Dosage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@route", (object?)state.Route ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@schedule", (object?)state.Schedule ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@daysSupply", state.DaysSupply.HasValue ? state.DaysSupply.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@quantity", state.Quantity.HasValue ? (decimal)state.Quantity.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@refills", state.Refills.HasValue ? state.Refills.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@refillsRem", state.RefillsRemaining.HasValue ? state.RefillsRemaining.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@status", (object?)state.Status ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@issueDt", state.IssueDate.HasValue ? state.IssueDate.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@lastFillDt", state.LastDispenseDate.HasValue ? state.LastDispenseDate.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@expDt", state.ExpirationDate.HasValue ? state.ExpirationDate.Value : DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
    }
}
