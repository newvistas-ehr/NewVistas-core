// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Data;
using Microsoft.Data.SqlClient;

namespace NewVistas.SiloHost.Infrastructure.Federation;

/// <summary>
/// SQL Server / SQL Express implementation of <see cref="IOutboxRepository"/>.
/// Uses the connection string resolved by the profile from
/// <see cref="OutboxOptions.ConnectionStringName"/> (or its profile-specific
/// fallback).
///
/// Single-silo assumption: <see cref="ReadPendingAsync"/> does not lock claimed
/// rows. If a multi-silo cluster ever hosts the outbox, add a
/// <c>WITH (UPDLOCK, READPAST)</c> hint plus a <c>ClaimedByInstance</c> column.
/// </summary>
public sealed class SqlOutboxRepository : IOutboxRepository
{
    private readonly string _connectionString;
    private readonly ILogger<SqlOutboxRepository> _logger;

    public SqlOutboxRepository(string connectionString, ILogger<SqlOutboxRepository> logger)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Outbox connection string cannot be empty.", nameof(connectionString));
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<bool> InsertIfNewAsync(OutboxRow row, CancellationToken cancellationToken)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM [FederationOutbox] WHERE [EventId] = @EventId)
BEGIN
    INSERT INTO [FederationOutbox]
        ([EventId], [PatientId], [Domain], [EventType], [OccurredUtc],
         [SourceClusterId], [EventHash], [PreviousEventHash], [EnvelopeBlob])
    VALUES
        (@EventId, @PatientId, @Domain, @EventType, @OccurredUtc,
         @SourceClusterId, @EventHash, @PreviousEventHash, @EnvelopeBlob);
END";

        using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(cancellationToken);

        using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.Add("@EventId", SqlDbType.VarChar, 64).Value = row.EventId;
        cmd.Parameters.Add("@PatientId", SqlDbType.VarChar, 128).Value = row.PatientId;
        cmd.Parameters.Add("@Domain", SqlDbType.VarChar, 64).Value = row.Domain;
        cmd.Parameters.Add("@EventType", SqlDbType.VarChar, 128).Value = row.EventType;
        cmd.Parameters.Add("@OccurredUtc", SqlDbType.DateTime2).Value = row.OccurredUtc;
        cmd.Parameters.Add("@SourceClusterId", SqlDbType.VarChar, 64).Value = row.SourceClusterId;
        cmd.Parameters.Add("@EventHash", SqlDbType.VarChar, 128).Value = row.EventHash;
        cmd.Parameters.Add("@PreviousEventHash", SqlDbType.VarChar, 128).Value = row.PreviousEventHash;
        cmd.Parameters.Add("@EnvelopeBlob", SqlDbType.VarBinary, -1).Value = row.EnvelopeBlob;

        int affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0;
    }

    public async Task<IReadOnlyList<PendingOutboxEntry>> ReadPendingAsync(int batchSize, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT TOP (@BatchSize) [EventId], [EnvelopeBlob], [Attempts]
FROM [FederationOutbox]
WHERE [SentUtc] IS NULL AND [NextAttemptUtc] <= SYSUTCDATETIME()
ORDER BY [EnqueuedUtc]";

        using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(cancellationToken);

        using SqlCommand cmd = new(sql, conn);
        cmd.Parameters.Add("@BatchSize", SqlDbType.Int).Value = batchSize;

        var entries = new List<PendingOutboxEntry>(batchSize);
        using SqlDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new PendingOutboxEntry(
                EventId: reader.GetString(0),
                EnvelopeBlob: (byte[])reader[1],
                Attempts: reader.GetInt32(2)));
        }
        return entries;
    }

    public async Task MarkSentAsync(IReadOnlyList<string> eventIds, CancellationToken cancellationToken)
    {
        if (eventIds.Count == 0) return;

        // Build a parameterized IN clause; SQL Server has no native bulk update by id list.
        string idList = string.Join(",", Enumerable.Range(0, eventIds.Count).Select(i => $"@id{i}"));
        string sql = $@"
UPDATE [FederationOutbox]
SET [SentUtc] = SYSUTCDATETIME()
WHERE [SentUtc] IS NULL AND [EventId] IN ({idList})";

        using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(cancellationToken);

        using SqlCommand cmd = new(sql, conn);
        for (int i = 0; i < eventIds.Count; i++)
        {
            cmd.Parameters.Add($"@id{i}", SqlDbType.VarChar, 64).Value = eventIds[i];
        }
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task ScheduleRetryAsync(IReadOnlyList<string> eventIds, string error, TimeSpan retryAfter, CancellationToken cancellationToken)
    {
        if (eventIds.Count == 0) return;

        string idList = string.Join(",", Enumerable.Range(0, eventIds.Count).Select(i => $"@id{i}"));
        string sql = $@"
UPDATE [FederationOutbox]
SET [Attempts] = [Attempts] + 1,
    [LastError] = @LastError,
    [NextAttemptUtc] = DATEADD(SECOND, @RetrySeconds, SYSUTCDATETIME())
WHERE [SentUtc] IS NULL AND [EventId] IN ({idList})";

        using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(cancellationToken);

        using SqlCommand cmd = new(sql, conn);
        // Cap LastError length to the column width so a long stack trace
        // doesn't fail the update.
        string truncated = error.Length > 2000 ? error[..2000] : error;
        cmd.Parameters.Add("@LastError", SqlDbType.NVarChar, 2000).Value = truncated;
        cmd.Parameters.Add("@RetrySeconds", SqlDbType.Int).Value = (int)retryAfter.TotalSeconds;
        for (int i = 0; i < eventIds.Count; i++)
        {
            cmd.Parameters.Add($"@id{i}", SqlDbType.VarChar, 64).Value = eventIds[i];
        }
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
