// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Orleans;

namespace NewVistas.SiloHost.Infrastructure.Cdc;

/// <summary>
/// Background service that polls OrleansStorage for changed grains, reads their state
/// via grain interfaces, and materializes into the rpt.* star schema for reporting.
///
/// Replaces the SQL-only CDC pipeline (002-CDCViews.sql / 003-CDCMaterialize.sql) which
/// assumed JSON payloads. This service works with binary grain storage by calling
/// grain Get*Async() methods through IGrainFactory.
/// </summary>
public class CdcMaterializationService : BackgroundService
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<CdcMaterializationService> _logger;
    private readonly CdcOptions _options;
    private readonly IConfiguration _configuration;
    private readonly IReadOnlyList<ICdcEntityMaterializer> _materializers;

    public CdcMaterializationService(
        IGrainFactory grainFactory,
        ILogger<CdcMaterializationService> logger,
        IOptions<CdcOptions> options,
        IConfiguration configuration,
        IEnumerable<ICdcEntityMaterializer> materializers)
    {
        _grainFactory = grainFactory;
        _logger = logger;
        _options = options.Value;
        _configuration = configuration;
        _materializers = materializers.OrderBy(m => m.Priority).ThenBy(m => m.EntityName).ToList();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("CDC materialization service is disabled via configuration");
            return;
        }

        _logger.LogInformation(
            "CDC materialization service starting — {Count} materializers, polling every {Interval}s",
            _materializers.Count, _options.PollingIntervalSeconds);

        // Let the silo stabilize before first poll
        await Task.Delay(TimeSpan.FromSeconds(_options.StartupDelaySeconds), stoppingToken);

        string connectionString = ResolveConnectionString();

        // Ensure watermark table exists
        await EnsureWatermarkTableAsync(connectionString, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            Stopwatch cycleSw = Stopwatch.StartNew();
            int totalRows = 0;
            int entitiesProcessed = 0;

            foreach (ICdcEntityMaterializer materializer in _materializers)
            {
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    int rows = await ProcessMaterializerAsync(materializer, connectionString, stoppingToken);
                    if (rows > 0)
                    {
                        totalRows += rows;
                        entitiesProcessed++;
                    }
                }
                catch (Exception ex)
                {
                    // Watermark not advanced — retry entire batch next cycle
                    _logger.LogError(ex,
                        "CDC {EntityName}: materialization failed, will retry next cycle",
                        materializer.EntityName);
                }
            }

            cycleSw.Stop();
            if (totalRows > 0)
            {
                _logger.LogInformation(
                    "CDC cycle completed: {TotalRows} rows across {EntityCount} entities in {Duration}ms",
                    totalRows, entitiesProcessed, cycleSw.ElapsedMilliseconds);
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.PollingIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("CDC materialization service stopped");
    }

    private async Task<int> ProcessMaterializerAsync(
        ICdcEntityMaterializer materializer, string connectionString, CancellationToken ct)
    {
        await using SqlConnection conn = new(connectionString);
        await conn.OpenAsync(ct);

        // Read watermark
        DateTime watermark = await ReadWatermarkAsync(conn, materializer.EntityName, ct);

        // Query OrleansStorage for changed grain keys
        List<ChangedGrainInfo> changedGrains = await QueryChangedGrainsAsync(
            conn, materializer.GrainTypePattern, watermark, _options.BatchSize, ct);

        if (changedGrains.Count == 0)
            return 0;

        _logger.LogDebug(
            "CDC {EntityName}: processing {Count} changed grains since {Watermark:O}",
            materializer.EntityName, changedGrains.Count, watermark);

        Stopwatch sw = Stopwatch.StartNew();

        // Materialize the batch
        int rowCount = await materializer.MaterializeAsync(changedGrains, conn, _grainFactory, ct);

        sw.Stop();

        // Advance watermark to the max ModifiedOn from the processed batch
        DateTime newWatermark = changedGrains.Max(g => g.ModifiedOn);
        await UpdateWatermarkAsync(conn, materializer.EntityName, newWatermark, rowCount, (int)sw.ElapsedMilliseconds, ct);

        _logger.LogInformation(
            "CDC {EntityName}: materialized {RowCount} rows from {GrainCount} grains in {Duration}ms",
            materializer.EntityName, rowCount, changedGrains.Count, sw.ElapsedMilliseconds);

        return rowCount;
    }

    private static async Task<DateTime> ReadWatermarkAsync(
        SqlConnection conn, string entityName, CancellationToken ct)
    {
        using SqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT LastProcessedAt FROM rpt.CDCWatermark WHERE EntityName = @name";
        cmd.Parameters.AddWithValue("@name", entityName);
        object? result = await cmd.ExecuteScalarAsync(ct);
        return result is DateTime dt ? dt : new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    private static async Task<List<ChangedGrainInfo>> QueryChangedGrainsAsync(
        SqlConnection conn, string grainTypePattern, DateTime watermark, int batchSize, CancellationToken ct)
    {
        List<ChangedGrainInfo> results = new();
        using SqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT TOP(@batchSize) GrainIdExtensionString, ModifiedOn
            FROM OrleansStorage
            WHERE GrainTypeString LIKE @pattern
              AND ModifiedOn > @watermark
            ORDER BY ModifiedOn ASC";
        cmd.Parameters.AddWithValue("@batchSize", batchSize);
        cmd.Parameters.AddWithValue("@pattern", grainTypePattern);
        cmd.Parameters.AddWithValue("@watermark", watermark);

        using SqlDataReader reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            string? grainKey = reader.IsDBNull(0) ? null : reader.GetString(0);
            if (grainKey != null)
            {
                results.Add(new ChangedGrainInfo(grainKey, reader.GetDateTime(1)));
            }
        }

        return results;
    }

    private static async Task UpdateWatermarkAsync(
        SqlConnection conn, string entityName, DateTime watermark, int rowCount, int durationMs, CancellationToken ct)
    {
        using SqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = @"
            MERGE rpt.CDCWatermark AS tgt
            USING (SELECT @name AS EntityName) AS src ON tgt.EntityName = src.EntityName
            WHEN MATCHED THEN
                UPDATE SET LastProcessedAt = @watermark, LastRowCount = @count, LastRunDurationMs = @duration
            WHEN NOT MATCHED THEN
                INSERT (EntityName, LastProcessedAt, LastRowCount, LastRunDurationMs)
                VALUES (@name, @watermark, @count, @duration);";
        cmd.Parameters.AddWithValue("@name", entityName);
        cmd.Parameters.AddWithValue("@watermark", watermark);
        cmd.Parameters.AddWithValue("@count", rowCount);
        cmd.Parameters.AddWithValue("@duration", durationMs);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task EnsureWatermarkTableAsync(string connectionString, CancellationToken ct)
    {
        await using SqlConnection conn = new(connectionString);
        await conn.OpenAsync(ct);
        using SqlCommand cmd = conn.CreateCommand();
        cmd.CommandText = @"
            IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'rpt')
                EXEC('CREATE SCHEMA rpt');
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID('rpt') AND name = 'CDCWatermark')
                CREATE TABLE rpt.CDCWatermark (
                    EntityName      VARCHAR(100)    NOT NULL PRIMARY KEY,
                    LastProcessedAt DATETIME2       NOT NULL DEFAULT '2000-01-01',
                    LastRowCount    INT             NOT NULL DEFAULT 0,
                    LastRunDurationMs INT           NULL
                );";
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private string ResolveConnectionString()
    {
        if (_options.ReportingConnectionStringName is not null)
        {
            return _configuration.GetConnectionString(_options.ReportingConnectionStringName)
                ?? throw new InvalidOperationException(
                    $"Connection string '{_options.ReportingConnectionStringName}' not found for CDC reporting.");
        }

        return _configuration.GetConnectionString("SqlExpress")
            ?? _configuration.GetConnectionString("OrleansDatabase")
            ?? throw new InvalidOperationException(
                "No SQL connection string found for CDC service. Configure 'SqlExpress', 'OrleansDatabase', or set Cdc:ReportingConnectionStringName.");
    }
}
