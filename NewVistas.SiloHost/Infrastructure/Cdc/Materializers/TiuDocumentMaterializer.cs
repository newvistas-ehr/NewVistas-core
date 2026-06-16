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
/// Materializes TiuDocumentGrain state into rpt.FactNote.
/// </summary>
public class TiuDocumentMaterializer : ICdcEntityMaterializer
{
    private readonly ILogger<TiuDocumentMaterializer> _logger;

    public TiuDocumentMaterializer(ILogger<TiuDocumentMaterializer> logger) => _logger = logger;

    public string EntityName => "TiuDocument";
    public string GrainTypePattern => "%TiuDocumentGrain,%";
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
                TiuDocumentState state = await grainFactory
                    .GetGrain<ITiuDocumentGrain>(grain.GrainKey)
                    .GetDocumentAsync();

                long? patientSK = await DimensionKeyResolver.ResolvePatientSKAsync(conn, state.PatientId);
                long? authorSK = await DimensionKeyResolver.UpsertProviderAsync(conn, state.AuthorId, state.AuthorName);
                long? locationSK = await DimensionKeyResolver.UpsertLocationAsync(conn, state.LocationId, state.LocationName);

                await UpsertFactNoteAsync(conn, grain.GrainKey, state, patientSK, authorSK, locationSK, ct);
                count++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "CDC TiuDocument: failed to read grain {GrainKey}, skipping",
                    grain.GrainKey);
            }
        }

        return count;
    }

    private static async Task UpsertFactNoteAsync(
        SqlConnection conn, string grainKey, TiuDocumentState state,
        long? patientSK, long? authorSK, long? locationSK, CancellationToken ct)
    {
        bool hasAddenda = state.AddendumIds.Count > 0;
        int textLength = state.ReportText?.Length ?? 0;

        // Hours from entry to signature
        decimal? hoursToSign = state.SignedDateTime.HasValue
            ? (decimal)(state.SignedDateTime.Value - state.EntryDate).TotalHours
            : null;

        using SqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = @"
            MERGE rpt.FactNote AS tgt
            USING (SELECT @grainKey AS NoteGrainKey) AS src
                ON tgt.NoteGrainKey = src.NoteGrainKey
            WHEN MATCHED THEN
                UPDATE SET
                    [Status] = @status,
                    HasAddenda = @hasAddenda,
                    AddendumCount = @addendumCount,
                    TextLength = @textLen,
                    HoursToSign = @hoursToSign,
                    SignedDateTime = @signedDt,
                    CDCTimestamp = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (NoteGrainKey, PatientSK, AuthorSK, LocationSK, ReferenceDateKey,
                        DocumentType, [Subject], [Status], HasAddenda, AddendumCount,
                        TextLength, HoursToSign,
                        ReferenceDateTime, EntryDateTime, SignedDateTime)
                VALUES (@grainKey, @patientSK, @authorSK, @locationSK, @refDateKey,
                        @docType, @subject, @status, @hasAddenda, @addendumCount,
                        @textLen, @hoursToSign,
                        @refDt, @entryDt, @signedDt);";

        cmd.Parameters.AddWithValue("@grainKey", grainKey);
        cmd.Parameters.AddWithValue("@patientSK", patientSK.HasValue ? patientSK.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@authorSK", authorSK.HasValue ? authorSK.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@locationSK", locationSK.HasValue ? locationSK.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@refDateKey", DimensionKeyResolver.ToDateKey(state.ReferenceDate) ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@docType", (object?)state.DocumentType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@subject", (object?)state.Subject ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@status", (object?)state.Status ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@hasAddenda", hasAddenda);
        cmd.Parameters.AddWithValue("@addendumCount", state.AddendumIds.Count);
        cmd.Parameters.AddWithValue("@textLen", textLength);
        cmd.Parameters.AddWithValue("@hoursToSign", hoursToSign.HasValue ? hoursToSign.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@refDt", state.ReferenceDate);
        cmd.Parameters.AddWithValue("@entryDt", state.EntryDate);
        cmd.Parameters.AddWithValue("@signedDt", state.SignedDateTime.HasValue ? state.SignedDateTime.Value : DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
    }
}
