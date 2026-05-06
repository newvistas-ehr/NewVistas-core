// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Lexicon Utility Controller — unified clinical terminology search.
/// Maps to VistA Lexicon Utility (File #757, LEX*.m routines).
///
/// Provides unified search across SNOMED CT, ICD-10-CM, CPT, and LOINC
/// vocabularies. Used by clinical UIs for coded entry (problem list,
/// encounter diagnosis, procedure coding, lab ordering).
/// </summary>
[Authorize]
[ApiController]
[Route("api/lexicon")]
[Produces("application/json")]
public class LexiconController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<LexiconController> _logger;

    public LexiconController(IGrainFactory grainFactory, ILogger<LexiconController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private ILexiconSearchGrain GetSearchGrain() =>
        _grainFactory.GetGrain<ILexiconSearchGrain>("LEX-INDEX");

    // ─── Search ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Unified terminology search across all coding systems.
    /// Mirrors LEX^LEX10 (Lexicon lookup).
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] string? system,
        [FromQuery] int maxResults = 25)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(q))
                return Ok(new List<LexiconIndexEntry>());

            List<LexiconIndexEntry> results = await GetSearchGrain().SearchAsync(q, system, maxResults);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching lexicon for {Query}", q);
            return StatusCode(500, "Error searching lexicon.");
        }
    }

    /// <summary>
    /// Lookup a specific code in a specific coding system.
    /// </summary>
    [HttpGet("lookup/{codingSystem}/{code}")]
    public async Task<IActionResult> LookupCode(string codingSystem, string code)
    {
        try
        {
            LexiconIndexEntry? entry = await GetSearchGrain().LookupCodeAsync(code, codingSystem);
            if (entry == null)
                return NotFound(new { message = $"Code {code} not found in {codingSystem}" });

            return Ok(entry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error looking up {CodingSystem}:{Code}", codingSystem, code);
            return StatusCode(500, "Error looking up code.");
        }
    }

    /// <summary>
    /// Get all terms for a specific coding system.
    /// </summary>
    [HttpGet("system/{codingSystem}")]
    public async Task<IActionResult> GetByCodingSystem(string codingSystem, [FromQuery] int maxResults = 50)
    {
        try
        {
            List<LexiconIndexEntry> results = await GetSearchGrain().GetByCodingSystemAsync(codingSystem, maxResults);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting terms for {CodingSystem}", codingSystem);
            return StatusCode(500, "Error retrieving terms.");
        }
    }

    /// <summary>
    /// Get index status and statistics.
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        try
        {
            LexiconIndexStatus status = await GetSearchGrain().GetStatusAsync();
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting lexicon status");
            return StatusCode(500, "Error retrieving lexicon status.");
        }
    }

    /// <summary>
    /// Get a single term's full detail.
    /// </summary>
    [HttpGet("term/{codingSystem}/{code}")]
    public async Task<IActionResult> GetTerm(string codingSystem, string code)
    {
        try
        {
            ILexiconTermGrain grain = _grainFactory.GetGrain<ILexiconTermGrain>($"LEX:{codingSystem}:{code}");
            LexiconTermState state = await grain.GetTermAsync();
            if (string.IsNullOrEmpty(state.Code))
                return NotFound(new { message = $"Term {codingSystem}:{code} not found" });

            return Ok(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting term {CodingSystem}:{Code}", codingSystem, code);
            return StatusCode(500, "Error retrieving term.");
        }
    }

    // ─── Bulk Load ────────────────────────────────────────────────────────────

    /// <summary>
    /// Bulk load terms into the lexicon index.
    /// </summary>
    [HttpPost("load")]
    public async Task<IActionResult> LoadTerms([FromBody] List<LexLoadTermRequest> terms)
    {
        try
        {
            List<LexiconIndexEntry> entries = terms.Select(t => new LexiconIndexEntry
            {
                Code = t.Code,
                CodingSystem = t.CodingSystem,
                Designation = t.Designation,
                FullySpecifiedName = t.FullySpecifiedName,
                IsActive = t.IsActive,
                SemanticType = t.SemanticType
            }).ToList();

            await GetSearchGrain().LoadTermsAsync(entries);
            return Ok(new { loaded = entries.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading lexicon terms");
            return StatusCode(500, "Error loading terms.");
        }
    }

    /// <summary>
    /// Seed demo data with ~100 common clinical terms.
    /// </summary>
    [HttpPost("demo/load")]
    public async Task<IActionResult> SeedDemoData()
    {
        try
        {
            await GetSearchGrain().SeedDemoDataAsync();
            LexiconIndexStatus status = await GetSearchGrain().GetStatusAsync();
            return Ok(new { message = "Demo data loaded", totalTerms = status.TotalTerms });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding lexicon demo data");
            return StatusCode(500, "Error seeding demo data.");
        }
    }
}

// ─── Request DTOs ─────────────────────────────────────────────────────────────

public record LexLoadTermRequest(
    string Code,
    string CodingSystem,
    string Designation,
    string? FullySpecifiedName,
    bool IsActive,
    string? SemanticType);
