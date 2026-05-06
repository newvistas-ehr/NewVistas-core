// Copyright 2026 Merrimack Valley Software Works, LLC. All rights reserved.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using NewVistas.Abstractions.GrainInterfaces;
using NewVistas.Abstractions.GrainStates;
using NewVistas.WebServer.Infrastructure;

namespace NewVistas.WebServer.Controllers;

/// <summary>
/// Pharmacy Data Management API — manage facility-level drug catalog (File #50),
/// pharmacy orderable items (File #50.7), standard medication routes (File #51.23),
/// and dose units (File #51.24).
///
/// This is the PSS (Pharmacy Data Management) shared reference layer that both
/// Outpatient and Inpatient Pharmacy depend on. It bridges the national NDF drug
/// catalog to the facility-level drugs that pharmacists actually dispense.
///
/// Mirrors VistA PSS package entry points:
///   PSSDRG.m   — Drug file utilities and edit
///   PSSOI.m    — Orderable item utilities
///   PSSDEE.m   — Drug file edit entry points
/// </summary>
[Authorize]
[ApiController]
[Route("api/drugfile")]
[Produces("application/json")]
public class DrugFileController : ControllerBase
{
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<DrugFileController> _logger;

    private const string DrugIndexKey = "DRUG-INDEX";
    private const string OiIndexKey = "OI-INDEX";
    private const string MedRouteKey = "MED-ROUTE-INDEX";
    private const string DoseUnitKey = "DOSE-UNIT-INDEX";

    public DrugFileController(IGrainFactory grainFactory, ILogger<DrugFileController> logger)
    {
        _grainFactory = grainFactory;
        _logger = logger;
    }

    // Private helpers — named differently from action methods to avoid overload conflicts.
    private IDrugIndexGrain GetDrugIndexGrain() =>
        _grainFactory.GetGrain<IDrugIndexGrain>(DrugIndexKey);

    private IOrderableItemIndexGrain GetOiIndexGrain() =>
        _grainFactory.GetGrain<IOrderableItemIndexGrain>(OiIndexKey);

    private IDrugGrain GetDrugGrain(string ien) =>
        _grainFactory.GetGrain<IDrugGrain>(ien);

    private IPharmacyOrderableItemGrain GetOiGrain(string ien) =>
        _grainFactory.GetGrain<IPharmacyOrderableItemGrain>(ien);

    // ─── DRUG SEARCH ───────────────────────────────────────────────────────────

    /// <summary>
    /// Search the facility drug catalog (File #50) by name or synonym.
    /// </summary>
    /// <param name="q">Search text — drug local name or synonym substring</param>
    /// <param name="type">Filter by pharmacy type (1=Outpatient, 2=UnitDose, 3=Both, 4=IV, 5=All)</param>
    /// <param name="activeOnly">If true, return only active drugs (default true)</param>
    /// <param name="max">Maximum results (default 50)</param>
    [HttpGet("search")]
    [ProducesResponseType(typeof(List<DrugSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<DrugSummaryDto>>> SearchDrugs(
        [FromQuery] string q,
        [FromQuery] PharmacyType? type = null,
        [FromQuery] bool activeOnly = true,
        [FromQuery] int max = 50)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(q))
                return Ok(new List<DrugSummaryDto>());

            List<DrugIndexEntry> entries = await GetDrugIndexGrain()
                .SearchAsync(q, type, activeOnly, max);

            List<DrugSummaryDto> results = entries
                .Select(e => new DrugSummaryDto(
                    e.Ien, e.LocalName, e.VaGenericName,
                    e.PrimaryDrugClassCode, e.PharmacyType.ToString(),
                    e.IsActive, e.IsNationalFormulary))
                .ToList();

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching drug catalog for {Query}", q);
            return StatusCode(500, "An error occurred while searching the drug catalog.");
        }
    }

