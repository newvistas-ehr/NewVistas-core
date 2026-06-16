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
/// Materializes AuditEventGrain state into rpt.FactAuditEvent.
/// Append-only — audit events are immutable and never updated.
/// Uses CreatedDate for watermarking instead of ModifiedOn.
/// Runs last (Priority 20) since it references PatientSK and UserSK.
/// </summary>
public class AuditEventMaterializer : ICdcEntityMaterializer
{
    private readonly ILogger<AuditEventMaterializer> _logger;

    public AuditEventMaterializer(ILogger<AuditEventMaterializer> logger) => _logger = logger;

    public string EntityName => "AuditEvent";
    public string GrainTypePattern => "%AuditEventGrain,%";
    public int Priority => 20;

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
                AuditEventState state = await grainFactory
                    .GetGrain<IAuditEventGrain>(grain.GrainKey)
                    .GetEventAsync();

                // Skip empty/unwritten events
                if (string.IsNullOrEmpty(state.Domain)) continue;

                long? patientSK = await DimensionKeyResolver.ResolvePatientSKAsync(conn, state.PatientId);
                long? userSK = await DimensionKeyResolver.UpsertProviderAsync(conn, state.UserId, state.UserName);
                long? locationSK = await DimensionKeyResolver.UpsertLocationAsync(conn, state.LocationId, state.LocationName);

                bool inserted = await InsertFactAuditEventAsync(conn, grain.GrainKey, state, patientSK, userSK, locationSK, ct);
                if (inserted) count++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "CDC AuditEvent: failed to read grain {GrainKey}, skipping",
                    grain.GrainKey);
            }
        }

        return count;
    }

    private static async Task<bool> InsertFactAuditEventAsync(
        SqlConnection conn, string grainKey, AuditEventState state,
        long? patientSK, long? userSK, long? locationSK, CancellationToken ct)
    {
        using SqlCommand cmd = conn.CreateCommand();
        // Append-only with idempotency check
        cmd.CommandText = @"
            INSERT INTO rpt.FactAuditEvent (
                AuditGrainKey, PatientSK, UserSK, LocationSK, EventDateKey,
                Domain, [Action], EntityType, EntityId, Details, OldValue, NewValue,
                EventDateTime, CDCTimestamp
            )
            SELECT
                @grainKey, @patientSK, @userSK, @locationSK, @eventDateKey,
                @domain, @action, @entityType, @entityId, @details, @oldValue, @newValue,
                @eventDt, SYSUTCDATETIME()
            WHERE NOT EXISTS (
                SELECT 1 FROM rpt.FactAuditEvent WHERE AuditGrainKey = @grainKey
            );";

        cmd.Parameters.AddWithValue("@grainKey", grainKey);
        cmd.Parameters.AddWithValue("@patientSK", patientSK.HasValue ? patientSK.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@userSK", userSK.HasValue ? userSK.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@locationSK", locationSK.HasValue ? locationSK.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@eventDateKey", DimensionKeyResolver.ToDateKey(state.Timestamp) ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@domain", state.Domain);
        cmd.Parameters.AddWithValue("@action", state.Action);
        cmd.Parameters.AddWithValue("@entityType", (object?)state.EntityType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@entityId", (object?)state.EntityId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@details", (object?)state.Details ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@oldValue", (object?)state.OldValue ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@newValue", (object?)state.NewValue ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@eventDt", state.Timestamp);

        int rows = await cmd.ExecuteNonQueryAsync(ct);
        return rows > 0;
    }
}
