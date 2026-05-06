// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.SiloHost.Infrastructure.Cdc.Materializers;

/// <summary>
/// Materializes LabTestGrain state into rpt.FactLabResult and rpt.DimLabTest.
/// </summary>
public class LabTestMaterializer : ICdcEntityMaterializer
{
    private readonly ILogger<LabTestMaterializer> _logger;

    public LabTestMaterializer(ILogger<LabTestMaterializer> logger) => _logger = logger;

    public string EntityName => "LabTest";
    public string GrainTypePattern => "%LabTestGrain,%";
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
                LabTestState state = await grainFactory
                    .GetGrain<ILabTestGrain>(grain.GrainKey)
                    .GetLabTestAsync();

                long? patientSK = await DimensionKeyResolver.ResolvePatientSKAsync(conn, state.PatientId);
                long? labTestSK = await DimensionKeyResolver.UpsertLabTestAsync(
                    conn, state.TestId, state.TestName, state.TestCode, state.Category);
                long? providerSK = await DimensionKeyResolver.UpsertProviderAsync(
                    conn, state.OrderingProviderId, state.OrderingProviderName);

                await UpsertFactLabResultAsync(conn, grain.GrainKey, state, patientSK, labTestSK, providerSK, ct);
                count++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "CDC LabTest: failed to read grain {GrainKey}, skipping",
                    grain.GrainKey);
            }
        }

        return count;
    }

    private static async Task UpsertFactLabResultAsync(
        SqlConnection conn, string grainKey, LabTestState state,
        long? patientSK, long? labTestSK, long? providerSK, CancellationToken ct)
    {
        bool isAbnormal = state.AbnormalFlag is "H" or "L" or "HH" or "LL" or "A" or "AA";
        bool isCritical = state.AbnormalFlag is "HH" or "LL" or "AA" || state.IsCritical;

        // Try to parse numeric result
        decimal? resultNumeric = decimal.TryParse(state.ResultValue, out decimal parsed) ? parsed : null;

        using SqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = @"
            MERGE rpt.FactLabResult AS tgt
            USING (SELECT @grainKey AS LabTestGrainKey) AS src
                ON tgt.LabTestGrainKey = src.LabTestGrainKey
            WHEN MATCHED THEN
                UPDATE SET
                    [Status] = @status,
                    ResultValue = @resultVal,
                    ResultNumeric = @resultNum,
                    ResultUnit = @resultUnit,
                    ReferenceLow = @refLow,
                    ReferenceHigh = @refHigh,
                    AbnormalFlag = @abnormal,
                    IsAbnormal = @isAbnormal,
                    IsCritical = @isCritical,
                    CollectionDateTime = @collectDt,
                    CollectionDateKey = @collectDateKey,
                    ResultDateTime = @resultDt,
                    ResultDateKey = @resultDateKey,
                    VerifiedDateTime = @verifiedDt,
                    CDCTimestamp = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (LabTestGrainKey, PatientSK, LabTestSK, OrderingProviderSK,
                        [Status], SpecimenType, PerformingLab,
                        ResultValue, ResultNumeric, ResultUnit, ReferenceLow, ReferenceHigh,
                        AbnormalFlag, IsAbnormal, IsCritical,
                        CollectionDateTime, CollectionDateKey, ResultDateTime, ResultDateKey, VerifiedDateTime)
                VALUES (@grainKey, @patientSK, @labTestSK, @providerSK,
                        @status, @specimen, @perfLab,
                        @resultVal, @resultNum, @resultUnit, @refLow, @refHigh,
                        @abnormal, @isAbnormal, @isCritical,
                        @collectDt, @collectDateKey, @resultDt, @resultDateKey, @verifiedDt);";

        cmd.Parameters.AddWithValue("@grainKey", grainKey);
        cmd.Parameters.AddWithValue("@patientSK", patientSK.HasValue ? patientSK.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@labTestSK", labTestSK.HasValue ? labTestSK.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@providerSK", providerSK.HasValue ? providerSK.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@status", (object?)state.Status ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@specimen", (object?)state.SpecimenType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@perfLab", (object?)state.PerformingLab ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@resultVal", (object?)state.ResultValue ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@resultNum", resultNumeric.HasValue ? resultNumeric.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@resultUnit", (object?)state.ResultUnit ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@refLow", (object?)state.ReferenceRangeLow ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@refHigh", (object?)state.ReferenceRangeHigh ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@abnormal", (object?)state.AbnormalFlag ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@isAbnormal", isAbnormal);
        cmd.Parameters.AddWithValue("@isCritical", isCritical);
        cmd.Parameters.AddWithValue("@collectDt", state.CollectionDateTime.HasValue ? state.CollectionDateTime.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@collectDateKey", DimensionKeyResolver.ToDateKey(state.CollectionDateTime) ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@resultDt", state.ResultDateTime.HasValue ? state.ResultDateTime.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@resultDateKey", DimensionKeyResolver.ToDateKey(state.ResultDateTime) ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@verifiedDt", state.VerifiedDateTime.HasValue ? state.VerifiedDateTime.Value : DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
    }
}
