// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.Abstractions.Services;

/// <summary>
/// Bulk import service for reference data code sets.
/// Reads CSV streams and populates Orleans grains in batches.
///
/// This mirrors VistA's KIDS install process for loading standardized
/// code files (ICD-10, CPT, LOINC, NDF) into the system.
/// </summary>
public class ReferenceDataImportService : IReferenceDataImportService
{
    private readonly ILogger<ReferenceDataImportService> _logger;
    private const int BatchSize = 100;

    public ReferenceDataImportService(ILogger<ReferenceDataImportService> logger)
    {
        _logger = logger;
    }

    public async Task<ImportResult> ImportIcd10CodesAsync(
        IGrainFactory grainFactory, Stream csvStream, bool idempotent = true)
    {
        Stopwatch sw = Stopwatch.StartNew();
        ImportResult result = new();
        List<Icd10IndexEntry> indexEntries = new();

        await foreach (string[] fields in ReadCsvAsync(csvStream))
        {
            result.TotalRecords++;

            if (fields.Length < 4)
            {
                result.ErrorRecords++;
                result.Errors.Add($"Line {result.TotalRecords}: Expected 4 columns (Code, ShortDescription, LongDescription, Category), got {fields.Length}");
                continue;
            }

            try
            {
                string code = fields[0].Trim();
                string shortDesc = fields[1].Trim();
                string longDesc = fields[2].Trim();
                string category = fields[3].Trim();

                if (string.IsNullOrEmpty(code))
                {
                    result.ErrorRecords++;
                    result.Errors.Add($"Line {result.TotalRecords}: Empty code");
                    continue;
                }

                string grainKey = code.Contains("ICD10:") ? code : code;
                IIcd10Grain grain = grainFactory.GetGrain<IIcd10Grain>(grainKey);

                if (idempotent)
                {
                    Icd10State existing = await grain.GetCodeAsync();
                    if (!string.IsNullOrEmpty(existing.ShortDescription))
                    {
                        result.SkippedRecords++;
                        continue;
                    }
                }

                await grain.LoadCodeAsync(code, shortDesc, longDesc, true, result.TotalRecords, category);
                result.ImportedRecords++;

                indexEntries.Add(new Icd10IndexEntry
                {
                    Code = code,
                    ShortDescription = shortDesc,
                    LongDescription = longDesc,
                    IsBillable = true,
                    OrderNumber = result.TotalRecords,
                    Chapter = category,
                    IsActive = true
                });
            }
            catch (Exception ex)
            {
                result.ErrorRecords++;
                result.Errors.Add($"Line {result.TotalRecords}: {ex.Message}");
            }

            // Batch index update
            if (indexEntries.Count >= BatchSize)
            {
                await FlushIcd10IndexAsync(grainFactory, indexEntries);
            }
        }

        // Final flush
        if (indexEntries.Count > 0)
        {
            await FlushIcd10IndexAsync(grainFactory, indexEntries);
        }

        sw.Stop();
        result.Duration = sw.Elapsed;

        _logger.LogInformation(
            "ICD-10 import complete: {Total} total, {Imported} imported, {Skipped} skipped, {Errors} errors in {Duration}",
            result.TotalRecords, result.ImportedRecords, result.SkippedRecords, result.ErrorRecords, result.Duration);

        return result;
    }

