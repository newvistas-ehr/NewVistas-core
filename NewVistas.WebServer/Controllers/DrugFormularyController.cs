// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.WebServer.Infrastructure;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Drug Formulary / National Drug File API — load, search, and retrieve
/// VA drug catalog data from NDF Files #50.6, #50.605, and #50.68.
///
/// This is pure reference data with no patient interaction. It mirrors:
///   - PSNLOOK.m  — drug lookup and display
///   - PSNCOMP.m  — NDF matching algorithms
///   - PSNAPIS.m  — programmatic API for drug information
///
/// The drug catalog is loaded from NDF global exports (.zwr files)
/// or programmatically via the load endpoints. Once loaded, searches
/// run entirely in-memory against the index grains.
/// </summary>
[Authorize]
[ApiController]
[Route("api/drugformulary")]
[Produces("application/json")]
public class DrugFormularyController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<DrugFormularyController> _logger;

    private const string ProductIndexKey = "NDF-PRODUCT-INDEX";
    private const string GenericIndexKey = "NDF-GENERIC-INDEX";
    private const string ClassIndexKey = "NDF-CLASS-INDEX";

    public DrugFormularyController(IGrainFactory grainFactory, ILogger<DrugFormularyController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    private IVaProductIndexGrain GetProductIndex() =>
        _grainFactory.GetGrain<IVaProductIndexGrain>(ProductIndexKey);

    private IVaGenericIndexGrain GetGenericIndex() =>
        _grainFactory.GetGrain<IVaGenericIndexGrain>(GenericIndexKey);

    private IDrugClassIndexGrain GetClassIndex() =>
        _grainFactory.GetGrain<IDrugClassIndexGrain>(ClassIndexKey);

    private IVaProductGrain GetProductGrain(string ien) =>
        _grainFactory.GetGrain<IVaProductGrain>(ien);

    // ─── PRODUCT SEARCH ────────────────────────────────────────────────────────

    /// <summary>
    /// Search VA Products by name, generic name, or NDC code.
    ///
    /// If the search text matches an NDC pattern (digits and hyphens),
    /// performs an NDC-to-product lookup. Otherwise performs a text search
    /// against product name and generic name.
    ///
    /// Mirrors PSNLOOK.m drug selection and PSNCOMP.m matching strategies.
    /// </summary>
    /// <param name="q">Search text — product name, generic name, or NDC code</param>
    /// <param name="formularyOnly">If true, returns only VA National Formulary products</param>
    /// <param name="classCode">Filter by VA drug class code (e.g., "AM100")</param>
    /// <param name="activeOnly">If true, returns only active products (default true)</param>
    /// <param name="maxResults">Maximum results (default 50)</param>
    [HttpGet("search")]
    [ProducesResponseType(typeof(List<VaProductSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<VaProductSummaryDto>>> Search(
        [FromQuery] string q,
        [FromQuery] bool formularyOnly = false,
        [FromQuery] string? classCode = null,
        [FromQuery] bool activeOnly = true,
        [FromQuery] int maxResults = 50)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(q))
                return Ok(new List<VaProductSummaryDto>());

            List<VaProductIndexEntry> results = await GetProductIndex()
                .SearchAsync(q, formularyOnly, classCode, activeOnly, maxResults);

            return Ok(results.Select(MapToSummaryDto).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching drug formulary for '{Query}'", q);
            return StatusCode(500, "An error occurred while searching the drug formulary");
        }
    }

    // ─── PRODUCT DETAIL ────────────────────────────────────────────────────────

    /// <summary>
    /// Get a VA Product summary from the index by its VistA IEN.
    /// Returns the lightweight index entry without activating the full grain.
    /// </summary>
    /// <param name="ien">VistA IEN from File #50.68</param>
    [HttpGet("products/{ien}")]
    [ProducesResponseType(typeof(VaProductSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VaProductSummaryDto>> GetProduct(string ien)
    {
        try
        {
            VaProductIndexEntry? entry = await GetProductIndex().GetProductAsync(ien);
            if (entry == null)
                return NotFound(new { Message = $"VA Product IEN '{ien}' not found" });

            return Ok(MapToSummaryDto(entry));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving VA Product IEN '{Ien}'", ien);
            return StatusCode(500, "An error occurred while retrieving the VA Product");
        }
    }

    /// <summary>
    /// Get full VA Product detail including ingredients, NDC codes, and dosing limits.
    /// Activates the individual VaProductGrain to retrieve the complete state.
    /// </summary>
    /// <param name="ien">VistA IEN from File #50.68</param>
    [HttpGet("products/{ien}/detail")]
    [ProducesResponseType(typeof(VaProductDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VaProductDetailDto>> GetProductDetail(string ien)
    {
        try
        {
            VaProductState product = await GetProductGrain(ien).GetProductAsync();
            if (string.IsNullOrEmpty(product.Name))
                return NotFound(new { Message = $"VA Product IEN '{ien}' not found" });

            return Ok(MapToDetailDto(product));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving VA Product detail for IEN '{Ien}'", ien);
            return StatusCode(500, "An error occurred while retrieving the VA Product detail");
        }
    }

    /// <summary>
    /// Get a VA Product by NDC (National Drug Code).
    /// Mirrors ^PSNDF(50.67,"NDC") cross-reference lookup.
    /// </summary>
    /// <param name="ndc">NDC code (e.g., "0002-7232-50")</param>
    [HttpGet("products/ndc/{ndc}")]
    [ProducesResponseType(typeof(VaProductSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VaProductSummaryDto>> GetByNdc(string ndc)
    {
        try
        {
            VaProductIndexEntry? entry = await GetProductIndex().GetByNdcAsync(ndc);
            if (entry == null)
                return NotFound(new { Message = $"No VA Product found for NDC '{ndc}'" });

            return Ok(MapToSummaryDto(entry));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving VA Product for NDC '{Ndc}'", ndc);
            return StatusCode(500, "An error occurred while looking up the NDC");
        }
    }

    /// <summary>
    /// Get all VA Products associated with a VA Generic drug IEN.
    /// Mirrors VAP^PSNAPIS — all formulations of a generic drug.
    /// </summary>
    /// <param name="genericIen">VA Generic IEN from File #50.6</param>
    /// <param name="maxResults">Maximum results (default 100)</param>
    [HttpGet("generics/{genericIen}/products")]
    [ProducesResponseType(typeof(List<VaProductSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<VaProductSummaryDto>>> GetProductsByGeneric(
        string genericIen,
        [FromQuery] int maxResults = 100)
    {
        try
        {
            List<VaProductIndexEntry> results = await GetProductIndex()
                .GetByGenericAsync(genericIen, maxResults);
            return Ok(results.Select(MapToSummaryDto).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving products for generic IEN '{GenericIen}'", genericIen);
            return StatusCode(500, "An error occurred while retrieving products for the generic drug");
        }
    }

    /// <summary>
    /// Get all VA Products in a given drug class.
    /// </summary>
    /// <param name="classCode">VA drug class code (e.g., "AM100")</param>
    /// <param name="activeOnly">If true, return only active products (default true)</param>
    /// <param name="maxResults">Maximum results (default 200)</param>
    [HttpGet("classes/{classCode}/products")]
    [ProducesResponseType(typeof(List<VaProductSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<VaProductSummaryDto>>> GetProductsByClass(
        string classCode,
        [FromQuery] bool activeOnly = true,
        [FromQuery] int maxResults = 200)
    {
        try
        {
            List<VaProductIndexEntry> results = await GetProductIndex()
                .GetByDrugClassAsync(classCode, activeOnly, maxResults);
            return Ok(results.Select(MapToSummaryDto).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving products for class '{ClassCode}'", classCode);
            return StatusCode(500, "An error occurred while retrieving products for the drug class");
        }
    }

    // ─── GENERICS ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Search VA Generic drug names.
    /// Mirrors $$VAGN^PSNAPIS and PSNLOOK.m generic browsing.
    /// </summary>
    /// <param name="q">Search text</param>
    /// <param name="activeOnly">Return only active generics (default true)</param>
    /// <param name="maxResults">Maximum results (default 50)</param>
    [HttpGet("generics/search")]
    [ProducesResponseType(typeof(List<VaGenericDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<VaGenericDto>>> SearchGenerics(
        [FromQuery] string q,
        [FromQuery] bool activeOnly = true,
        [FromQuery] int maxResults = 50)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(q))
                return Ok(new List<VaGenericDto>());

            List<VaGenericEntry> results = await GetGenericIndex()
                .SearchAsync(q, activeOnly, maxResults);

            return Ok(results.Select(MapToGenericDto).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching generics for '{Query}'", q);
            return StatusCode(500, "An error occurred while searching generic drug names");
        }
    }

    // ─── DRUG CLASSES ──────────────────────────────────────────────────────────

    /// <summary>
    /// Get all VA drug classes, ordered by class code.
    /// Mirrors the VA drug classification system in File #50.605.
    /// </summary>
    [HttpGet("classes")]
    [ProducesResponseType(typeof(List<DrugClassDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<DrugClassDto>>> GetAllClasses()
    {
        try
        {
            List<DrugClassEntry> classes = await GetClassIndex().GetAllClassesAsync();
            return Ok(classes.Select(MapToClassDto).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving drug classes");
            return StatusCode(500, "An error occurred while retrieving drug classes");
        }
    }

    /// <summary>
    /// Search drug classes by code prefix or name.
    /// </summary>
    /// <param name="q">Class code prefix (e.g., "AM") or name text (e.g., "antibiotic")</param>
    /// <param name="maxResults">Maximum results (default 50)</param>
    [HttpGet("classes/search")]
    [ProducesResponseType(typeof(List<DrugClassDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<DrugClassDto>>> SearchClasses(
        [FromQuery] string q,
        [FromQuery] int maxResults = 50)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(q))
                return Ok(new List<DrugClassDto>());

            List<DrugClassEntry> results = await GetClassIndex().SearchAsync(q, maxResults);
            return Ok(results.Select(MapToClassDto).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching drug classes for '{Query}'", q);
            return StatusCode(500, "An error occurred while searching drug classes");
        }
    }

    /// <summary>
    /// Get a specific drug class by code.
    /// Mirrors CLASS^PSNAPIS.
    /// </summary>
    /// <param name="code">Drug class code (e.g., "AM100")</param>
    [HttpGet("classes/{code}")]
    [ProducesResponseType(typeof(DrugClassDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DrugClassDto>> GetClass(string code)
    {
        try
        {
            DrugClassEntry? entry = await GetClassIndex().GetClassAsync(code);
            if (entry == null)
                return NotFound(new { Message = $"Drug class '{code}' not found" });

            return Ok(MapToClassDto(entry));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving drug class '{Code}'", code);
            return StatusCode(500, "An error occurred while retrieving the drug class");
        }
    }

    // ─── STATUS ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Get the current load status of all NDF indexes.
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(NdfStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<NdfStatusDto>> GetStatus()
    {
        try
        {
            NdfProductIndexStatus productStatus = await GetProductIndex().GetStatusAsync();
            NdfGenericIndexStatus genericStatus = await GetGenericIndex().GetStatusAsync();
            NdfClassIndexStatus classStatus = await GetClassIndex().GetStatusAsync();

            return Ok(new NdfStatusDto
            {
                Products = productStatus,
                Generics = genericStatus,
                Classes = classStatus
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving NDF index status");
            return StatusCode(500, "An error occurred while retrieving NDF status");
        }
    }

    // ─── LOAD ENDPOINTS ────────────────────────────────────────────────────────

    /// <summary>
    /// Bulk-load VA Products into the index.
    /// Accepts a JSON array of VaProductIndexEntry records.
    /// In production this would parse the NDF global export (.zwr files).
    /// </summary>
    [HttpPost("products/load")]
    [ProducesResponseType(typeof(NdfProductIndexStatus), StatusCodes.Status200OK)]
    public async Task<IActionResult> LoadProducts([FromBody] List<VaProductIndexEntry> entries)
    {
        try
        {
            if (entries == null || entries.Count == 0)
                return BadRequest(new { Message = "No product entries provided" });

            _logger.LogInformation("Loading {Count} VA Products into NDF index", entries.Count);

            await GetProductIndex().LoadProductsAsync(entries);

            // Also persist each product as an individual grain
            foreach (VaProductIndexEntry entry in entries)
            {
                IVaProductGrain productGrain = GetProductGrain(entry.Ien);
                await productGrain.LoadProductAsync(
                    entry.Name,
                    entry.VaGenericIen,
                    entry.VaGenericName,
                    string.Empty,
                    entry.DosageFormName,
                    entry.Strength,
                    string.Empty,
                    entry.StrengthUnitName,
                    entry.Name,
                    entry.PrimaryDrugClassCode,
                    entry.PrimaryDrugClassName,
                    entry.FormularyIndicator,
                    null,
                    entry.ControlledSubstanceSchedule,
                    null,
                    null, null, null, null, null, null,
                    false, false,
                    entry.RxNormCode,
                    null,
                    entry.IsActive,
                    null,
                    new List<DrugIngredient>(),
                    entry.NdcCodes.Select(ndc => new NdcEntry { NdcCode = ndc }).ToList(),
                    new List<string>());
            }

            NdfProductIndexStatus status = await GetProductIndex().GetStatusAsync();

            _logger.LogInformation(
                "Loaded {Total} VA Products ({Formulary} formulary, {Active} active)",
                status.TotalProducts, status.FormularyProducts, status.ActiveProducts);

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading VA Products");
            return StatusCode(500, "An error occurred while loading VA Products");
        }
    }

    /// <summary>
    /// Bulk-load VA Generic drug names into the index.
    /// </summary>
    [HttpPost("generics/load")]
    [ProducesResponseType(typeof(NdfGenericIndexStatus), StatusCodes.Status200OK)]
    public async Task<IActionResult> LoadGenerics([FromBody] List<VaGenericEntry> entries)
    {
        try
        {
            if (entries == null || entries.Count == 0)
                return BadRequest(new { Message = "No generic entries provided" });

            _logger.LogInformation("Loading {Count} VA Generics into NDF index", entries.Count);
            await GetGenericIndex().LoadGenericsAsync(entries);
            NdfGenericIndexStatus status = await GetGenericIndex().GetStatusAsync();

            _logger.LogInformation("Loaded {Total} VA Generics ({Active} active)",
                status.TotalGenerics, status.ActiveGenerics);

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading VA Generics");
            return StatusCode(500, "An error occurred while loading VA Generics");
        }
    }

    /// <summary>
    /// Bulk-load VA drug classes into the index.
    /// </summary>
    [HttpPost("classes/load")]
    [ProducesResponseType(typeof(NdfClassIndexStatus), StatusCodes.Status200OK)]
    public async Task<IActionResult> LoadClasses([FromBody] List<DrugClassEntry> entries)
    {
        try
        {
            if (entries == null || entries.Count == 0)
                return BadRequest(new { Message = "No drug class entries provided" });

            _logger.LogInformation("Loading {Count} drug classes into NDF index", entries.Count);
            await GetClassIndex().LoadClassesAsync(entries);
            NdfClassIndexStatus status = await GetClassIndex().GetStatusAsync();

            _logger.LogInformation("Loaded {Total} drug classes", status.TotalClasses);
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading drug classes");
            return StatusCode(500, "An error occurred while loading drug classes");
        }
    }

    // ─── DEMO DATA ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Load a small set of representative NDF sample data for demonstration.
    /// This seeds the indexes with well-known VA drugs to enable testing
    /// without the full 29,584-product NDF dataset.
    /// </summary>
    [HttpPost("demo/load")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> LoadDemoData()
    {
        var saved = DemoSeedHelper.SetSystemContext();
        try
        {
            // Load demo drug classes
            List<DrugClassEntry> classes =
            [
                new() { Code = "AM100", Name = "AMINOGLYCOSIDES", IsActive = true },
                new() { Code = "AM110", Name = "AMINOGLYCOSIDES,OTHER", ParentCode = "AM100", IsActive = true },
                new() { Code = "AM200", Name = "CEPHALOSPORINS", IsActive = true },
                new() { Code = "AM210", Name = "CEPHALOSPORINS,1ST GEN", ParentCode = "AM200", IsActive = true },
                new() { Code = "CV000", Name = "CARDIOVASCULAR AGENTS", IsActive = true },
                new() { Code = "CV200", Name = "ANTIHYPERTENSIVE AGENTS", ParentCode = "CV000", IsActive = true },
                new() { Code = "CV400", Name = "ANTIARRHYTHMIC AGENTS", ParentCode = "CV000", IsActive = true },
                new() { Code = "HS000", Name = "HORMONES/SYNTHETICS/MODIFIERS", IsActive = true },
                new() { Code = "HS502", Name = "THYROID", ParentCode = "HS000", IsActive = true },
                new() { Code = "CN000", Name = "CENTRAL NERVOUS SYSTEM AGENTS", IsActive = true },
                new() { Code = "CN101", Name = "ANTIDEPRESSANTS,OTHER", ParentCode = "CN000", IsActive = true },
                new() { Code = "CN104", Name = "ANTIDEPRESSANTS,SSRI", ParentCode = "CN000", IsActive = true },
                new() { Code = "AU000", Name = "AUTONOMIC AGENTS", IsActive = true },
                new() { Code = "AU300", Name = "PARASYMPATHOMIMETICS", ParentCode = "AU000", IsActive = true },
            ];

            // Load demo VA generics
            List<VaGenericEntry> generics =
            [
                new() { Ien = "1", Name = "ATROPINE", IsActive = true, Vuid = "4019591" },
                new() { Ien = "2", Name = "AMOXICILLIN", IsActive = true, Vuid = "4019679" },
                new() { Ien = "3", Name = "METOPROLOL", IsActive = true, Vuid = "4019800" },
                new() { Ien = "4", Name = "LISINOPRIL", IsActive = true, Vuid = "4019825" },
                new() { Ien = "5", Name = "SERTRALINE", IsActive = true, Vuid = "4021053" },
                new() { Ien = "6", Name = "LEVOTHYROXINE", IsActive = true, Vuid = "4021060" },
                new() { Ien = "7", Name = "GENTAMICIN", IsActive = true, Vuid = "4019700" },
                new() { Ien = "8", Name = "CEPHALEXIN", IsActive = true, Vuid = "4019710" },
                new() { Ien = "9", Name = "AMIODARONE", IsActive = true, Vuid = "4019715" },
                new() { Ien = "10", Name = "WARFARIN", IsActive = true, Vuid = "4019760" },
            ];

            // Load demo VA products
            List<VaProductIndexEntry> products =
            [
                new() {
                    Ien = "1001", Name = "ATROPINE SO4 0.4MG/ML INJ",
                    VaGenericIen = "1", VaGenericName = "ATROPINE",
                    DosageFormName = "INJECTION", Strength = "0.4", StrengthUnitName = "MG/ML",
                    PrimaryDrugClassCode = "AU300", PrimaryDrugClassName = "PARASYMPATHOMIMETICS",
                    FormularyIndicator = true, IsActive = true, RxNormCode = "1190692",
                    NdcCodes = ["0002-1200-01", "0002-1200-10"]
                },
                new() {
                    Ien = "1002", Name = "ATROPINE SO4 0.4MG TAB",
                    VaGenericIen = "1", VaGenericName = "ATROPINE",
                    DosageFormName = "TABLET", Strength = "0.4", StrengthUnitName = "MG",
                    PrimaryDrugClassCode = "AU300", PrimaryDrugClassName = "PARASYMPATHOMIMETICS",
                    FormularyIndicator = true, IsActive = true,
                    NdcCodes = ["0002-1201-01"]
                },
                new() {
                    Ien = "2001", Name = "AMOXICILLIN 250MG CAP",
                    VaGenericIen = "2", VaGenericName = "AMOXICILLIN",
                    DosageFormName = "CAPSULE", Strength = "250", StrengthUnitName = "MG",
                    PrimaryDrugClassCode = "AM200", PrimaryDrugClassName = "CEPHALOSPORINS",
                    FormularyIndicator = true, IsActive = true,
                    NdcCodes = ["0093-3107-01"]
                },
                new() {
                    Ien = "2002", Name = "AMOXICILLIN 500MG CAP",
                    VaGenericIen = "2", VaGenericName = "AMOXICILLIN",
                    DosageFormName = "CAPSULE", Strength = "500", StrengthUnitName = "MG",
                    PrimaryDrugClassCode = "AM200", PrimaryDrugClassName = "CEPHALOSPORINS",
                    FormularyIndicator = true, IsActive = true,
                    NdcCodes = ["0093-3108-01", "0093-3108-05"]
                },
                new() {
                    Ien = "3001", Name = "METOPROLOL TARTRATE 25MG TAB",
                    VaGenericIen = "3", VaGenericName = "METOPROLOL",
                    DosageFormName = "TABLET", Strength = "25", StrengthUnitName = "MG",
                    PrimaryDrugClassCode = "CV200", PrimaryDrugClassName = "ANTIHYPERTENSIVE AGENTS",
                    FormularyIndicator = true, IsActive = true, RxNormCode = "866514",
                    NdcCodes = ["0006-0059-54"]
                },
                new() {
                    Ien = "3002", Name = "METOPROLOL TARTRATE 50MG TAB",
                    VaGenericIen = "3", VaGenericName = "METOPROLOL",
                    DosageFormName = "TABLET", Strength = "50", StrengthUnitName = "MG",
                    PrimaryDrugClassCode = "CV200", PrimaryDrugClassName = "ANTIHYPERTENSIVE AGENTS",
                    FormularyIndicator = true, IsActive = true, RxNormCode = "866516",
                    NdcCodes = ["0006-0060-54"]
                },
                new() {
                    Ien = "4001", Name = "LISINOPRIL 5MG TAB",
                    VaGenericIen = "4", VaGenericName = "LISINOPRIL",
                    DosageFormName = "TABLET", Strength = "5", StrengthUnitName = "MG",
                    PrimaryDrugClassCode = "CV200", PrimaryDrugClassName = "ANTIHYPERTENSIVE AGENTS",
                    FormularyIndicator = true, IsActive = true, RxNormCode = "311353",
                    NdcCodes = ["0006-0019-54"]
                },
                new() {
                    Ien = "4002", Name = "LISINOPRIL 10MG TAB",
                    VaGenericIen = "4", VaGenericName = "LISINOPRIL",
                    DosageFormName = "TABLET", Strength = "10", StrengthUnitName = "MG",
                    PrimaryDrugClassCode = "CV200", PrimaryDrugClassName = "ANTIHYPERTENSIVE AGENTS",
                    FormularyIndicator = true, IsActive = true, RxNormCode = "311354",
                    NdcCodes = ["0006-0020-54"]
                },
                new() {
                    Ien = "5001", Name = "SERTRALINE HCL 50MG TAB",
                    VaGenericIen = "5", VaGenericName = "SERTRALINE",
                    DosageFormName = "TABLET", Strength = "50", StrengthUnitName = "MG",
                    PrimaryDrugClassCode = "CN104", PrimaryDrugClassName = "ANTIDEPRESSANTS,SSRI",
                    FormularyIndicator = true, IsActive = true, RxNormCode = "312940",
                    NdcCodes = ["0049-4900-30"]
                },
                new() {
                    Ien = "5002", Name = "SERTRALINE HCL 100MG TAB",
                    VaGenericIen = "5", VaGenericName = "SERTRALINE",
                    DosageFormName = "TABLET", Strength = "100", StrengthUnitName = "MG",
                    PrimaryDrugClassCode = "CN104", PrimaryDrugClassName = "ANTIDEPRESSANTS,SSRI",
                    FormularyIndicator = true, IsActive = true,
                    NdcCodes = ["0049-4960-30"]
                },
                new() {
                    Ien = "6001", Name = "LEVOTHYROXINE NA 0.05MG TAB",
                    VaGenericIen = "6", VaGenericName = "LEVOTHYROXINE",
                    DosageFormName = "TABLET", Strength = "0.05", StrengthUnitName = "MG",
                    PrimaryDrugClassCode = "HS502", PrimaryDrugClassName = "THYROID",
                    FormularyIndicator = true, IsActive = true,
                    NdcCodes = ["0048-1040-03"]
                },
                new() {
                    Ien = "7001", Name = "GENTAMICIN SO4 40MG/ML INJ",
                    VaGenericIen = "7", VaGenericName = "GENTAMICIN",
                    DosageFormName = "INJECTION", Strength = "40", StrengthUnitName = "MG/ML",
                    PrimaryDrugClassCode = "AM100", PrimaryDrugClassName = "AMINOGLYCOSIDES",
                    FormularyIndicator = true, IsActive = true,
                    NdcCodes = ["0002-7232-50"]
                },
                new() {
                    Ien = "8001", Name = "CEPHALEXIN 250MG CAP",
                    VaGenericIen = "8", VaGenericName = "CEPHALEXIN",
                    DosageFormName = "CAPSULE", Strength = "250", StrengthUnitName = "MG",
                    PrimaryDrugClassCode = "AM210", PrimaryDrugClassName = "CEPHALOSPORINS,1ST GEN",
                    FormularyIndicator = true, IsActive = true,
                    NdcCodes = ["0093-3140-01"]
                },
                new() {
                    Ien = "9001", Name = "AMIODARONE HCL 200MG TAB",
                    VaGenericIen = "9", VaGenericName = "AMIODARONE",
                    DosageFormName = "TABLET", Strength = "200", StrengthUnitName = "MG",
                    PrimaryDrugClassCode = "CV400", PrimaryDrugClassName = "ANTIARRHYTHMIC AGENTS",
                    FormularyIndicator = false, IsActive = true, ControlledSubstanceSchedule = null,
                    NdcCodes = ["0187-0090-90"]
                },
                new() {
                    Ien = "10001", Name = "WARFARIN NA 5MG TAB",
                    VaGenericIen = "10", VaGenericName = "WARFARIN",
                    DosageFormName = "TABLET", Strength = "5", StrengthUnitName = "MG",
                    PrimaryDrugClassCode = "CV000", PrimaryDrugClassName = "CARDIOVASCULAR AGENTS",
                    FormularyIndicator = true, IsActive = true, RxNormCode = "855332",
                    NdcCodes = ["0056-0170-70"]
                },
            ];

            await Task.WhenAll(
                GetClassIndex().LoadClassesAsync(classes),
                GetGenericIndex().LoadGenericsAsync(generics)
            );
            await GetProductIndex().LoadProductsAsync(products);

            NdfProductIndexStatus productStatus = await GetProductIndex().GetStatusAsync();

            _logger.LogInformation("Demo NDF data loaded: {Products} products, {Generics} generics, {Classes} classes",
                productStatus.TotalProducts, generics.Count, classes.Count);

            return Ok(new
            {
                Message = "Demo NDF data loaded successfully",
                Products = productStatus.TotalProducts,
                Generics = generics.Count,
                Classes = classes.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading demo NDF data");
            return StatusCode(500, "An error occurred while loading demo data");
        }
        finally
        {
            DemoSeedHelper.RestoreContext(saved);
        }
    }

    // ─── MAPPING ───────────────────────────────────────────────────────────────

    private static VaProductSummaryDto MapToSummaryDto(VaProductIndexEntry entry) => new()
    {
        Ien = entry.Ien,
        Name = entry.Name,
        VaGenericIen = entry.VaGenericIen,
        VaGenericName = entry.VaGenericName,
        DosageFormName = entry.DosageFormName,
        Strength = entry.Strength,
        StrengthUnitName = entry.StrengthUnitName,
        PrimaryDrugClassCode = entry.PrimaryDrugClassCode,
        PrimaryDrugClassName = entry.PrimaryDrugClassName,
        FormularyIndicator = entry.FormularyIndicator,
        IsActive = entry.IsActive,
        RxNormCode = entry.RxNormCode,
        ControlledSubstanceSchedule = entry.ControlledSubstanceSchedule,
        NdcCodes = entry.NdcCodes
    };

    private static VaProductDetailDto MapToDetailDto(VaProductState product) => new()
    {
        Ien = product.Ien,
        Name = product.Name,
        VaGenericIen = product.VaGenericIen,
        VaGenericName = product.VaGenericName,
        DosageFormName = product.DosageFormName,
        Strength = product.Strength,
        StrengthUnitName = product.StrengthUnitName,
        PrintName = product.PrintName,
        PrimaryDrugClassCode = product.PrimaryDrugClassCode,
        PrimaryDrugClassName = product.PrimaryDrugClassName,
        SecondaryDrugClassCodes = product.SecondaryDrugClassCodes,
        FormularyIndicator = product.FormularyIndicator,
        FormularyRestrictions = product.FormularyRestrictions,
        ControlledSubstanceSchedule = product.ControlledSubstanceSchedule,
        CopayTier = product.CopayTier,
        MaxSingleDose = product.MaxSingleDose,
        MinSingleDose = product.MinSingleDose,
        MaxDailyDose = product.MaxDailyDose,
        MinDailyDose = product.MinDailyDose,
        MaxCumulativeDose = product.MaxCumulativeDose,
        DoseUnit = product.DoseUnit,
        IsDosageCheckExcluded = product.IsDosageCheckExcluded,
        IsHazardousWaste = product.IsHazardousWaste,
        RxNormCode = product.RxNormCode,
        Vuid = product.Vuid,
        IsActive = product.IsActive,
        InactivationDate = product.InactivationDate,
        Ingredients = product.Ingredients,
        NdcCodes = product.NdcCodes
    };

    private static VaGenericDto MapToGenericDto(VaGenericEntry entry) => new()
    {
        Ien = entry.Ien,
        Name = entry.Name,
        Vuid = entry.Vuid,
        IsActive = entry.IsActive,
        ProductCount = entry.ProductIens.Count
    };

    private static DrugClassDto MapToClassDto(DrugClassEntry entry) => new()
    {
        Code = entry.Code,
        Name = entry.Name,
        ParentCode = entry.ParentCode,
        IsActive = entry.IsActive
    };
}

// ─── DTOs ─────────────────────────────────────────────────────────────────────

/// <summary>Lightweight summary of a VA Product for search results.</summary>
public class VaProductSummaryDto
{
    public string Ien { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string VaGenericIen { get; set; } = string.Empty;
    public string VaGenericName { get; set; } = string.Empty;
    public string DosageFormName { get; set; } = string.Empty;
    public string Strength { get; set; } = string.Empty;
    public string StrengthUnitName { get; set; } = string.Empty;
    public string PrimaryDrugClassCode { get; set; } = string.Empty;
    public string PrimaryDrugClassName { get; set; } = string.Empty;
    public bool FormularyIndicator { get; set; }
    public bool IsActive { get; set; }
    public string? RxNormCode { get; set; }
    public string? ControlledSubstanceSchedule { get; set; }
    public List<string> NdcCodes { get; set; } = new();
}

/// <summary>Full detail of a VA Product including ingredients and dosing limits.</summary>
public class VaProductDetailDto
{
    public string Ien { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string VaGenericIen { get; set; } = string.Empty;
    public string VaGenericName { get; set; } = string.Empty;
    public string DosageFormName { get; set; } = string.Empty;
    public string Strength { get; set; } = string.Empty;
    public string StrengthUnitName { get; set; } = string.Empty;
    public string PrintName { get; set; } = string.Empty;
    public string PrimaryDrugClassCode { get; set; } = string.Empty;
    public string PrimaryDrugClassName { get; set; } = string.Empty;
    public List<string> SecondaryDrugClassCodes { get; set; } = new();
    public bool FormularyIndicator { get; set; }
    public string? FormularyRestrictions { get; set; }
    public string? ControlledSubstanceSchedule { get; set; }
    public string? CopayTier { get; set; }
    public decimal? MaxSingleDose { get; set; }
    public decimal? MinSingleDose { get; set; }
    public decimal? MaxDailyDose { get; set; }
    public decimal? MinDailyDose { get; set; }
    public decimal? MaxCumulativeDose { get; set; }
    public string? DoseUnit { get; set; }
    public bool IsDosageCheckExcluded { get; set; }
    public bool IsHazardousWaste { get; set; }
    public string? RxNormCode { get; set; }
    public string? Vuid { get; set; }
    public bool IsActive { get; set; }
    public DateTime? InactivationDate { get; set; }
    public List<DrugIngredient> Ingredients { get; set; } = new();
    public List<NdcEntry> NdcCodes { get; set; } = new();
}

/// <summary>VA Generic drug name DTO.</summary>
public class VaGenericDto
{
    public string Ien { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Vuid { get; set; }
    public bool IsActive { get; set; }
    public int ProductCount { get; set; }
}

/// <summary>VA drug class DTO.</summary>
public class DrugClassDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ParentCode { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>Combined NDF index status response.</summary>
public class NdfStatusDto
{
    public NdfProductIndexStatus Products { get; set; } = new();
    public NdfGenericIndexStatus Generics { get; set; } = new();
    public NdfClassIndexStatus Classes { get; set; } = new();
}
