// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Data;
using Microsoft.Data.SqlClient;
using NewVistas.Abstractions.Federation;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.SiloHost.Infrastructure.Federation;

/// <summary>
/// SQL implementation of <see cref="IOutboxStatistics"/>. Runs a single
/// aggregate query against <c>FederationOutbox</c>:
///
/// <list type="bullet">
///   <item><description>pending count (sent_utc IS NULL)</description></item>
///   <item><description>sent count</description></item>
///   <item><description>oldest pending row (MIN enqueued_utc WHERE sent_utc IS NULL)</description></item>
///   <item><description>max attempts on any pending row (MAX attempts WHERE sent_utc IS NULL)</description></item>
///   <item><description>last sent timestamp (MAX sent_utc)</description></item>
/// </list>
///
/// All in one round-trip. The filtered index
/// <c>IX_FederationOutbox_Pending</c> keeps the pending-row predicates cheap;
/// the sent-count scan is full-table but bounded — at the deployment scales
/// this targets, the table is at most low millions of rows.
/// </summary>
public sealed class SqlOutboxStatistics : IOutboxStatistics
{
    private const int CommandTimeoutSeconds = 10;

    private readonly string _connectionString;

    public SqlOutboxStatistics(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Outbox connection string is required.", nameof(connectionString));
        _connectionString = connectionString;
    }

    public async Task<OutboxStats> GetAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    SUM(CASE WHEN [SentUtc] IS NULL THEN 1 ELSE 0 END) AS Pending,
    SUM(CASE WHEN [SentUtc] IS NOT NULL THEN 1 ELSE 0 END) AS Sent,
    MIN(CASE WHEN [SentUtc] IS NULL THEN [EnqueuedUtc] END) AS OldestPendingUtc,
    MAX(CASE WHEN [SentUtc] IS NULL THEN [Attempts] ELSE 0 END) AS MaxAttemptsOnPending,
    MAX([SentUtc]) AS LastSentUtc
FROM [FederationOutbox]";

        using SqlConnection conn = new(_connectionString);
        await conn.OpenAsync(cancellationToken);

        using SqlCommand cmd = new(sql, conn) { CommandTimeout = CommandTimeoutSeconds };
        using SqlDataReader reader = await cmd.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return new OutboxStats(0, 0, null, 0, null, Available: true);
        }

        int pending = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
        int sent = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
        DateTime? oldestPending = reader.IsDBNull(2) ? null : reader.GetDateTime(2);
        int maxAttempts = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
        DateTime? lastSent = reader.IsDBNull(4) ? null : reader.GetDateTime(4);

        return new OutboxStats(pending, sent, oldestPending, maxAttempts, lastSent, Available: true);
    }
}