    public async Task<ImportResult> ImportDrugProductsAsync(
        IGrainFactory grainFactory, Stream csvStream, bool idempotent = true)
    {
        Stopwatch sw = Stopwatch.StartNew();
        ImportResult result = new();
        List<VaProductIndexEntry> indexEntries = new();

        await foreach (string[] fields in ReadCsvAsync(csvStream))
        {
            result.TotalRecords++;

            if (fields.Length < 10)
            {
                result.ErrorRecords++;
                result.Errors.Add($"Line {result.TotalRecords}: Expected 10 columns, got {fields.Length}");
                continue;
            }

            try
            {
                string ien = fields[0].Trim();
                string name = fields[1].Trim();
                string genericName = fields[2].Trim();
                string drugClassCode = fields[3].Trim();
                string drugClassName = fields[4].Trim();
                string ndc = fields[5].Trim();
                string strength = fields[6].Trim();
                string dosageForm = fields[7].Trim();
                string route = fields[8].Trim();
                string unit = fields[9].Trim();

                if (string.IsNullOrEmpty(ien))
                {
                    result.ErrorRecords++;
                    result.Errors.Add($"Line {result.TotalRecords}: Empty IEN");
                    continue;
                }

                IVaProductGrain grain = grainFactory.GetGrain<IVaProductGrain>(ien);

                if (idempotent)
                {
                    VaProductState existing = await grain.GetProductAsync();
                    if (!string.IsNullOrEmpty(existing.Name))
                    {
                        result.SkippedRecords++;
                        continue;
                    }
                }

                await grain.LoadProductAsync(
                    name: name,
                    vaGenericIen: $"GEN-{ien}",
                    vaGenericName: genericName,
                    dosageFormIen: $"DF-{ien}",
                    dosageFormName: dosageForm,
                    strength: strength,
                    strengthUnitIen: $"SU-{ien}",
                    strengthUnitName: unit,
                    printName: name,
                    primaryDrugClassCode: drugClassCode,
                    primaryDrugClassName: drugClassName,
                    formularyIndicator: true,
                    formularyRestrictions: null,
                    controlledSubstanceSchedule: null,
                    copayTier: null,
                    maxSingleDose: null,
                    minSingleDose: null,
                    maxDailyDose: null,
                    minDailyDose: null,
                    maxCumulativeDose: null,
                    doseUnit: unit,
                    isDosageCheckExcluded: false,
                    isHazardousWaste: false,
                    rxNormCode: null,
                    vuid: null,
                    isActive: true,
                    inactivationDate: null,
                    ingredients: new List<DrugIngredient>(),
                    ndcCodes: string.IsNullOrEmpty(ndc) ? new List<NdcEntry>() : new List<NdcEntry>
                    {
                        new() { NdcCode = ndc, IsActive = true }
                    },
                    secondaryDrugClassCodes: new List<string>());

                result.ImportedRecords++;

                indexEntries.Add(new VaProductIndexEntry
                {
                    Ien = ien,
                    Name = name,
                    VaGenericIen = $"GEN-{ien}",
                    VaGenericName = genericName,
                    DosageFormName = dosageForm,
                    Strength = strength,
                    StrengthUnitName = unit,
                    PrimaryDrugClassCode = drugClassCode,
                    PrimaryDrugClassName = drugClassName,
                    FormularyIndicator = true,
                    IsActive = true,
                    NdcCodes = string.IsNullOrEmpty(ndc) ? new List<string>() : new List<string> { ndc }
                });
            }
            catch (Exception ex)
            {
                result.ErrorRecords++;
                result.Errors.Add($"Line {result.TotalRecords}: {ex.Message}");
            }

            if (indexEntries.Count >= BatchSize)
            {
                await FlushNdfIndexAsync(grainFactory, indexEntries);
            }
        }

        if (indexEntries.Count > 0)
        {
            await FlushNdfIndexAsync(grainFactory, indexEntries);
        }

        sw.Stop();
        result.Duration = sw.Elapsed;

        _logger.LogInformation(
            "NDF import complete: {Total} total, {Imported} imported, {Skipped} skipped, {Errors} errors in {Duration}",
            result.TotalRecords, result.ImportedRecords, result.SkippedRecords, result.ErrorRecords, result.Duration);

        return result;
    }

