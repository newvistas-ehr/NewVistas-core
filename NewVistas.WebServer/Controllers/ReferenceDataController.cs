// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.Abstractions.Services;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Reference Data Bulk Import API — load, search, and retrieve standardized
/// code sets (ICD-10, CPT, LOINC, NDF) used across the clinical system.
///
/// This replaces VistA's KIDS (Kernel Installation and Distribution System)
/// for installing reference data patches. Supports:
///   - CSV file upload for each code set
///   - Built-in sample data seeding
///   - Search and lookup for CPT and LOINC codes
/// </summary>
[Authorize]
[ApiController]
[Route("api/referencedata")]
[Produces("application/json")]
public class ReferenceDataController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<ReferenceDataController> _logger;
    private readonly IWebHostEnvironment _environment;

    private const string CptIndexKey = "CPT-INDEX";
    private const string LoincIndexKey = "LOINC-INDEX";

    public ReferenceDataController(
        IGrainFactory grainFactory,
        ILogger<ReferenceDataController> logger,
        IWebHostEnvironment environment)
    {
        _grainFactory = grainFactory;
        _logger = logger;
        _environment = environment;
    }

    // ─── Import endpoints ────────────────────────────────────────────────────

    /// <summary>
    /// Upload and import ICD-10-CM codes from a CSV file.
    /// Expected columns: Code, ShortDescription, LongDescription, Category
    /// </summary>
    [HttpPost("import/icd10")]
    [ProducesResponseType(typeof(ImportResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ImportIcd10(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { Message = "No file provided" });

            ReferenceDataImportService importService = CreateImportService();
            using Stream stream = file.OpenReadStream();
            ImportResult result = await importService.ImportIcd10CodesAsync(_grainFactory, stream);

            _logger.LogInformation("ICD-10 import: {Imported}/{Total} records", result.ImportedRecords, result.TotalRecords);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing ICD-10 codes");
            return StatusCode(500, "An error occurred while importing ICD-10 codes");
        }
    }

    /// <summary>
    /// Upload and import VA drug products from a CSV file.
    /// Expected columns: IEN, VaProductName, GenericName, DrugClassCode, DrugClassName, NationalDrugCode, Strength, DosageForm, Route, Unit
    /// </summary>
    [HttpPost("import/drugs")]
    [ProducesResponseType(typeof(ImportResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ImportDrugs(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { Message = "No file provided" });

            ReferenceDataImportService importService = CreateImportService();
            using Stream stream = file.OpenReadStream();
            ImportResult result = await importService.ImportDrugProductsAsync(_grainFactory, stream);

            _logger.LogInformation("NDF import: {Imported}/{Total} records", result.ImportedRecords, result.TotalRecords);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing drug products");
            return StatusCode(500, "An error occurred while importing drug products");
        }
    }

    /// <summary>
    /// Upload and import CPT procedure codes from a CSV file.
    /// Expected columns: Code, ShortName, LongDescription, Category
    /// </summary>
    [HttpPost("import/cpt")]
    [ProducesResponseType(typeof(ImportResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ImportCpt(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { Message = "No file provided" });

            ReferenceDataImportService importService = CreateImportService();
            using Stream stream = file.OpenReadStream();
            ImportResult result = await importService.ImportCptCodesAsync(_grainFactory, stream);

            _logger.LogInformation("CPT import: {Imported}/{Total} records", result.ImportedRecords, result.TotalRecords);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing CPT codes");
            return StatusCode(500, "An error occurred while importing CPT codes");
        }
    }

    /// <summary>
    /// Upload and import LOINC observation codes from a CSV file.
    /// Expected columns: LOINC_NUM, COMPONENT, PROPERTY, TIME_ASPCT, SYSTEM, SCALE_TYP, METHOD_TYP, SHORTNAME, LONG_COMMON_NAME, STATUS, CLASSTYPE
    /// </summary>
    [HttpPost("import/loinc")]
    [ProducesResponseType(typeof(ImportResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ImportLoinc(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { Message = "No file provided" });

            ReferenceDataImportService importService = CreateImportService();
            using Stream stream = file.OpenReadStream();
            ImportResult result = await importService.ImportLoincCodesAsync(_grainFactory, stream);

            _logger.LogInformation("LOINC import: {Imported}/{Total} records", result.ImportedRecords, result.TotalRecords);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing LOINC codes");
            return StatusCode(500, "An error occurred while importing LOINC codes");
        }
    }

    /// <summary>
    /// Seed all reference data from built-in sample CSV files.
    /// Loads ICD-10, CPT, LOINC, and NDF sample data without requiring file upload.
    /// </summary>
    [HttpPost("seed")]
    [ProducesResponseType(typeof(RefDataSeedResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> SeedAll()
    {
        try
        {
            RefDataSeedResult seedResult = new();
            ReferenceDataImportService importService = CreateImportService();
            string seedPath = Path.Combine(_environment.ContentRootPath, "SeedData");

            // ICD-10
            string icd10Path = Path.Combine(seedPath, "icd10-sample.csv");
            if (System.IO.File.Exists(icd10Path))
            {
                using FileStream stream = System.IO.File.OpenRead(icd10Path);
                seedResult.Icd10 = await importService.ImportIcd10CodesAsync(_grainFactory, stream);
            }

            // CPT
            string cptPath = Path.Combine(seedPath, "cpt-sample.csv");
            if (System.IO.File.Exists(cptPath))
            {
                using FileStream stream = System.IO.File.OpenRead(cptPath);
                seedResult.Cpt = await importService.ImportCptCodesAsync(_grainFactory, stream);
            }

            // LOINC
            string loincPath = Path.Combine(seedPath, "loinc-sample.csv");
            if (System.IO.File.Exists(loincPath))
            {
                using FileStream stream = System.IO.File.OpenRead(loincPath);
                seedResult.Loinc = await importService.ImportLoincCodesAsync(_grainFactory, stream);
            }

            // NDF
            string ndfPath = Path.Combine(seedPath, "ndf-sample.csv");
            if (System.IO.File.Exists(ndfPath))
            {
                using FileStream stream = System.IO.File.OpenRead(ndfPath);
                seedResult.Ndf = await importService.ImportDrugProductsAsync(_grainFactory, stream);
            }

            _logger.LogInformation("Reference data seed complete");
            return Ok(seedResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding reference data");
            return StatusCode(500, "An error occurred while seeding reference data");
        }
    }

    // ─── CPT lookup endpoints ────────────────────────────────────────────────

    /// <summary>
    /// Get a specific CPT code by its code value.
    /// </summary>
    [HttpGet("cpt/{code}")]
    [ProducesResponseType(typeof(CptCodeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CptCodeDto>> GetCptCode(string code)
    {
        try
        {
            ICptCodeIndexGrain index = _grainFactory.GetGrain<ICptCodeIndexGrain>(CptIndexKey);
            CptCodeIndexEntry? entry = await index.GetCodeAsync(code);

            if (entry == null)
                return NotFound(new { Message = $"CPT code '{code}' not found" });

            return Ok(MapCptDto(entry));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving CPT code '{Code}'", code);
            return StatusCode(500, "An error occurred while retrieving the CPT code");
        }
    }

    /// <summary>
    /// Search CPT codes by code prefix or description text.
    /// </summary>
    [HttpGet("cpt/search")]
    [ProducesResponseType(typeof(List<CptCodeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CptCodeDto>>> SearchCpt(
        [FromQuery] string term = "",
        [FromQuery] int maxResults = 50)
    {
        try
        {
            ICptCodeIndexGrain index = _grainFactory.GetGrain<ICptCodeIndexGrain>(CptIndexKey);
            List<CptCodeIndexEntry> results = await index.SearchAsync(term, maxResults);

            return Ok(results.Select(MapCptDto).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching CPT codes for '{Term}'", term);
            return StatusCode(500, "An error occurred while searching CPT codes");
        }
    }

    // ─── LOINC lookup endpoints ──────────────────────────────────────────────

    /// <summary>
    /// Get a specific LOINC code by its code value.
    /// </summary>
    [HttpGet("loinc/{code}")]
    [ProducesResponseType(typeof(LoincCodeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LoincCodeDto>> GetLoincCode(string code)
    {
        try
        {
            ILoincCodeIndexGrain index = _grainFactory.GetGrain<ILoincCodeIndexGrain>(LoincIndexKey);
            LoincCodeIndexEntry? entry = await index.GetCodeAsync(code);

            if (entry == null)
                return NotFound(new { Message = $"LOINC code '{code}' not found" });

            return Ok(MapLoincDto(entry));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving LOINC code '{Code}'", code);
            return StatusCode(500, "An error occurred while retrieving the LOINC code");
        }
    }

    /// <summary>
    /// Search LOINC codes by component, short name, or long common name.
    /// </summary>
    [HttpGet("loinc/search")]
    [ProducesResponseType(typeof(List<LoincCodeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<LoincCodeDto>>> SearchLoinc(
        [FromQuery] string term = "",
        [FromQuery] int maxResults = 50)
    {
        try
        {
            ILoincCodeIndexGrain index = _grainFactory.GetGrain<ILoincCodeIndexGrain>(LoincIndexKey);
            List<LoincCodeIndexEntry> results = await index.SearchAsync(term, maxResults);

            return Ok(results.Select(MapLoincDto).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching LOINC codes for '{Term}'", term);
            return StatusCode(500, "An error occurred while searching LOINC codes");
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private ReferenceDataImportService CreateImportService()
    {
        ILogger<ReferenceDataImportService> importLogger =
            HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger<ReferenceDataImportService>();
        return new ReferenceDataImportService(importLogger);
    }

    private static CptCodeDto MapCptDto(CptCodeIndexEntry entry) => new()
    {
        Code = entry.Code,
        ShortName = entry.ShortName,
        LongDescription = entry.LongDescription,
        Category = entry.Category,
        WorkRvu = entry.WorkRvu,
        TotalRvu = entry.TotalRvu,
        Status = entry.Status
    };

    private static LoincCodeDto MapLoincDto(LoincCodeIndexEntry entry) => new()
    {
        Code = entry.Code,
        Component = entry.Component,
        Property = entry.Property,
        TimeAspect = entry.TimeAspect,
        System = entry.System,
        Scale = entry.Scale,
        Method = entry.Method,
        ShortName = entry.ShortName,
        LongCommonName = entry.LongCommonName,
        Status = entry.Status,
        ClassType = entry.ClassType
    };
}

/// <summary>
/// Result of seeding all reference data code sets.
/// </summary>
public class RefDataSeedResult
{
    public ImportResult? Icd10 { get; set; }
    public ImportResult? Cpt { get; set; }
    public ImportResult? Loinc { get; set; }
    public ImportResult? Ndf { get; set; }
}

/// <summary>
/// CPT code response DTO.
/// </summary>
public class CptCodeDto
{
    public string Code { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string LongDescription { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal WorkRvu { get; set; }
    public decimal TotalRvu { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// LOINC code response DTO.
/// </summary>
public class LoincCodeDto
{
    public string Code { get; set; } = string.Empty;
    public string Component { get; set; } = string.Empty;
    public string Property { get; set; } = string.Empty;
    public string TimeAspect { get; set; } = string.Empty;
    public string System { get; set; } = string.Empty;
    public string Scale { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string LongCommonName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string ClassType { get; set; } = string.Empty;
}
