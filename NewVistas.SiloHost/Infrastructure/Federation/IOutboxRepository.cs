// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
namespace NewVistas.SiloHost.Infrastructure.Federation;

/// <summary>
/// Storage abstraction for the federation outbox. The production
/// implementation is <c>SqlOutboxRepository</c> (against a deployment-local SQL
/// Server / SQL Express / Azure SQL); unit tests substitute an in-memory
/// implementation so sink and drainer logic can be exercised without a real
/// database.
/// </summary>
public interface IOutboxRepository
{
    /// <summary>
    /// Insert a new envelope row. Idempotent on <see cref="OutboxRow.EventId"/>
    /// — re-inserting an existing event id is a no-op and returns false.
    /// </summary>
    Task<bool> InsertIfNewAsync(OutboxRow row, CancellationToken cancellationToken);

    /// <summary>
    /// Read up to <paramref name="batchSize"/> envelopes that are pending
    /// shipment (<c>SentUtc IS NULL</c> and <c>NextAttemptUtc &lt;= now</c>),
    /// ordered by enqueue time so older events ship first.
    /// </summary>
    Task<IReadOnlyList<PendingOutboxEntry>> ReadPendingAsync(int batchSize, CancellationToken cancellationToken);

    /// <summary>
    /// Mark these event ids shipped (<c>SentUtc = now</c>). Idempotent — already-sent rows are not re-stamped.
    /// </summary>
    Task MarkSentAsync(IReadOnlyList<string> eventIds, CancellationToken cancellationToken);

    /// <summary>
    /// Record a transport failure: increment <c>Attempts</c>, set <c>LastError</c>,
    /// and push <c>NextAttemptUtc</c> out by <paramref name="retryAfter"/>.
    /// </summary>
    Task ScheduleRetryAsync(IReadOnlyList<string> eventIds, string error, TimeSpan retryAfter, CancellationToken cancellationToken);
}
