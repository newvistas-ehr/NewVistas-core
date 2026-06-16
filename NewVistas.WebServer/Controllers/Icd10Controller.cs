// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at https://mozilla.org/MPL/2.0/.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// ICD-10-CM Diagnosis Code API — load, search, and retrieve ICD-10 codes.
///
/// Mirrors VistA ICD DIAGNOSIS file (#80) lookups:
///   - Code lookup  → $$ICDDX^ICDCODE / ^ICD9 "B" cross-reference
///   - Text search  → DGICD.m LEX / $$ICDDATA^ICDXCODE
///   - Bulk load    → equivalent to KIDS install of ICD-10 patch
///
/// The CMS ICD-10-CM order file (icd10cm-order-2023.txt) is a fixed-width
/// format distributed by CMS at:
///   https://www.cms.gov/medicare/coding-billing/icd-10-codes
/// </summary>
[Authorize]
[ApiController]
[Route("api/icd10")]
[Produces("application/json")]
public class Icd10Controller : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<Icd10Controller> _logger;
    private readonly IWebHostEnvironment _environment;

    private const string IndexGrainKey = "ICD10-INDEX";

    public Icd10Controller(
        IGrainFactory grainFactory,
        ILogger<Icd10Controller> logger,
        IWebHostEnvironment environment)
    {
        _grainFactory = grainFactory;
        _logger = logger;
        _environment = environment;
    }

    private IIcd10IndexGrain GetIndex() =>
        _grainFactory.GetGrain<IIcd10IndexGrain>(IndexGrainKey);

    /// <summary>
    /// Load ICD-10-CM codes from the bundled CMS order file (icd10cm-order-2023.txt).
    /// This parses the fixed-width file and bulk-loads all codes into the index grain.
    /// Equivalent to installing an ICD-10 update patch in VistA via KIDS.
    /// </summary>
    /// <returns>Load statistics</returns>
    /// <response code="200">Codes loaded successfully</response>
    /// <response code="404">CMS order file not found in the application directory</response>
    /// <response code="500">Error during loading</response>
    [HttpPost("load")]
    [ProducesResponseType(typeof(Icd10LoadResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LoadCodes()
    {
        try
        {
            var filePath = Path.Combine(_environment.ContentRootPath, "icd10cm-order-2023.txt");
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new { Message = "ICD-10-CM order file not found. Expected: icd10cm-order-2023.txt" });
            }

            _logger.LogInformation("Loading ICD-10-CM codes from {FilePath}", filePath);

            var entries = new List<Icd10IndexEntry>();
            var lines = await System.IO.File.ReadAllLinesAsync(filePath);

            foreach (var line in lines)
            {
                if (line.Length < 17)
                    continue;

                var entry = ParseOrderLine(line);
                if (entry != null)
                    entries.Add(entry);
            }

            var index = GetIndex();
            await index.LoadCodesAsync(entries);

            var status = await index.GetStatusAsync();

            _logger.LogInformation(
                "Loaded {Total} ICD-10-CM codes ({Billable} billable)",
                status.TotalCodes, status.BillableCodes);

            return Ok(new Icd10LoadResult
            {
                TotalCodesLoaded = status.TotalCodes,
                BillableCodes = status.BillableCodes,
                HeaderCodes = status.TotalCodes - status.BillableCodes,
                LoadedAt = status.LastLoadedDate ?? DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading ICD-10-CM codes");
            return StatusCode(500, "An error occurred while loading ICD-10-CM codes");
        }
    }

    /// <summary>
    /// Upload and load ICD-10-CM codes from a CMS order file.
    /// Use this to load a different year's code set.
    /// </summary>
    /// <param name="file">CMS ICD-10-CM order file (fixed-width format)</param>
    /// <returns>Load statistics</returns>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(Icd10LoadResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadCodes(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { Message = "No file provided" });

            var entries = new List<Icd10IndexEntry>();
            using var reader = new StreamReader(file.OpenReadStream());

            while (await reader.ReadLineAsync() is { } line)
            {
                if (line.Length < 17)
                    continue;

                var entry = ParseOrderLine(line);
                if (entry != null)
                    entries.Add(entry);
            }

            var index = GetIndex();
            await index.LoadCodesAsync(entries);

            var status = await index.GetStatusAsync();

            _logger.LogInformation(
                "Uploaded {Total} ICD-10-CM codes ({Billable} billable) from {FileName}",
                status.TotalCodes, status.BillableCodes, file.FileName);

            return Ok(new Icd10LoadResult
            {
                TotalCodesLoaded = status.TotalCodes,
                BillableCodes = status.BillableCodes,
                HeaderCodes = status.TotalCodes - status.BillableCodes,
                LoadedAt = status.LastLoadedDate ?? DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading ICD-10-CM codes");
            return StatusCode(500, "An error occurred while uploading ICD-10-CM codes");
        }
    }

    /// <summary>
    /// Search ICD-10-CM codes by code or description text.
    /// If the search text starts with a letter followed by a digit, it is
    /// treated as a code prefix search (like VistA ^ICD9 "B" xref).
    /// Otherwise it searches descriptions (like VistA DGICD.m LEX lookup).
    /// </summary>
    /// <param name="q">Search text — code prefix (e.g., "M54") or description (e.g., "diabetes")</param>
    /// <param name="billableOnly">If true, returns only billable codes (excludes header/category codes)</param>
    /// <param name="maxResults">Maximum number of results to return (default 50)</param>
    /// <returns>Matching ICD-10 codes</returns>
    [HttpGet("search")]
    [ProducesResponseType(typeof(List<Icd10CodeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Icd10CodeDto>>> Search(
        [FromQuery] string q,
        [FromQuery] bool billableOnly = false,
        [FromQuery] int maxResults = 50)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(q))
                return Ok(new List<Icd10CodeDto>());

            var index = GetIndex();
            var results = await index.SearchAsync(q, billableOnly, maxResults);

            return Ok(results.Select(MapToDto).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching ICD-10-CM codes for '{Query}'", q);
            return StatusCode(500, "An error occurred while searching ICD-10-CM codes");
        }
    }

    /// <summary>
    /// Get a specific ICD-10-CM code by its code value.
    /// Mirrors $$ICDDX^ICDCODE.
    /// </summary>
    /// <param name="code">ICD-10-CM code (e.g., "A00.0", "M54.5")</param>
    /// <returns>Code details or 404 if not found</returns>
    [HttpGet("{code}")]
    [ProducesResponseType(typeof(Icd10CodeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Icd10CodeDto>> GetCode(string code)
    {
        try
        {
            var index = GetIndex();
            var entry = await index.GetCodeAsync(code.ToUpperInvariant());

            if (entry == null)
                return NotFound(new { Message = $"ICD-10-CM code '{code}' not found" });

            return Ok(MapToDto(entry));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ICD-10-CM code '{Code}'", code);
            return StatusCode(500, "An error occurred while retrieving the ICD-10-CM code");
        }
    }

    /// <summary>
    /// Get all ICD-10-CM codes within a code prefix/chapter.
    /// e.g., prefix "A0" returns A00-A09, prefix "M54" returns M54.x codes.
    /// </summary>
    /// <param name="prefix">Code prefix (e.g., "A0", "M54", "E11")</param>
    /// <param name="billableOnly">If true, returns only billable codes</param>
    /// <param name="maxResults">Maximum results (default 100)</param>
    [HttpGet("prefix/{prefix}")]
    [ProducesResponseType(typeof(List<Icd10CodeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<Icd10CodeDto>>> GetByPrefix(
        string prefix,
        [FromQuery] bool billableOnly = false,
        [FromQuery] int maxResults = 100)
    {
        try
        {
            var index = GetIndex();
            var results = await index.GetByPrefixAsync(prefix, billableOnly, maxResults);

            return Ok(results.Select(MapToDto).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ICD-10-CM codes for prefix '{Prefix}'", prefix);
            return StatusCode(500, "An error occurred while retrieving ICD-10-CM codes");
        }
    }

    /// <summary>
    /// Get the current status of the ICD-10-CM code index.
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(Icd10IndexStatus), StatusCodes.Status200OK)]
    public async Task<ActionResult<Icd10IndexStatus>> GetStatus()
    {
        try
        {
            var index = GetIndex();
            var status = await index.GetStatusAsync();
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving ICD-10 index status");
            return StatusCode(500, "An error occurred while retrieving index status");
        }
    }

    /// <summary>
    /// Parses a single line from the CMS ICD-10-CM order file (fixed-width format).
    ///
    /// Format:
    ///   Positions 1-5:   Order number
    ///   Position  6:     Space
    ///   Positions 7-13:  ICD-10 code (7 chars, right-padded with spaces)
    ///   Position  14:    Space
    ///   Position  15:    Header flag (0=header/category, 1=billable)
    ///   Position  16:    Space
    ///   Positions 17-76: Short description (60 chars)
    ///   Positions 77+:   Long description
    /// </summary>
    private static Icd10IndexEntry? ParseOrderLine(string line)
    {
        if (line.Length < 17)
            return null;

        var orderStr = line[..5].Trim();
        if (!int.TryParse(orderStr, out var orderNumber))
            return null;

        var rawCode = line.Substring(6, 7).Trim();
        var billableFlag = line[14];
        var shortDesc = line.Length >= 77
            ? line.Substring(16, 60).Trim()
            : line[16..].Trim();
        var longDesc = line.Length > 77
            ? line[77..].Trim()
            : shortDesc;

        // Format the code with a dot: "A001" → "A00.1", "M545" → "M54.5"
        // ICD-10-CM codes have the dot after the 3rd character
        var code = rawCode.Length > 3
            ? rawCode[..3] + "." + rawCode[3..]
            : rawCode;

        code = code.ToUpperInvariant();

        return new Icd10IndexEntry
        {
            Code = code,
            ShortDescription = shortDesc,
            LongDescription = longDesc,
            IsBillable = billableFlag == '1',
            OrderNumber = orderNumber,
            Chapter = GetChapter(code),
            IsActive = true
        };
    }

    /// <summary>
    /// Determines the ICD-10-CM chapter from the code's first character(s).
    /// </summary>
    private static string? GetChapter(string code)
    {
        if (string.IsNullOrEmpty(code))
            return null;

        return code[0] switch
        {
            'A' or 'B' => "A00-B99 Certain infectious and parasitic diseases",
            'C' => "C00-D49 Neoplasms",
            'D' when code.CompareTo("D50") >= 0 => "D50-D89 Blood and immune mechanism disorders",
            'D' => "C00-D49 Neoplasms",
            'E' => "E00-E89 Endocrine, nutritional and metabolic diseases",
            'F' => "F01-F99 Mental, behavioral and neurodevelopmental disorders",
            'G' => "G00-G99 Nervous system diseases",
            'H' when code.CompareTo("H60") >= 0 => "H60-H95 Ear and mastoid process diseases",
            'H' => "H00-H59 Eye and adnexa diseases",
            'I' => "I00-I99 Circulatory system diseases",
            'J' => "J00-J99 Respiratory system diseases",
            'K' => "K00-K95 Digestive system diseases",
            'L' => "L00-L99 Skin and subcutaneous tissue diseases",
            'M' => "M00-M99 Musculoskeletal and connective tissue diseases",
            'N' => "N00-N99 Genitourinary system diseases",
            'O' => "O00-O9A Pregnancy, childbirth and the puerperium",
            'P' => "P00-P96 Certain conditions originating in the perinatal period",
            'Q' => "Q00-Q99 Congenital malformations and chromosomal abnormalities",
            'R' => "R00-R99 Symptoms, signs and abnormal clinical findings",
            'S' or 'T' => "S00-T88 Injury, poisoning and external causes",
            'V' or 'W' or 'X' or 'Y' => "V00-Y99 External causes of morbidity",
            'Z' => "Z00-Z99 Factors influencing health status and contact with health services",
            _ => null
        };
    }

    private static Icd10CodeDto MapToDto(Icd10IndexEntry entry) => new()
    {
        Code = entry.Code,
        ShortDescription = entry.ShortDescription,
        LongDescription = entry.LongDescription,
        IsBillable = entry.IsBillable,
        IsActive = entry.IsActive,
        Chapter = entry.Chapter
    };
}

/// <summary>
/// Result of ICD-10-CM code loading operation.
/// </summary>
public class Icd10LoadResult
{
    public int TotalCodesLoaded { get; set; }
    public int BillableCodes { get; set; }
    public int HeaderCodes { get; set; }
    public DateTime LoadedAt { get; set; }
}

/// <summary>
/// ICD-10-CM code response DTO.
/// </summary>
public class Icd10CodeDto
{
    public string Code { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string LongDescription { get; set; } = string.Empty;
    public bool IsBillable { get; set; }
    public bool IsActive { get; set; }
    public string? Chapter { get; set; }
}
