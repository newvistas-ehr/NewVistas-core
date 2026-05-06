// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Reflection;
using Microsoft.Data.SqlClient;

namespace NewVistas.SiloHost.Infrastructure;

/// <summary>
/// Ensures the NewVistasDB SQL Express database exists and has the Orleans schema applied.
/// Run before Orleans starts up. Idempotent — safe to call on every startup.
/// Delete the database to reset all demo data; it will be recreated automatically.
/// </summary>
public static class DatabaseInitializer
{
    private const string MainSchemaResource = "NewVistas.SiloHost.Sql.OrleansMainSchema.sql";
    private const string ClusteringSchemaResource = "NewVistas.SiloHost.Sql.OrleansClusteringSchema.sql";
    private const string PersistenceSchemaResource = "NewVistas.SiloHost.Sql.OrleansPersistenceSchema.sql";
    private const string FederationOutboxSchemaResource = "NewVistas.SiloHost.Sql.FederationOutboxSchema.sql";

    public static async Task EnsureDatabaseAsync(string connectionString, ILogger logger)
    {
        string databaseName = GetDatabaseName(connectionString);
        string masterConnectionString = BuildMasterConnectionString(connectionString);

        // Step 1: Create the database if it doesn't exist
        await EnsureDatabaseExistsAsync(masterConnectionString, databaseName, logger);

        // Step 2: Apply Orleans schema if not already present
        await EnsureOrleansSchemaAsync(connectionString, logger);
    }

    /// <summary>
    /// Ensures the Orleans schema tables exist in an already-created database.
    /// Use this for Azure SQL or any environment where the database is pre-created
    /// (e.g. via <c>az sql db create</c>) and only the schema needs to be applied.
    /// </summary>
    public static async Task EnsureSchemaAsync(string connectionString, ILogger logger)
    {
        await EnsureOrleansSchemaAsync(connectionString, logger);
    }

    /// <summary>
    /// Ensures the <c>FederationOutbox</c> table exists in an already-created
    /// database. Idempotent — the schema script's <c>IF NOT EXISTS</c> guards
    /// make every call after the first a no-op. Called only by site profiles
    /// that opt into the federation outbox.
    /// </summary>
    public static async Task EnsureFederationOutboxSchemaAsync(string connectionString, ILogger logger)
    {
        using SqlConnection conn = new(connectionString);
        await conn.OpenAsync();

        logger.LogInformation("Applying federation outbox schema (idempotent).");
        await ExecuteEmbeddedScriptAsync(conn, FederationOutboxSchemaResource, logger);
    }

    private static string GetDatabaseName(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        return builder.InitialCatalog;
    }

    private static string BuildMasterConnectionString(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        builder.InitialCatalog = "master";
        return builder.ConnectionString;
    }

