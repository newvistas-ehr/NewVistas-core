// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using Orleans;

namespace NewVistas.SiloHost.Infrastructure.Cdc.Materializers;

/// <summary>
/// Materializes PatientGrain state into rpt.DimPatient (SCD Type 1 upsert).
/// Runs first (Priority 0) so PatientSK is available for all fact materializers.
/// </summary>
public class PatientMaterializer : ICdcEntityMaterializer
{
    private readonly ILogger<PatientMaterializer> _logger;

    public PatientMaterializer(ILogger<PatientMaterializer> logger) => _logger = logger;

    public string EntityName => "Patient";
    public string GrainTypePattern => "%PatientGrain,%";
    public int Priority => 0;

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
                PatientState state = await grainFactory
                    .GetGrain<IPatientGrain>(grain.GrainKey)
                    .GetPatientAsync();

                await UpsertDimPatientAsync(conn, grain.GrainKey, state, ct);
                count++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "CDC Patient: failed to read grain {GrainKey}, skipping",
                    grain.GrainKey);
            }
        }

        return count;
    }

    private static async Task UpsertDimPatientAsync(
        SqlConnection conn, string grainKey, PatientState state, CancellationToken ct)
    {
        bool isVeteran = string.Equals(state.Veteran, "Y", StringComparison.OrdinalIgnoreCase);
        string? ssnLast4 = state.SocialSecurityNumber?.Length >= 4
            ? state.SocialSecurityNumber[^4..]
            : null;
        bool isServiceConnected = state.ServiceConnectedPercentage.HasValue
            && state.ServiceConnectedPercentage.Value > 0;
        string? serviceEra = DeriveServiceEra(state.ServiceEntryDate, state.ServiceSeparationDate);

        using SqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = @"
            MERGE rpt.DimPatient AS tgt
            USING (SELECT @patientId AS PatientId) AS src
                ON tgt.PatientId = src.PatientId AND tgt.IsCurrent = 1
            WHEN MATCHED THEN
                UPDATE SET
                    PatientName = @name,
                    Sex = @sex,
                    DateOfBirth = @dob,
                    SSNLast4 = @ssn4,
                    IsVeteran = @isVet,
                    ServiceBranch = @branch,
                    ServiceEra = @era,
                    IsServiceConnected = @isSC,
                    SCPercent = @scPct,
                    LastCDCTimestamp = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (PatientId, PatientName, Sex, DateOfBirth, SSNLast4,
                        IsVeteran, ServiceBranch, ServiceEra, IsServiceConnected, SCPercent,
                        SourceGrainKey, LastCDCTimestamp)
                VALUES (@patientId, @name, @sex, @dob, @ssn4,
                        @isVet, @branch, @era, @isSC, @scPct,
                        @grainKey, SYSUTCDATETIME());";

        cmd.Parameters.AddWithValue("@patientId", state.PatientId);
        cmd.Parameters.AddWithValue("@name", (object?)state.Name ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@sex", (object?)state.Sex ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@dob", state.DateOfBirth.HasValue ? state.DateOfBirth.Value.Date : DBNull.Value);
        cmd.Parameters.AddWithValue("@ssn4", (object?)ssnLast4 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@isVet", isVeteran);
        cmd.Parameters.AddWithValue("@branch", (object?)state.ServiceBranch ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@era", (object?)serviceEra ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@isSC", isServiceConnected);
        cmd.Parameters.AddWithValue("@scPct", state.ServiceConnectedPercentage.HasValue ? state.ServiceConnectedPercentage.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@grainKey", grainKey);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Derives the VA service era from service entry/separation dates.
    /// </summary>
    private static string? DeriveServiceEra(DateTime? entryDate, DateTime? separationDate)
    {
        if (!entryDate.HasValue) return null;
        int year = entryDate.Value.Year;
        return year switch
        {
            >= 2001 => "GWOT",
            >= 1990 => "Persian Gulf",
            >= 1975 => "Post-Vietnam",
            >= 1964 => "Vietnam",
            >= 1955 => "Post-Korean",
            >= 1950 => "Korean",
            >= 1947 => "Post-WWII",
            >= 1941 => "WWII",
            _ => "Pre-WWII"
        };
    }
}
