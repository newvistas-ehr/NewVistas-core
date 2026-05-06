// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.Helpers;

namespace NewVistas.Abstractions.Importers;

/// <summary>
/// Top-level orchestrator for ZWR import. Parses files, runs importers in dependency order:
/// Phase 1   — Patients (all other domains reference patient grain keys)
/// Phase 1.5 — Pre-populate user IEN mappings (for CareTeam provider references)
/// Phase 2   — All other clinical domains in parallel
/// </summary>
public class ZwrImportOrchestrator
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger _logger;
    private readonly IenMap _ienMap = new();

    public ZwrImportOrchestrator(IGrainFactory grainFactory, ILogger logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    public Task<ImportResult> ImportAsync(string path) => ImportAsync(path, null);

    public async Task<ImportResult> ImportAsync(string path, Dictionary<long, string>? userIenToGrainKey)
    {
        var result = new ImportResult();

        // 1. Collect all ZWR files
        string[] files;
        if (Directory.Exists(path))
        {
            files = Directory.GetFiles(path, "*.zwr", SearchOption.AllDirectories);
            if (files.Length == 0)
            {
                _logger.LogWarning("No .zwr files found in {Path}", path);
                return result;
            }
        }
        else if (File.Exists(path))
        {
            files = [path];
        }
        else
        {
            _logger.LogError("Path not found: {Path}", path);
            return result;
        }

        _logger.LogInformation("Found {Count} ZWR file(s) to import", files.Length);

        // 2. Parse all files into grouped records
        var allRecords = new Dictionary<(string Global, string? FileNumber, long Ien), List<ZwrRecord>>();

        foreach (string file in files)
        {
            _logger.LogInformation("Parsing {File}...", Path.GetFileName(file));
            try
            {
                Dictionary<(string, string?, long), List<ZwrRecord>> parsed = ZwrParser.ParseFile(file);
                foreach (var kvp in parsed)
                {
                    if (!allRecords.TryGetValue(kvp.Key, out List<ZwrRecord>? existing))
                    {
                        allRecords[kvp.Key] = kvp.Value;
                    }
                    else
                    {
                        existing.AddRange(kvp.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse {File}", file);
            }
        }

        _logger.LogInformation("Parsed {Count} record groups across {Files} file(s)",
            allRecords.Count, files.Length);

        // 3. Phase 1: Import patients first (all other domains reference patient keys)
        _logger.LogInformation("Phase 1: Importing patients...");
        var patientImporter = new PatientImporter(_grainFactory, _ienMap, _logger);
        await patientImporter.ImportAsync(allRecords, result);
        _logger.LogInformation("Phase 1 complete: {Count} patients imported", result.PatientCount);

        // 3.5. Phase 1.5: Pre-populate user IEN → grain key mappings
        // Users are created by Program.cs with known structure; register them in the IenMap
        // so CareTeamImporter (and other importers) can resolve provider references.
        PopulateUserMappings(userIenToGrainKey);

        // 4. Phase 2: Import all other domains in parallel
        _logger.LogInformation("Phase 2: Importing clinical domains...");

        await Task.WhenAll(
            new AllergyImporter(_grainFactory, _ienMap, _logger).ImportAsync(allRecords, result),
            new ProblemImporter(_grainFactory, _ienMap, _logger).ImportAsync(allRecords, result),
            new OrderImporter(_grainFactory, _ienMap, _logger).ImportAsync(allRecords, result),
            new LabImporter(_grainFactory, _ienMap, _logger).ImportAsync(allRecords, result),
            new VitalImporter(_grainFactory, _ienMap, _logger).ImportAsync(allRecords, result),
            new TiuImporter(_grainFactory, _ienMap, _logger).ImportAsync(allRecords, result),
            new ConsultImporter(_grainFactory, _ienMap, _logger).ImportAsync(allRecords, result),
            new SurgeryImporter(_grainFactory, _ienMap, _logger).ImportAsync(allRecords, result),
            new RadiologyImporter(_grainFactory, _ienMap, _logger).ImportAsync(allRecords, result),
            new AdtImporter(_grainFactory, _ienMap, _logger).ImportAsync(allRecords, result),
            new PharmacyImporter(_grainFactory, _ienMap, _logger).ImportAsync(allRecords, result),
            new ImmunizationImporter(_grainFactory, _ienMap, _logger).ImportAsync(allRecords, result),
            new NursingImporter(_grainFactory, _ienMap, _logger).ImportAsync(allRecords, result),
            new DentalImporter(_grainFactory, _ienMap, _logger).ImportAsync(allRecords, result),
            new MentalHealthImporter(_grainFactory, _ienMap, _logger).ImportAsync(allRecords, result),
            new SocialWorkImporter(_grainFactory, _ienMap, _logger).ImportAsync(allRecords, result),
            new HealthFactorImporter(_grainFactory, _ienMap, _logger).ImportAsync(allRecords, result),
            new DietOrderImporter(_grainFactory, _ienMap, _logger).ImportAsync(allRecords, result),
            new ProstheticsImporter(_grainFactory, _ienMap, _logger).ImportAsync(allRecords, result),
            new MeansTestImporter(_grainFactory, _ienMap, _logger).ImportAsync(allRecords, result),
            new ScConditionImporter(_grainFactory, _ienMap, _logger).ImportAsync(allRecords, result),
            new ReminderImporter(_grainFactory, _ienMap, _logger).ImportAsync(allRecords, result),
            new CareTeamImporter(_grainFactory, _ienMap, _logger).ImportAsync(allRecords, result));

        _logger.LogInformation("Phase 2 complete: {Total} total records imported across all domains",
            result.TotalImported);

        return result;
    }

    /// <summary>
    /// Pre-populate the IenMap with user IEN → grain key mappings.
    /// VistA NEW PERSON file (#200) IENs are mapped to the grain keys used by
    /// Program.cs when seeding demo users (e.g., IEN 1 → "USER:{aspNetId}").
    /// This allows importers like CareTeamImporter to resolve provider references.
    /// </summary>
    private void PopulateUserMappings(Dictionary<long, string>? userIenToGrainKey)
    {
        if (userIenToGrainKey == null || userIenToGrainKey.Count == 0)
        {
            _logger.LogInformation("Phase 1.5: No user IEN mappings provided — skipping");
            return;
        }

        foreach (var (ien, grainKey) in userIenToGrainKey)
        {
            _ienMap.Register("VA200", ien, grainKey);
        }

        _logger.LogInformation("Phase 1.5: Registered {Count} user IEN → grain key mappings",
            userIenToGrainKey.Count);
    }
}