    public async Task<ImportResult> ImportCptCodesAsync(
        IGrainFactory grainFactory, Stream csvStream, bool idempotent = true)
    {
        Stopwatch sw = Stopwatch.StartNew();
        ImportResult result = new();
        List<CptCodeIndexEntry> indexEntries = new();

        await foreach (string[] fields in ReadCsvAsync(csvStream))
        {
            result.TotalRecords++;

            if (fields.Length < 4)
            {
                result.ErrorRecords++;
                result.Errors.Add($"Line {result.TotalRecords}: Expected at least 4 columns (Code, ShortName, LongDescription, Category), got {fields.Length}");
                continue;
            }

            try
            {
                string code = fields[0].Trim();
                string shortName = fields[1].Trim();
                string longDescription = fields[2].Trim();
                string category = fields[3].Trim();

                if (string.IsNullOrEmpty(code))
                {
                    result.ErrorRecords++;
                    result.Errors.Add($"Line {result.TotalRecords}: Empty code");
                    continue;
                }

                string grainKey = $"CPT:{code}";
                ICptCodeGrain grain = grainFactory.GetGrain<ICptCodeGrain>(grainKey);

                if (idempotent)
                {
                    CptCodeState existing = await grain.GetCodeAsync();
                    if (!string.IsNullOrEmpty(existing.ShortName))
                    {
                        result.SkippedRecords++;
                        continue;
                    }
                }

                await grain.LoadCodeAsync(code, shortName, longDescription, category,
                    0m, 0m, "ACTIVE", null);
                result.ImportedRecords++;

                indexEntries.Add(new CptCodeIndexEntry
                {
                    Code = code,
                    ShortName = shortName,
                    LongDescription = longDescription,
                    Category = category,
                    Status = "ACTIVE"
                });
            }
            catch (Exception ex)
            {
                result.ErrorRecords++;
                result.Errors.Add($"Line {result.TotalRecords}: {ex.Message}");
            }

            if (indexEntries.Count >= BatchSize)
            {
                await FlushCptIndexAsync(grainFactory, indexEntries);
            }
        }

        if (indexEntries.Count > 0)
        {
            await FlushCptIndexAsync(grainFactory, indexEntries);
        }

        sw.Stop();
        result.Duration = sw.Elapsed;

        _logger.LogInformation(
            "CPT import complete: {Total} total, {Imported} imported, {Skipped} skipped, {Errors} errors in {Duration}",
            result.TotalRecords, result.ImportedRecords, result.SkippedRecords, result.ErrorRecords, result.Duration);

        return result;
    }

    public async Task<ImportResult> ImportLoincCodesAsync(
        IGrainFactory grainFactory, Stream csvStream, bool idempotent = true)
    {
        Stopwatch sw = Stopwatch.StartNew();
        ImportResult result = new();
        List<LoincCodeIndexEntry> indexEntries = new();

        await foreach (string[] fields in ReadCsvAsync(csvStream))
        {
            result.TotalRecords++;

            if (fields.Length < 11)
            {
                result.ErrorRecords++;
                result.Errors.Add($"Line {result.TotalRecords}: Expected 11 columns, got {fields.Length}");
                continue;
            }

            try
            {
                string loincNum = fields[0].Trim();
                string component = fields[1].Trim();
                string property = fields[2].Trim();
                string timeAspect = fields[3].Trim();
                string system = fields[4].Trim();
                string scale = fields[5].Trim();
                string method = fields[6].Trim();
                string shortName = fields[7].Trim();
                string longCommonName = fields[8].Trim();
                string status = fields[9].Trim();
                string classType = fields[10].Trim();

                if (string.IsNullOrEmpty(loincNum))
                {
                    result.ErrorRecords++;
                    result.Errors.Add($"Line {result.TotalRecords}: Empty LOINC number");
                    continue;
                }

                string grainKey = $"LOINC:{loincNum}";
                ILoincCodeGrain grain = grainFactory.GetGrain<ILoincCodeGrain>(grainKey);

                if (idempotent)
                {
                    LoincCodeState existing = await grain.GetCodeAsync();
                    if (!string.IsNullOrEmpty(existing.Component))
                    {
                        result.SkippedRecords++;
                        continue;
                    }
                }

                await grain.LoadCodeAsync(loincNum, component, property, timeAspect,
                    system, scale, method, shortName, longCommonName, status, classType);
                result.ImportedRecords++;

                indexEntries.Add(new LoincCodeIndexEntry
                {
                    Code = loincNum,
                    Component = component,
                    Property = property,
                    TimeAspect = timeAspect,
                    System = system,
                    Scale = scale,
                    Method = method,
                    ShortName = shortName,
                    LongCommonName = longCommonName,
                    Status = status,
                    ClassType = classType
                });
            }
            catch (Exception ex)
            {
                result.ErrorRecords++;
                result.Errors.Add($"Line {result.TotalRecords}: {ex.Message}");
            }

            if (indexEntries.Count >= BatchSize)
            {
                await FlushLoincIndexAsync(grainFactory, indexEntries);
            }
        }

        if (indexEntries.Count > 0)
        {
            await FlushLoincIndexAsync(grainFactory, indexEntries);
        }

        sw.Stop();
        result.Duration = sw.Elapsed;

        _logger.LogInformation(
            "LOINC import complete: {Total} total, {Imported} imported, {Skipped} skipped, {Errors} errors in {Duration}",
            result.TotalRecords, result.ImportedRecords, result.SkippedRecords, result.ErrorRecords, result.Duration);

        return result;
    }