    private static async Task EnsureDatabaseExistsAsync(string masterConnectionString, string databaseName, ILogger logger)
    {
        using SqlConnection conn = new(masterConnectionString);
        await conn.OpenAsync();

        using SqlCommand checkCmd = new(
            "SELECT database_id FROM sys.databases WHERE Name = @dbName", conn);
        checkCmd.Parameters.AddWithValue("@dbName", databaseName);

        object? result = await checkCmd.ExecuteScalarAsync();

        if (result is null || result == DBNull.Value)
        {
            logger.LogInformation("Database '{DbName}' not found — creating on SQL Express.", databaseName);

            // Sanitize: use bracket-quoted name to prevent injection (databaseName comes from config)
            using SqlCommand createCmd = new(
                $"CREATE DATABASE [{databaseName.Replace("]", "]]")}]", conn);
            await createCmd.ExecuteNonQueryAsync();

            logger.LogInformation("Database '{DbName}' created successfully.", databaseName);
        }
        else
        {
            logger.LogInformation("Database '{DbName}' already exists.", databaseName);
        }
    }

    private static async Task EnsureOrleansSchemaAsync(string connectionString, ILogger logger)
    {
        using SqlConnection conn = new(connectionString);
        await conn.OpenAsync();

        // Check 1: Do the Orleans tables exist?
        using SqlCommand tableCheckCmd = new(
            "SELECT OBJECT_ID(N'[OrleansStorage]', 'U')", conn);
        object? tableResult = await tableCheckCmd.ExecuteScalarAsync();
        bool tablesExist = tableResult is not null && tableResult != DBNull.Value;

        if (tablesExist)
        {
            // Check 2: Do the required OrleansQuery rows exist?
            // These are absent if a prior deployment ran only the CREATE TABLE batches
            // but not the INSERT batches (GO batch-separator bug in SqlCommand execution).
            using SqlCommand rowCheckCmd = new(
                "SELECT COUNT(*) FROM [OrleansQuery] WHERE [QueryKey] = 'GatewaysQueryKey'", conn);
            int rowCount = (int)(await rowCheckCmd.ExecuteScalarAsync() ?? 0);

            if (rowCount > 0)
            {
                logger.LogInformation("Orleans schema already present — skipping initialization.");
                return;
            }

            // Tables exist but data rows are missing — drop the schema so we can
            // recreate it cleanly with the full scripts (including all INSERT batches).
            logger.LogWarning(
                "Orleans tables exist but OrleansQuery rows are missing — " +
                "dropping and recreating schema to recover from incomplete initialization.");
            await DropOrleansSchemaAsync(conn, logger);
        }

        logger.LogInformation("Orleans schema not found — running initialization scripts.");

        // Run Main schema first (creates OrleansQuery table and sets isolation)
        await ExecuteEmbeddedScriptAsync(conn, MainSchemaResource, logger);

        // Run Clustering schema (creates OrleansMembershipVersionTable, OrleansMembershipTable,
        // and inserts all 9 clustering query rows including CleanupDefunctSiloEntriesKey)
        await ExecuteEmbeddedScriptAsync(conn, ClusteringSchemaResource, logger);

        // Run Persistence schema (creates OrleansStorage table and inserts persistence query rows)
        await ExecuteEmbeddedScriptAsync(conn, PersistenceSchemaResource, logger);

        logger.LogInformation("Orleans schema initialized successfully.");
    }

    private static async Task DropOrleansSchemaAsync(SqlConnection conn, ILogger logger)
    {
        // Orleans membership state is transient — silos re-register on restart.
        // Dropping and recreating is safe and is the cleanest recovery path.
        string[] tablesToDrop =
        [
            "OrleansStorage",
            "OrleansMembershipTable",
            "OrleansMembershipVersionTable",
            "OrleansRemindersTable",
            "OrleansQuery"
        ];

        foreach (string table in tablesToDrop)
        {
            using SqlCommand dropCmd = new(
                $"IF OBJECT_ID(N'[{table}]', 'U') IS NOT NULL DROP TABLE [{table}]", conn);
            dropCmd.CommandTimeout = 60;
            await dropCmd.ExecuteNonQueryAsync();
            logger.LogDebug("Dropped table [{Table}] (if existed).", table);
        }
    }

    private static async Task ExecuteEmbeddedScriptAsync(SqlConnection conn, string resourceName, ILogger logger)
    {
        Assembly assembly = typeof(DatabaseInitializer).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream(resourceName);

        if (stream is null)
        {
            // Log available resources to help diagnose naming issues
            string available = string.Join(", ", assembly.GetManifestResourceNames());
            throw new InvalidOperationException(
                $"Embedded SQL resource '{resourceName}' not found. Available: [{available}]");
        }

        using StreamReader reader = new(stream);
        string sql = await reader.ReadToEndAsync();

        // Orleans SQL Server scripts use GO as a batch separator (an SSMS/sqlcmd tool command,
        // not T-SQL). SqlCommand does not process GO — split into batches and execute each separately.
        string[] batches = System.Text.RegularExpressions.Regex.Split(
            sql,
            @"^\s*GO\s*(?:--[^\r\n]*)?\r?$",
            System.Text.RegularExpressions.RegexOptions.Multiline |
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        logger.LogDebug("Executing SQL script '{ResourceName}' in {Count} batches.", resourceName, batches.Length);

        foreach (string batch in batches)
        {
            string trimmedBatch = batch.Trim();
            if (string.IsNullOrWhiteSpace(trimmedBatch)) continue;

            using SqlCommand cmd = new(trimmedBatch, conn);
            cmd.CommandTimeout = 120;
            await cmd.ExecuteNonQueryAsync();
        }
    }
}