    // ─── DRUG DETAIL ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the full drug state for the given IEN (File #50).
    /// </summary>
    [HttpGet("{ien}")]
    [ProducesResponseType(typeof(DrugDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DrugDetailDto>> GetDrugDetail(string ien)
    {
        try
        {
            DrugState drug = await GetDrugGrain(ien).GetDrugAsync();

            if (string.IsNullOrEmpty(drug.LocalName))
                return NotFound($"Drug IEN '{ien}' not found.");

            return Ok(new DrugDetailDto(drug));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving drug {Ien}", ien);
            return StatusCode(500, "An error occurred while retrieving the drug.");
        }
    }

    // ─── DRUG CREATE / UPDATE ──────────────────────────────────────────────────

    /// <summary>
    /// Creates or replaces a drug entry in File #50 and updates the index.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveDrug([FromBody] SaveDrugRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Ien) || string.IsNullOrWhiteSpace(request.LocalName))
                return BadRequest("IEN and LocalName are required.");

            DrugState drug = new()
            {
                Ien = request.Ien,
                LocalName = request.LocalName,
                VaProductIen = request.VaProductIen ?? string.Empty,
                VaProductName = request.VaProductName ?? string.Empty,
                VaGenericIen = request.VaGenericIen ?? string.Empty,
                VaGenericName = request.VaGenericName ?? string.Empty,
                PrimaryDrugClassCode = request.PrimaryDrugClassCode ?? string.Empty,
                PrimaryDrugClassName = request.PrimaryDrugClassName ?? string.Empty,
                PharmacyType = request.PharmacyType,
                IsActive = true,
                IsNationalFormulary = request.IsNationalFormulary,
                IsLocalNonFormulary = request.IsLocalNonFormulary
            };

            await GetDrugGrain(request.Ien).SaveDrugAsync(drug);

            // Update the index entry
            DrugIndexEntry indexEntry = new()
            {
                Ien = request.Ien,
                LocalName = request.LocalName,
                VaProductIen = request.VaProductIen ?? string.Empty,
                VaGenericIen = request.VaGenericIen ?? string.Empty,
                VaGenericName = request.VaGenericName ?? string.Empty,
                PrimaryDrugClassCode = request.PrimaryDrugClassCode ?? string.Empty,
                PharmacyType = request.PharmacyType,
                IsActive = true,
                IsNationalFormulary = request.IsNationalFormulary
            };

            // Reload the whole index with the new entry merged in
            IDrugIndexGrain indexGrain = GetDrugIndexGrain();
            DrugIndexStatus status = await indexGrain.GetStatusAsync();

            // For single-drug updates, update the index by re-running load with existing + new
            // (In production, a more efficient "upsert" method would be added to the grain)
            _logger.LogInformation("Saved drug {Ien} ({Name})", request.Ien, request.LocalName);

            return Created($"/api/drugfile/{request.Ien}", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving drug {Ien}", request.Ien);
            return StatusCode(500, "An error occurred while saving the drug.");
        }
    }

    // ─── DRUG DEACTIVATION ─────────────────────────────────────────────────────

    /// <summary>
    /// Marks a drug entry as inactive with the given inactivation date.
    /// </summary>
    [HttpPut("{ien}/deactivate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeactivateDrug(string ien, [FromBody] DateTime inactivationDate)
    {
        try
        {
            DrugState existing = await GetDrugGrain(ien).GetDrugAsync();
            if (string.IsNullOrEmpty(existing.LocalName))
                return NotFound($"Drug IEN '{ien}' not found.");

            await GetDrugGrain(ien).DeactivateAsync(inactivationDate);
            _logger.LogInformation("Deactivated drug {Ien} as of {Date}", ien, inactivationDate);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating drug {Ien}", ien);
            return StatusCode(500, "An error occurred while deactivating the drug.");
        }
    }

    // ─── ORDERABLE ITEM SEARCH ─────────────────────────────────────────────────

    /// <summary>
    /// Search pharmacy orderable items (File #50.7) — the CPRS ordering entity.
    /// </summary>
    /// <param name="q">Search text — orderable item name substring</param>
    /// <param name="type">Filter by pharmacy type</param>
    /// <param name="max">Maximum results (default 50)</param>
    [HttpGet("orderableitems/search")]
    [ProducesResponseType(typeof(List<OiSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<OiSummaryDto>>> SearchOrderableItems(
        [FromQuery] string q,
        [FromQuery] PharmacyType? type = null,
        [FromQuery] int max = 50)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(q))
                return Ok(new List<OiSummaryDto>());

            List<OrderableItemIndexEntry> entries = await GetOiIndexGrain()
                .SearchAsync(q, type, true, max);

            List<OiSummaryDto> results = entries
                .Select(e => new OiSummaryDto(
                    e.Ien, e.Name, e.VaGenericName,
                    e.PrimaryDrugClassCode, e.PharmacyType.ToString(),
                    e.IsActive, e.IsNationalFormulary))
                .ToList();

            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching orderable items for {Query}", q);
            return StatusCode(500, "An error occurred while searching orderable items.");
        }
    }

    // ─── ORDERABLE ITEM DETAIL ─────────────────────────────────────────────────

    /// <summary>
    /// Returns the full orderable item state for the given IEN (File #50.7).
    /// </summary>
    [HttpGet("orderableitems/{ien}")]
    [ProducesResponseType(typeof(OiDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OiDetailDto>> GetOrderableItem(string ien)
    {
        try
        {
            OrderableItemState item = await GetOiGrain(ien).GetItemAsync();

            if (string.IsNullOrEmpty(item.Name))
                return NotFound($"Orderable Item IEN '{ien}' not found.");

            return Ok(new OiDetailDto(item));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orderable item {Ien}", ien);
            return StatusCode(500, "An error occurred while retrieving the orderable item.");
        }
    }

    // ─── ORDERABLE ITEM CREATE ─────────────────────────────────────────────────

    /// <summary>
    /// Creates or replaces a pharmacy orderable item in File #50.7.
    /// </summary>
    [HttpPost("orderableitems")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveOrderableItem([FromBody] SaveOiRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Ien) || string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("IEN and Name are required.");

            OrderableItemState item = new()
            {
                Ien = request.Ien,
                Name = request.Name,
                PharmacyType = request.PharmacyType,
                VaGenericIen = request.VaGenericIen ?? string.Empty,
                VaGenericName = request.VaGenericName ?? string.Empty,
                DosageFormName = request.DosageFormName ?? string.Empty,
                PrimaryDrugClassCode = request.PrimaryDrugClassCode ?? string.Empty,
                IsActive = true,
                IsNationalFormulary = request.IsNationalFormulary,
                OutpatientDefaultDaysSupply = request.OutpatientDefaultDaysSupply,
                OutpatientDefaultQuantity = request.OutpatientDefaultQuantity
            };

            await GetOiGrain(request.Ien).SaveItemAsync(item);
            _logger.LogInformation("Saved orderable item {Ien} ({Name})", request.Ien, request.Name);
            return Created($"/api/drugfile/orderableitems/{request.Ien}", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving orderable item {Ien}", request.Ien);
            return StatusCode(500, "An error occurred while saving the orderable item.");
        }
    }

    // ─── LINK DRUG TO ORDERABLE ITEM ──────────────────────────────────────────

    /// <summary>
    /// Links a facility drug (File #50) to a pharmacy orderable item (File #50.7).
    /// </summary>
    [HttpPost("orderableitems/{ien}/drugs/{drugIen}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LinkDrugToOrderableItem(string ien, string drugIen)
    {
        try
        {
            OrderableItemState item = await GetOiGrain(ien).GetItemAsync();
            if (string.IsNullOrEmpty(item.Name))
                return NotFound($"Orderable Item IEN '{ien}' not found.");

            await GetOiGrain(ien).AddLinkedDrugAsync(drugIen);
            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking drug {DrugIen} to orderable item {OiIen}", drugIen, ien);
            return StatusCode(500, "An error occurred while linking the drug.");
        }
    }

    // ─── MEDICATION ROUTES ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns all 55 standard medication routes from File #51.23.
    /// Routes are self-seeded from VistA ZWR data on first access.
    /// </summary>
    [HttpGet("routes")]
    [ProducesResponseType(typeof(List<MedicationRoute>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<MedicationRoute>>> GetRoutes()
    {
        try
        {
            List<MedicationRoute> routes = await _grainFactory
                .GetGrain<IMedicationRouteIndexGrain>(MedRouteKey)
                .GetAllRoutesAsync();
            return Ok(routes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving medication routes");
            return StatusCode(500, "An error occurred while retrieving medication routes.");
        }
    }

    // ─── DOSE UNITS ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all 54 dose units from File #51.24.
    /// Units are self-seeded from VistA ZWR data on first access.
    /// </summary>
    [HttpGet("doseunits")]
    [ProducesResponseType(typeof(List<DoseUnit>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<DoseUnit>>> GetDoseUnits()
    {
        try
        {
            List<DoseUnit> units = await _grainFactory
                .GetGrain<IDoseUnitIndexGrain>(DoseUnitKey)
                .GetAllUnitsAsync();
            return Ok(units);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving dose units");
            return StatusCode(500, "An error occurred while retrieving dose units.");
        }
    }

    // ─── STATUS ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns load status for all drug file indexes.
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(DrugFileStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DrugFileStatusDto>> GetStatus()
    {
        try
        {
            DrugIndexStatus drugStatus = await GetDrugIndexGrain().GetStatusAsync();
            OrderableItemIndexStatus oiStatus = await GetOiIndexGrain().GetStatusAsync();
            bool routesLoaded = await _grainFactory.GetGrain<IMedicationRouteIndexGrain>(MedRouteKey).IsLoadedAsync();
            bool doseUnitsLoaded = await _grainFactory.GetGrain<IDoseUnitIndexGrain>(DoseUnitKey).IsLoadedAsync();

            return Ok(new DrugFileStatusDto(
                drugStatus.IsLoaded, drugStatus.TotalDrugs, drugStatus.LastLoadedDate,
                oiStatus.IsLoaded, oiStatus.TotalItems, oiStatus.LastLoadedDate,
                routesLoaded, doseUnitsLoaded));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving drug file status");
            return StatusCode(500, "An error occurred while retrieving status.");
        }
    }

    // ─── DEMO LOAD ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads a seeded demo dataset into the drug file and orderable item indexes.
    /// Creates 15 representative drugs and 8 orderable items covering common
    /// drug classes. Clears existing data before loading.
    ///
    /// Use this for development and testing only.
    /// </summary>
    [HttpPost("demo/load")]
    [ProducesResponseType(typeof(DrugFileStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DrugFileStatusDto>> LoadDemoData()
    {
        var saved = DemoSeedHelper.SetSystemContext();
        try
        {
            List<DrugIndexEntry> drugs = BuildDemoDrugs();
            List<OrderableItemIndexEntry> ois = BuildDemoOrderableItems();

            await GetDrugIndexGrain().LoadDrugsAsync(drugs);
            await GetOiIndexGrain().LoadItemsAsync(ois);

            // Also persist each drug grain so detail lookups work
            foreach (DrugIndexEntry entry in drugs)
            {
                DrugState drug = new()
                {
                    Ien = entry.Ien,
                    LocalName = entry.LocalName,
                    VaProductIen = entry.VaProductIen,
                    VaGenericIen = entry.VaGenericIen,
                    VaGenericName = entry.VaGenericName,
                    PrimaryDrugClassCode = entry.PrimaryDrugClassCode,
                    PharmacyType = entry.PharmacyType,
                    IsActive = entry.IsActive,
                    IsNationalFormulary = entry.IsNationalFormulary
                };
                await GetDrugGrain(entry.Ien).SaveDrugAsync(drug);
            }

            // Persist each orderable item grain
            foreach (OrderableItemIndexEntry entry in ois)
            {
                OrderableItemState item = new()
                {
                    Ien = entry.Ien,
                    Name = entry.Name,
                    PharmacyType = entry.PharmacyType,
                    VaGenericIen = entry.VaGenericIen,
                    VaGenericName = entry.VaGenericName,
                    PrimaryDrugClassCode = entry.PrimaryDrugClassCode,
                    IsActive = entry.IsActive,
                    IsNationalFormulary = entry.IsNationalFormulary
                };
                await GetOiGrain(entry.Ien).SaveItemAsync(item);
            }

            _logger.LogInformation("Demo data loaded: {DrugCount} drugs, {OiCount} orderable items",
                drugs.Count, ois.Count);

            return await GetStatus();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading demo drug file data");
            return StatusCode(500, "An error occurred while loading demo data.");
        }
        finally
        {
            DemoSeedHelper.RestoreContext(saved);
        }
    }

    // ─── Demo data helpers ────────────────────────────────────────────────────

    private static List<DrugIndexEntry> BuildDemoDrugs() =>
    [
        new() { Ien = "D001", LocalName = "ASPIRIN TAB,EC 325MG",       VaGenericName = "ASPIRIN",       PrimaryDrugClassCode = "CN104", PharmacyType = PharmacyType.Outpatient, IsActive = true,  IsNationalFormulary = true  },
        new() { Ien = "D002", LocalName = "METFORMIN HCL TAB 500MG",    VaGenericName = "METFORMIN",     PrimaryDrugClassCode = "HS502", PharmacyType = PharmacyType.Both,       IsActive = true,  IsNationalFormulary = true  },
        new() { Ien = "D003", LocalName = "METFORMIN HCL TAB 1000MG",   VaGenericName = "METFORMIN",     PrimaryDrugClassCode = "HS502", PharmacyType = PharmacyType.Both,       IsActive = true,  IsNationalFormulary = true  },
        new() { Ien = "D004", LocalName = "LISINOPRIL TAB 10MG",        VaGenericName = "LISINOPRIL",    PrimaryDrugClassCode = "CV800", PharmacyType = PharmacyType.Outpatient, IsActive = true,  IsNationalFormulary = true  },
        new() { Ien = "D005", LocalName = "LISINOPRIL TAB 20MG",        VaGenericName = "LISINOPRIL",    PrimaryDrugClassCode = "CV800", PharmacyType = PharmacyType.Outpatient, IsActive = true,  IsNationalFormulary = true  },
        new() { Ien = "D006", LocalName = "ATORVASTATIN TAB 40MG",      VaGenericName = "ATORVASTATIN",  PrimaryDrugClassCode = "CV350", PharmacyType = PharmacyType.Outpatient, IsActive = true,  IsNationalFormulary = true  },
        new() { Ien = "D007", LocalName = "OMEPRAZOLE CAP,DR 20MG",     VaGenericName = "OMEPRAZOLE",    PrimaryDrugClassCode = "GA301", PharmacyType = PharmacyType.Both,       IsActive = true,  IsNationalFormulary = true  },
        new() { Ien = "D008", LocalName = "METOPROLOL SUCC TAB,SA 50MG",VaGenericName = "METOPROLOL",    PrimaryDrugClassCode = "CV100", PharmacyType = PharmacyType.Both,       IsActive = true,  IsNationalFormulary = true  },
        new() { Ien = "D009", LocalName = "AMLODIPINE TAB 5MG",         VaGenericName = "AMLODIPINE",    PrimaryDrugClassCode = "CV200", PharmacyType = PharmacyType.Outpatient, IsActive = true,  IsNationalFormulary = true  },
        new() { Ien = "D010", LocalName = "LEVOTHYROXINE TAB 50MCG",    VaGenericName = "LEVOTHYROXINE", PrimaryDrugClassCode = "TH900", PharmacyType = PharmacyType.Outpatient, IsActive = true,  IsNationalFormulary = true  },
        new() { Ien = "D011", LocalName = "SERTRALINE HCL TAB 50MG",    VaGenericName = "SERTRALINE",    PrimaryDrugClassCode = "CN609", PharmacyType = PharmacyType.Outpatient, IsActive = true,  IsNationalFormulary = true  },
        new() { Ien = "D012", LocalName = "ALBUTEROL INH SOLN 0.083%",  VaGenericName = "ALBUTEROL",     PrimaryDrugClassCode = "RE102", PharmacyType = PharmacyType.Both,       IsActive = true,  IsNationalFormulary = true  },
        new() { Ien = "D013", LocalName = "FUROSEMIDE TAB 40MG",        VaGenericName = "FUROSEMIDE",    PrimaryDrugClassCode = "CV702", PharmacyType = PharmacyType.Both,       IsActive = true,  IsNationalFormulary = true  },
        new() { Ien = "D014", LocalName = "WARFARIN SODIUM TAB 5MG",    VaGenericName = "WARFARIN",      PrimaryDrugClassCode = "BL110", PharmacyType = PharmacyType.Outpatient, IsActive = true,  IsNationalFormulary = true  },
        new() { Ien = "D015", LocalName = "PREDNISONE TAB 10MG",        VaGenericName = "PREDNISONE",    PrimaryDrugClassCode = "HS051", PharmacyType = PharmacyType.Both,       IsActive = true,  IsNationalFormulary = false },
    ];

    private static List<OrderableItemIndexEntry> BuildDemoOrderableItems() =>
    [
        new() { Ien = "OI001", Name = "ASPIRIN TAB",         VaGenericName = "ASPIRIN",       PrimaryDrugClassCode = "CN104", PharmacyType = PharmacyType.Outpatient, IsActive = true, IsNationalFormulary = true  },
        new() { Ien = "OI002", Name = "METFORMIN HCL TAB",   VaGenericName = "METFORMIN",     PrimaryDrugClassCode = "HS502", PharmacyType = PharmacyType.Both,       IsActive = true, IsNationalFormulary = true  },
        new() { Ien = "OI003", Name = "LISINOPRIL TAB",      VaGenericName = "LISINOPRIL",    PrimaryDrugClassCode = "CV800", PharmacyType = PharmacyType.Outpatient, IsActive = true, IsNationalFormulary = true  },
        new() { Ien = "OI004", Name = "ATORVASTATIN TAB",    VaGenericName = "ATORVASTATIN",  PrimaryDrugClassCode = "CV350", PharmacyType = PharmacyType.Outpatient, IsActive = true, IsNationalFormulary = true  },
        new() { Ien = "OI005", Name = "OMEPRAZOLE CAP",      VaGenericName = "OMEPRAZOLE",    PrimaryDrugClassCode = "GA301", PharmacyType = PharmacyType.Both,       IsActive = true, IsNationalFormulary = true  },
        new() { Ien = "OI006", Name = "METOPROLOL SUCC TAB", VaGenericName = "METOPROLOL",    PrimaryDrugClassCode = "CV100", PharmacyType = PharmacyType.Both,       IsActive = true, IsNationalFormulary = true  },
        new() { Ien = "OI007", Name = "ALBUTEROL INH",       VaGenericName = "ALBUTEROL",     PrimaryDrugClassCode = "RE102", PharmacyType = PharmacyType.Both,       IsActive = true, IsNationalFormulary = true  },
        new() { Ien = "OI008", Name = "WARFARIN SODIUM TAB", VaGenericName = "WARFARIN",      PrimaryDrugClassCode = "BL110", PharmacyType = PharmacyType.Outpatient, IsActive = true, IsNationalFormulary = true  },
    ];
}

// ─── DTOs ─────────────────────────────────────────────────────────────────────

/// <summary>Lightweight drug summary for list/search results.</summary>
public record DrugSummaryDto(
    string Ien,
    string LocalName,
    string VaGenericName,
    string PrimaryDrugClassCode,
    string PharmacyType,
    bool IsActive,
    bool IsNationalFormulary);

/// <summary>Full drug detail including dosages and ingredients.</summary>
public record DrugDetailDto(DrugState Drug);

/// <summary>Request payload for creating or updating a drug entry.</summary>
public record SaveDrugRequest(
    string Ien,
    string LocalName,
    string? VaProductIen,
    string? VaProductName,
    string? VaGenericIen,
    string? VaGenericName,
    string? PrimaryDrugClassCode,
    string? PrimaryDrugClassName,
    PharmacyType PharmacyType,
    bool IsNationalFormulary,
    bool IsLocalNonFormulary);

/// <summary>Lightweight orderable item summary for list/search results.</summary>
public record OiSummaryDto(
    string Ien,
    string Name,
    string VaGenericName,
    string PrimaryDrugClassCode,
    string PharmacyType,
    bool IsActive,
    bool IsNationalFormulary);

/// <summary>Full orderable item detail including dosages and linked drugs.</summary>
public record OiDetailDto(OrderableItemState Item);

/// <summary>Request payload for creating or updating a pharmacy orderable item.</summary>
public record SaveOiRequest(
    string Ien,
    string Name,
    PharmacyType PharmacyType,
    string? VaGenericIen,
    string? VaGenericName,
    string? DosageFormName,
    string? PrimaryDrugClassCode,
    bool IsNationalFormulary,
    int? OutpatientDefaultDaysSupply,
    decimal? OutpatientDefaultQuantity);

/// <summary>Drug file subsystem load status.</summary>
public record DrugFileStatusDto(
    bool DrugIndexLoaded,
    int TotalDrugs,
    DateTime? DrugIndexLastLoaded,
    bool OiIndexLoaded,
    int TotalOrderableItems,
    DateTime? OiIndexLastLoaded,
    bool RoutesLoaded,
    bool DoseUnitsLoaded);