    // ─── Index flush helpers ─────────────────────────────────────────────────

    private static async Task FlushIcd10IndexAsync(IGrainFactory grainFactory, List<Icd10IndexEntry> entries)
    {
        IIcd10IndexGrain index = grainFactory.GetGrain<IIcd10IndexGrain>("ICD10-INDEX");
        await index.LoadCodesAsync(new List<Icd10IndexEntry>(entries));
        entries.Clear();
    }

    private static async Task FlushNdfIndexAsync(IGrainFactory grainFactory, List<VaProductIndexEntry> entries)
    {
        IVaProductIndexGrain index = grainFactory.GetGrain<IVaProductIndexGrain>("NDF-PRODUCT-INDEX");
        await index.LoadProductsAsync(new List<VaProductIndexEntry>(entries));
        entries.Clear();
    }

    private static async Task FlushCptIndexAsync(IGrainFactory grainFactory, List<CptCodeIndexEntry> entries)
    {
        ICptCodeIndexGrain index = grainFactory.GetGrain<ICptCodeIndexGrain>("CPT-INDEX");
        await index.LoadCodesAsync(new List<CptCodeIndexEntry>(entries));
        entries.Clear();
    }

    private static async Task FlushLoincIndexAsync(IGrainFactory grainFactory, List<LoincCodeIndexEntry> entries)
    {
        ILoincCodeIndexGrain index = grainFactory.GetGrain<ILoincCodeIndexGrain>("LOINC-INDEX");
        await index.LoadCodesAsync(new List<LoincCodeIndexEntry>(entries));
        entries.Clear();
    }

    // ─── CSV parsing ─────────────────────────────────────────────────────────

    /// <summary>
    /// Reads a CSV stream line by line, yielding parsed field arrays.
    /// Skips the header row. Handles quoted fields containing commas.
    /// </summary>
    private static async IAsyncEnumerable<string[]> ReadCsvAsync(Stream stream)
    {
        using StreamReader reader = new(stream);
        bool isHeader = true;

        while (await reader.ReadLineAsync() is { } line)
        {
            if (isHeader)
            {
                isHeader = false;
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
                continue;

            yield return ParseCsvLine(line);
        }
    }

    /// <summary>
    /// Parses a single CSV line handling quoted fields.
    /// </summary>
    private static string[] ParseCsvLine(string line)
    {
        List<string> fields = new();
        int i = 0;

        while (i < line.Length)
        {
            if (line[i] == '"')
            {
                // Quoted field
                i++;
                int start = i;
                while (i < line.Length)
                {
                    if (line[i] == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            i += 2; // Escaped quote
                        }
                        else
                        {
                            break;
                        }
                    }
                    else
                    {
                        i++;
                    }
                }

                fields.Add(line[start..i].Replace("\"\"", "\""));
                i++; // Skip closing quote
                if (i < line.Length && line[i] == ',')
                    i++; // Skip comma
            }
            else
            {
                // Unquoted field
                int start = i;
                while (i < line.Length && line[i] != ',')
                    i++;

                fields.Add(line[start..i]);
                if (i < line.Length)
                    i++; // Skip comma
            }
        }

        return fields.ToArray();
    }
}
